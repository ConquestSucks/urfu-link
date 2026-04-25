namespace MediaService.Api.Domain.Enums;

/// <summary>
/// Origin of an access grant. Direct = user-to-user grant (e.g. share link).
/// Conversation / Discipline = grant derived from membership in an external
/// principal; the snapshot of users is pushed via gRPC and kept in sync via
/// Kafka membership events from ChatService / DisciplineService.
/// </summary>
public enum GrantSource
{
    Direct = 0,
    Conversation = 1,
    Discipline = 2,
}
