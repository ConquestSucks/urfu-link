namespace DisciplineService.Api.Domain.Exceptions;

public sealed class DisciplineNotFoundException : InvalidOperationException
{
    public DisciplineNotFoundException()
    {
    }

    public DisciplineNotFoundException(string message)
        : base(message)
    {
    }

    public DisciplineNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public static DisciplineNotFoundException For(Guid disciplineId)
        => new($"Discipline '{disciplineId}' was not found.")
        {
            DisciplineId = disciplineId,
        };

    public Guid DisciplineId { get; private init; }
}

public sealed class DisciplineArchivedException : InvalidOperationException
{
    public DisciplineArchivedException()
    {
    }

    public DisciplineArchivedException(string message)
        : base(message)
    {
    }

    public DisciplineArchivedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public DisciplineArchivedException(Guid disciplineId)
        : base($"Discipline '{disciplineId}' is archived; modifications are not allowed.")
    {
        DisciplineId = disciplineId;
    }

    public Guid DisciplineId { get; }
}

public sealed class EnrollmentNotFoundException : InvalidOperationException
{
    public EnrollmentNotFoundException()
    {
    }

    public EnrollmentNotFoundException(string message)
        : base(message)
    {
    }

    public EnrollmentNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public EnrollmentNotFoundException(Guid disciplineId, Guid userId)
        : base($"User '{userId}' is not enrolled in discipline '{disciplineId}'.")
    {
        DisciplineId = disciplineId;
        UserId = userId;
    }

    public Guid DisciplineId { get; }

    public Guid UserId { get; }
}

public sealed class EnrollmentExistsException : InvalidOperationException
{
    public EnrollmentExistsException()
    {
    }

    public EnrollmentExistsException(string message)
        : base(message)
    {
    }

    public EnrollmentExistsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public EnrollmentExistsException(Guid disciplineId, Guid userId)
        : base($"User '{userId}' is already enrolled in discipline '{disciplineId}'.")
    {
        DisciplineId = disciplineId;
        UserId = userId;
    }

    public Guid DisciplineId { get; }

    public Guid UserId { get; }
}

public sealed class LastTeacherRemovalException : InvalidOperationException
{
    public LastTeacherRemovalException()
    {
    }

    public LastTeacherRemovalException(string message)
        : base(message)
    {
    }

    public LastTeacherRemovalException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public LastTeacherRemovalException(Guid disciplineId, Guid userId)
        : base($"User '{userId}' is the last teacher in discipline '{disciplineId}'; assign another teacher first.")
    {
        DisciplineId = disciplineId;
        UserId = userId;
    }

    public Guid DisciplineId { get; }

    public Guid UserId { get; }
}

public sealed class OwnerEnrollmentRemovalException : InvalidOperationException
{
    public OwnerEnrollmentRemovalException()
    {
    }

    public OwnerEnrollmentRemovalException(string message)
        : base(message)
    {
    }

    public OwnerEnrollmentRemovalException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public OwnerEnrollmentRemovalException(Guid disciplineId, Guid ownerTeacherId)
        : base($"Cannot unenroll owner teacher '{ownerTeacherId}' of discipline '{disciplineId}'; transfer ownership first.")
    {
        DisciplineId = disciplineId;
        OwnerTeacherId = ownerTeacherId;
    }

    public Guid DisciplineId { get; }

    public Guid OwnerTeacherId { get; }
}

public sealed class OwnerRoleChangeException : InvalidOperationException
{
    public OwnerRoleChangeException()
    {
    }

    public OwnerRoleChangeException(string message)
        : base(message)
    {
    }

    public OwnerRoleChangeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public OwnerRoleChangeException(Guid disciplineId, Guid ownerTeacherId)
        : base($"Cannot change role of owner teacher '{ownerTeacherId}' of discipline '{disciplineId}'.")
    {
        DisciplineId = disciplineId;
        OwnerTeacherId = ownerTeacherId;
    }

    public Guid DisciplineId { get; }

    public Guid OwnerTeacherId { get; }
}

public sealed class DisciplineSubgroupNotFoundException : InvalidOperationException
{
    public DisciplineSubgroupNotFoundException()
    {
    }

    public DisciplineSubgroupNotFoundException(string message)
        : base(message)
    {
    }

    public DisciplineSubgroupNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public DisciplineSubgroupNotFoundException(Guid disciplineId, Guid subgroupId)
        : base($"Subgroup '{subgroupId}' was not found in discipline '{disciplineId}'.")
    {
        DisciplineId = disciplineId;
        SubgroupId = subgroupId;
    }

    public Guid DisciplineId { get; }

    public Guid SubgroupId { get; }
}

public sealed class DisciplineSubgroupNotEmptyException : InvalidOperationException
{
    public DisciplineSubgroupNotEmptyException()
    {
    }

    public DisciplineSubgroupNotEmptyException(string message)
        : base(message)
    {
    }

    public DisciplineSubgroupNotEmptyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public DisciplineSubgroupNotEmptyException(Guid disciplineId, Guid subgroupId)
        : base($"Subgroup '{subgroupId}' in discipline '{disciplineId}' still has students; move or remove them first.")
    {
        DisciplineId = disciplineId;
        SubgroupId = subgroupId;
    }

    public Guid DisciplineId { get; }

    public Guid SubgroupId { get; }
}

public sealed class StudentSubgroupRequiredException : InvalidOperationException
{
    public StudentSubgroupRequiredException()
    {
    }

    public StudentSubgroupRequiredException(string message)
        : base(message)
    {
    }

    public StudentSubgroupRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public StudentSubgroupRequiredException(Guid disciplineId, Guid userId)
        : base($"Student '{userId}' in discipline '{disciplineId}' must belong to exactly one active subgroup.")
    {
        DisciplineId = disciplineId;
        UserId = userId;
    }

    public Guid DisciplineId { get; }

    public Guid UserId { get; }
}

public sealed class TeacherSubgroupNotAllowedException : InvalidOperationException
{
    public TeacherSubgroupNotAllowedException()
    {
    }

    public TeacherSubgroupNotAllowedException(string message)
        : base(message)
    {
    }

    public TeacherSubgroupNotAllowedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public TeacherSubgroupNotAllowedException(Guid disciplineId, Guid userId)
        : base($"Teacher '{userId}' in discipline '{disciplineId}' cannot be assigned to a student subgroup.")
    {
        DisciplineId = disciplineId;
        UserId = userId;
    }

    public Guid DisciplineId { get; }

    public Guid UserId { get; }
}
