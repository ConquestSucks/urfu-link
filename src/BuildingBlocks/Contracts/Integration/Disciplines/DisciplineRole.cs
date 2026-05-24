namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

/// <summary>
/// Role of a user within a discipline. Shared across services so that consumers
/// (e.g. ChatService) can interpret enrollment events without redefining the enum.
/// </summary>
public enum DisciplineRole
{
    Teacher = 0,
    Student = 1,
}
