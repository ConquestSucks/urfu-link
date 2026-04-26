namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Call;

public enum CallEndReason
{
    Completed = 1,
    DeclinedByCallee = 2,
    CancelledByCaller = 3,
    Missed = 4,
    NoAnswer = 5,
    Failed = 6,
}
