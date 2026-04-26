namespace Urfu.Link.Services.Notification.Channels.EmailChannel;

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailEnvelope envelope, CancellationToken cancellationToken);
}

public sealed record EmailEnvelope(string ToAddress, EmailContent Content);

public enum EmailSendOutcome
{
    Success = 0,
    Transient = 1,
    PermanentFailure = 2,
}

public sealed record EmailSendResult(EmailSendOutcome Outcome, string? Error)
{
    public static EmailSendResult Success { get; } = new(EmailSendOutcome.Success, null);

    public static EmailSendResult Transient(string error) => new(EmailSendOutcome.Transient, error);

    public static EmailSendResult Failed(string error) => new(EmailSendOutcome.PermanentFailure, error);
}
