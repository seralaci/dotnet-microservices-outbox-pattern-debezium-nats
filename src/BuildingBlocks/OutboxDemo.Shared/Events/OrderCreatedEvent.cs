namespace OutboxDemo.Shared.Events;

/// <summary>
/// Integration event raised when a new purchase order has been accepted.
/// Serialized into the outbox and propagated to downstream services via Debezium + NATS JetStream.
/// </summary>
/// <param name="Id">Identifier of the order aggregate.</param>
/// <param name="FirstName">First name of the customer.</param>
/// <param name="LastName">Last name of the customer.</param>
/// <param name="Email">Email address used for order confirmations.</param>
/// <param name="OrderDate">Moment when the order was placed (UTC).</param>
public sealed record OrderCreatedEvent(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    DateTimeOffset OrderDate);
