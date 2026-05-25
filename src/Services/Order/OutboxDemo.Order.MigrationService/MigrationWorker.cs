using System.Diagnostics;

namespace OutboxDemo.Order.MigrationService;

/// <summary>
/// One-shot worker that brings the Order database to a state in which the
/// rest of the platform can start safely.
/// </summary>
/// <remarks>
/// Responsibilities:
/// <list type="bullet">
///   <item><description>Creates the EF Core schema if missing.</description></item>
///   <item><description>Ensures the PostgreSQL logical-replication publication used by Debezium exists.</description></item>
/// </list>
/// The host is stopped on completion so Aspire can release any downstream service
/// that was waiting for the migration to finish.
/// </remarks>
internal sealed class MigrationWorker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<MigrationWorker> logger) : BackgroundService
{
    /// <summary>OpenTelemetry activity source name registered by this worker.</summary>
    internal const string ActivitySourceName = "OutboxDemo.Order.MigrationService";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = ActivitySource.StartActivity("Migration", ActivityKind.Internal);

        try
        {
            // Resolve scoped services (e.g. DbContext) inside the long-lived hosted service.
            await using var scope = serviceProvider.CreateAsyncScope();

            await EnsureDatabaseSchemaAsync(scope.ServiceProvider, stoppingToken);
            await EnsureOutboxPublicationAsync(scope.ServiceProvider, stoppingToken);
            // Creating the NATS stream is the Debezium Server's responsibility (create-stream=true in config).
            // If we created one too, Debezium would not be able to create its own due to subject overlap.

            logger.MigrationCompleted();
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            logger.MigrationFailed(ex);
            throw;
        }
        finally
        {
            // Migration is a one-shot job: signal the host to shut down regardless of success or failure
            // so Aspire's WaitForCompletion resolves and dependent services can start (or fail fast).
            hostApplicationLifetime.StopApplication();
        }
    }

    /// <summary>
    /// Ensures that the EF Core managed schema exists, retrying on transient connection failures.
    /// </summary>
    private async Task EnsureDatabaseSchemaAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<OrderDbContext>();

        logger.EnsuringDatabaseSchema(db.Database.GetDbConnection().Database);

        // The Npgsql provider's execution strategy retries DDL on transient failures
        // (e.g. database not yet accepting connections during cold start).
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        });
    }

    /// <summary>
    /// Creates the PostgreSQL <c>PUBLICATION</c> and sets <c>REPLICA IDENTITY FULL</c>
    /// on the outbox table so that Debezium can stream the table via logical replication.
    /// </summary>
    /// <remarks>
    /// The block is idempotent: it can be re-run on every startup without raising errors.
    /// <c>REPLICA IDENTITY FULL</c> guarantees that DELETE/UPDATE events carry the old row image,
    /// which is required by the "no additional space" outbox variant.
    /// </remarks>
    private async Task EnsureOutboxPublicationAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<OrderDbContext>();

        logger.EnsuringOutboxPublication();

        const string sql = """
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_publication WHERE pubname = 'dbz_outbox_publication') THEN
                    CREATE PUBLICATION dbz_outbox_publication FOR TABLE public.outbox_messages;
                END IF;
            END
            $$;

            ALTER TABLE public.outbox_messages REPLICA IDENTITY FULL;
            """;

        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

}
