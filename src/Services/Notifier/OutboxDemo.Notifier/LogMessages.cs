namespace OutboxDemo.Notifier;

/// <summary>
/// Strongly-typed, source-generated logger extensions for the Notifier service.
/// Using <see cref="LoggerMessageAttribute"/> avoids boxing and string formatting on the hot path.
/// </summary>
internal static partial class LogMessages
{
    /// <summary>The consumer successfully attached to a JetStream stream.</summary>
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Subscribed to JetStream stream {Stream} with consumer {Consumer} for subject {Subject}.")]
    public static partial void SubscribedToJetStream(this ILogger logger, string stream, string consumer, string subject);

    /// <summary>Message processing threw; the message will be NAK'd for redelivery.</summary>
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Error,
        Message = "Failed to process message on subject {Subject}. Negative-acking for redelivery.")]
    public static partial void MessageProcessingFailed(this ILogger logger, Exception exception, string subject);

    /// <summary>Stream creation by Debezium has not happened yet; the consumer is retrying.</summary>
    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Stream {Stream} not ready yet ({Reason}); retrying in {DelaySeconds}s.")]
    public static partial void StreamNotReady(this ILogger logger, string stream, string reason, double delaySeconds);

    /// <summary>An empty payload was delivered; nothing to process.</summary>
    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "Received empty message on {Subject}.")]
    public static partial void EmptyMessageReceived(this ILogger logger, string subject);

    /// <summary>Trace-level dump of the received payload to aid local debugging.</summary>
    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Debug,
        Message = "Received event on subject {Subject}: {Payload}")]
    public static partial void EventReceived(this ILogger logger, string subject, string payload);

    /// <summary>Message arrived on a subject the consumer is not interested in.</summary>
    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "Ignoring message on subject {Subject}.")]
    public static partial void IgnoringMessage(this ILogger logger, string subject);

    /// <summary>Confirmation email has been handed over to the SMTP server.</summary>
    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Information,
        Message = "Email sent to {Recipient}.")]
    public static partial void EmailSent(this ILogger logger, string recipient);
}
