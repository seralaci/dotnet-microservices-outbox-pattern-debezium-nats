namespace OutboxDemo.Order.MigrationService;

/// <summary>
/// Strongly-typed, source-generated logger extensions for the migration worker.
/// Using <see cref="LoggerMessageAttribute"/> avoids boxing and string formatting on the hot path.
/// </summary>
internal static partial class LogMessages
{
    /// <summary>Migration finished successfully and the host is about to stop.</summary>
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Migration completed successfully.")]
    public static partial void MigrationCompleted(this ILogger logger);

    /// <summary>Migration failed; the captured exception is attached for diagnostics.</summary>
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Migration failed.")]
    public static partial void MigrationFailed(this ILogger logger, Exception exception);

    /// <summary>About to call <c>EnsureCreatedAsync</c> on the specified database.</summary>
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Ensuring database schema for {Database}.")]
    public static partial void EnsuringDatabaseSchema(this ILogger logger, string? database);

    /// <summary>About to (re)create the Debezium logical replication publication.</summary>
    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Ensuring logical replication publication for outbox table.")]
    public static partial void EnsuringOutboxPublication(this ILogger logger);
}
