using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using OutboxDemo.Notifier.Infrastructure.Smtp;

namespace OutboxDemo.Notifier.Events;

/// <summary>
/// Background consumer that pulls Order events from NATS JetStream and triggers
/// a confirmation email for every <c>OrderCreated</c> event observed.
/// </summary>
/// <remarks>
/// The consumer is intentionally tolerant of cold-start ordering: the Debezium Server
/// creates the JetStream stream lazily, so this worker retries consumer creation with
/// exponential backoff until the stream becomes available.
/// </remarks>
internal sealed class OrderCreatedConsumer(
    INatsJSContext jsContext,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    /// <summary>OpenTelemetry activity source name registered by this consumer.</summary>
    internal const string ActivitySourceName = "OutboxDemo.Notifier";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    // Web defaults handle camelCase property names emitted by System.Text.Json on the producer side.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = await WaitForConsumerAsync(stoppingToken);

        logger.SubscribedToJetStream(
            NatsSubjects.StreamName,
            NatsSubjects.Order.Consumer,
            NatsSubjects.Order.Subject);

        await foreach (var msg in consumer.ConsumeAsync<byte[]>(cancellationToken: stoppingToken))
        {
            using var activity = ActivitySource.StartActivity("ProcessOrderCreatedEvent", ActivityKind.Consumer);

            try
            {
                await HandleMessageAsync(msg, stoppingToken);
                await msg.AckAsync(cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                activity?.AddException(ex);
                logger.MessageProcessingFailed(ex, msg.Subject);
                
                // NAK triggers immediate redelivery (subject to MaxDeliver) so the consumer can
                // recover from a transient failure without blocking other messages.
                await msg.NakAsync(cancellationToken: stoppingToken);
            }
        }
    }

    /// <summary>
    /// Loops until the JetStream consumer can be created, backing off exponentially
    /// (capped at 30 seconds) whenever the stream is not yet available.
    /// </summary>
    private async Task<INatsJSConsumer> WaitForConsumerAsync(CancellationToken cancellationToken)
    {
        // The `DebeziumStream` is created by Debezium Server on startup (create-stream=true).
        // Because the Notifier may start before the first CDC event triggers stream creation,
        // we wait for the stream and consumer to be ready using exponential retry.
        var delay = TimeSpan.FromSeconds(1);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                return await CreateConsumerAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.StreamNotReady(NatsSubjects.StreamName, ex.Message, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
                // Exponential backoff with a 30-second ceiling: fast at first, calm under prolonged outages.
                delay = TimeSpan.FromSeconds(Math.Min(30, delay.TotalSeconds * 2));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Consumer setup cancelled.");
    }

    /// <summary>
    /// Creates (or updates) the durable JetStream consumer used by this service.
    /// </summary>
    /// <remarks>
    /// Explicit-ack with a 30s ack-wait and <c>MaxDeliver=5</c> provides at-least-once
    /// semantics while still surfacing poison messages instead of redelivering forever.
    /// </remarks>
    private async Task<INatsJSConsumer> CreateConsumerAsync(CancellationToken cancellationToken)
    {
        var consumerConfig = new ConsumerConfig(NatsSubjects.Order.Consumer)
        {
            DurableName = NatsSubjects.Order.Consumer,
            FilterSubject = NatsSubjects.Order.Subject,
            DeliverPolicy = ConsumerConfigDeliverPolicy.All,
            AckPolicy = ConsumerConfigAckPolicy.Explicit,
            AckWait = TimeSpan.FromSeconds(30),
            MaxDeliver = 5,
            ReplayPolicy = ConsumerConfigReplayPolicy.Instant
        };

        return await jsContext.CreateOrUpdateConsumerAsync(NatsSubjects.StreamName, consumerConfig, cancellationToken);
    }

    /// <summary>
    /// Dispatches a single JetStream message: decodes the payload, runs validation,
    /// and triggers the email notification.
    /// </summary>
    private async Task HandleMessageAsync(INatsJSMsg<byte[]> msg, CancellationToken cancellationToken)
    {
        if (msg.Data is null || msg.Data.Length == 0)
        {
            logger.EmptyMessageReceived(msg.Subject);
            return;
        }

        // Decide the event type based on the subject (`events.order` -> OrderCreated).
        // The Debezium NATS JetStream sink currently does not propagate Kafka Connect headers
        // (`transforms.outbox.table.fields.additional.placement=type:header:eventType`),
        // so the subject -> EventType convention is the reliable source.
        var json = Encoding.UTF8.GetString(msg.Data);

        logger.EventReceived(msg.Subject, json);

        if (!string.Equals(msg.Subject, NatsSubjects.Order.Subject, StringComparison.Ordinal))
        {
            // Defensive: the consumer filter should already exclude unrelated subjects.
            logger.IgnoringMessage(msg.Subject);
            return;
        }

        var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize OrderCreatedEvent payload.");

        // Resolve a scoped IEmailSender for each message — keeps per-message dependencies isolated.
        await using var scope = scopeFactory.CreateAsyncScope();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var body = $"""
            <h1>Dear {orderEvent.FirstName} {orderEvent.LastName}!</h1>
            <p>Your order <strong>{orderEvent.Id}</strong> has started processing.</p>
            <p>Order date: {orderEvent.OrderDate:O}</p>
            """;

        await emailSender.SendAsync(orderEvent.Email, "Thank you for your order!", body, cancellationToken);
    }
}
