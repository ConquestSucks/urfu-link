namespace Urfu.Link.Services.Call.Application;

public sealed class LiveKitOptions
{
    public const string SectionName = "LiveKit";

    public string ServerUrl { get; set; } = "ws://localhost:7880";

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    public TimeSpan TokenTtl { get; set; } = TimeSpan.FromMinutes(10);
}
