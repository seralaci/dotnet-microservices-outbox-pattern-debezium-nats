namespace OutboxDemo.Order.Api.Domain;

/// <summary>
/// Transactional outbox row written in the same database transaction as the business change.
/// Debezium tails this table via logical replication and forwards each row to NATS JetStream.
/// </summary>
/// <remarks>
/// The row schema follows the Debezium outbox event router contract
/// (<c>aggregate_type</c>, <c>aggregate_id</c>, <c>type</c>, <c>payload</c>),
/// which means no custom Debezium SMT configuration is required.
/// </remarks>
internal sealed class OutboxMessage
{
    /// <summary>Primary key of the outbox row.</summary>
    public Guid Id { get; private init; } = Guid.NewGuid();

    /// <summary>Logical aggregate that produced the event (used to route to a NATS subject).</summary>
    public string AggregateType { get; private set; } = string.Empty;

    /// <summary>Identifier of the aggregate instance the event refers to.</summary>
    public Guid AggregateId { get; private set; }

    /// <summary>Event type discriminator (see <see cref="Shared.Messaging.EventTypes"/>).</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>Serialized JSON payload published to subscribers.</summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>Server-side timestamp captured when the row is created (UTC).</summary>
    public DateTimeOffset DateOccurred { get; private init; } = DateTimeOffset.UtcNow;

    // Reserved for EF Core. Do not call from application code.
    private OutboxMessage()
    {
    }

    /// <summary>
    /// Builds a validated <see cref="OutboxMessage"/> ready to be enqueued for CDC publication.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when a required string argument is missing.</exception>
    public static OutboxMessage Create(string aggregateType, Guid aggregateId, string type, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return new OutboxMessage
        {
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            Type = type,
            Payload = payload
        };
    }
}
