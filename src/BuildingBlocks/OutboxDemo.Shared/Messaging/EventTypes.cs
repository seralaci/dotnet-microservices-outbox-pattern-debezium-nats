namespace OutboxDemo.Shared.Messaging;

/// <summary>
/// Canonical event type identifiers stored in the <c>type</c> column of every outbox row.
/// Consumers use these values to dispatch a payload to the correct handler.
/// </summary>
public static class EventTypes
{
    /// <summary>Event type for <see cref="Events.OrderCreatedEvent"/>.</summary>
    public const string OrderCreated = "OrderCreated";
}
