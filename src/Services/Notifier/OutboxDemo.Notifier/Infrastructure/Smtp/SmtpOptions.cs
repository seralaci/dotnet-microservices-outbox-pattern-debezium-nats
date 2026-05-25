using System.ComponentModel.DataAnnotations;

namespace OutboxDemo.Notifier.Infrastructure.Smtp;

/// <summary>
/// Strongly-typed configuration for the SMTP relay used to deliver order confirmation emails.
/// Defaults target the local Mailpit container started by the AppHost.
/// </summary>
internal sealed class SmtpOptions
{
    /// <summary>
    /// Configuration section that binds to this options instance
    /// (e.g. <c>Smtp:Host</c> in <c>appsettings.json</c>).
    /// </summary>
    internal const string SectionName = "Smtp";

    /// <summary>From-address used on every outgoing email.</summary>
    [Required]
    public string Sender { get; init; } = "orders@outboxdemo.local";

    /// <summary>SMTP server hostname.</summary>
    [Required]
    public string Host { get; init; } = "localhost";

    /// <summary>SMTP server port (Mailpit listens on 1025 by default).</summary>
    [Range(1, 65535)]
    public int Port { get; init; } = 1025;
}
