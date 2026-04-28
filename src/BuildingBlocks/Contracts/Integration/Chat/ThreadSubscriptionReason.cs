namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

/// <summary>
/// Why a user is subscribed to a thread. Higher values represent stronger ownership and
/// take precedence during escalation: a Manual subscription becomes Mentioned/Replied as
/// the user's involvement deepens, and never downgrades.
/// </summary>
public enum ThreadSubscriptionReason
{
    Manual = 0,
    Mentioned = 1,
    Replied = 2,
}
