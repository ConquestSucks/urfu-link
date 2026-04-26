using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Urfu.Link.Services.Notification.Channels.EmailChannel;

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<EmailSendResult> SendAsync(EmailEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var settings = options.Value;

        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromDisplayName, settings.From));
        message.To.Add(MailboxAddress.Parse(envelope.ToAddress));
        message.Subject = envelope.Content.Subject;

        var body = new BodyBuilder
        {
            HtmlBody = envelope.Content.Html,
            TextBody = envelope.Content.Plain,
        };
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            var secureOption = settings.EnableStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(settings.Host, settings.Port, secureOption, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken).ConfigureAwait(false);
            }

            await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
            return EmailSendResult.Success;
        }
        catch (SmtpCommandException ex) when (ex.StatusCode is SmtpStatusCode.AuthenticationRequired or SmtpStatusCode.MailboxBusy)
        {
            logger.LogWarning(ex, "SMTP transient failure delivering to {To}", envelope.ToAddress);
            return EmailSendResult.Transient(ex.Message);
        }
        catch (SmtpCommandException ex)
        {
            logger.LogError(ex, "SMTP permanent failure delivering to {To}", envelope.ToAddress);
            return EmailSendResult.Failed(ex.Message);
        }
        catch (SmtpProtocolException ex)
        {
            return EmailSendResult.Transient(ex.Message);
        }
    }
}

public sealed class FakeEmailSender(ILogger<FakeEmailSender> logger) : IEmailSender
{
    public Task<EmailSendResult> SendAsync(EmailEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        _ = cancellationToken;
        logger.LogInformation(
            "[FakeEmail] to={To} subject={Subject}\nHTML:\n{Html}\nPLAIN:\n{Plain}",
            envelope.ToAddress,
            envelope.Content.Subject,
            envelope.Content.Html,
            envelope.Content.Plain);
        return Task.FromResult(EmailSendResult.Success);
    }
}
