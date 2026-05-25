using MailKit.Net.Smtp;
using MimeKit;
using MimeKit.Text;

namespace OutboxDemo.Notifier.Infrastructure.Smtp;

/// <summary>
/// Sends transactional HTML emails through an SMTP relay.
/// </summary>
internal interface IEmailSender
{
    /// <summary>
    /// Delivers a single HTML email synchronously to the relay.
    /// </summary>
    /// <param name="toAddress">Recipient address (RFC 5322).</param>
    /// <param name="subject">Subject line.</param>
    /// <param name="htmlBody">Body, encoded as HTML.</param>
    /// <param name="cancellationToken">Cancellation token for the SMTP exchange.</param>
    Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken);
}

/// <summary>
/// MailKit-backed implementation of <see cref="IEmailSender"/> bound to the
/// configured <see cref="SmtpOptions"/>.
/// </summary>
internal sealed class EmailSender(IOptions<SmtpOptions> options, ILogger<EmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    /// <inheritdoc />
    public async Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();

        message.From.Add(MailboxAddress.Parse(_options.Sender));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;
        message.Body = new TextPart(TextFormat.Html) { Text = htmlBody };

        using var smtp = new SmtpClient();

        // Mailpit (the local dev relay) does not require TLS; production should opt into StartTls/SslOnConnect.
        await smtp.ConnectAsync(_options.Host, _options.Port, MailKit.Security.SecureSocketOptions.None, cancellationToken);
        await smtp.SendAsync(message, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);

        logger.EmailSent(toAddress);
    }
}
