namespace Urfu.Link.Services.Call.Application;

public sealed class CallOptions
{
    public const string SectionName = "Calls";

    public TimeSpan RingTimeout { get; set; } = TimeSpan.FromSeconds(45);

    public TimeSpan SessionTtl { get; set; } = TimeSpan.FromHours(2);

    public TimeSpan EndedSessionTtl { get; set; } = TimeSpan.FromHours(1);
}
