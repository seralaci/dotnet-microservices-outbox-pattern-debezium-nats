namespace OutboxDemo.Shared.Messaging;

/// <summary>
/// Centralized catalog of NATS JetStream stream, subject, and consumer names
/// used across the producer (Debezium Server) and the consuming services.
/// </summary>
public static class NatsSubjects
{
    /// <summary>
    /// Name of the JetStream stream that captures outbox events.
    /// </summary>
    /// <remarks>
    /// The stream is created by Debezium Server on startup (default name: <c>DebeziumStream</c>).
    /// The migration service intentionally does NOT pre-create the stream: the Debezium
    /// <c>NatsJetStreamChangeConsumer</c> calls the CreateStream API itself, and two streams
    /// cannot coexist on the same subject hierarchy.
    /// </remarks>
    public const string StreamName = "DebeziumStream";

    /// <summary>
    /// Subject wildcard that the JetStream stream binds to.
    /// </summary>
    /// <remarks>
    /// A prefixed namespace (<c>events.&gt;</c>) is required so the stream filter does NOT
    /// overlap with the JetStream internal <c>$JS.&gt;</c> API hierarchy; otherwise the stream
    /// cannot be created in acked mode.
    /// </remarks>
    public const string SubjectFilter = "events.>";

    /// <summary>
    /// Subjects, aggregate types, and consumer names for the Order aggregate.
    /// </summary>
    public static class Order
    {
        /// <summary>
        /// Logical aggregate type stamped on every outbox row. Matches the Debezium
        /// <c>RouteByField</c> transform used to derive the destination subject.
        /// </summary>
        public const string AggregateType = "order";

        /// <summary>NATS subject that carries Order domain events.</summary>
        public const string Subject = "events.order";

        /// <summary>Durable consumer name owned by the Notifier service.</summary>
        public const string Consumer = "notifier-order-events";
    }
}
