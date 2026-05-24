using DisciplineService.Api.Domain.Exceptions;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

namespace DisciplineService.Api.Domain.Aggregates;

public sealed class Discipline
{
    private readonly List<IIntegrationEvent> _domainEvents = [];
    private readonly List<Enrollment> _enrollments = [];

    public Guid Id { get; private set; }

    public string Code { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public string Semester { get; private set; } = null!;

    public Guid OwnerTeacherId { get; private set; }

    public Guid? CoverAssetId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public bool IsArchived => ArchivedAtUtc.HasValue;

    public IReadOnlyList<Enrollment> Enrollments => _enrollments;

    public IReadOnlyList<IIntegrationEvent> DomainEvents => _domainEvents;

    private Discipline()
    {
    }

    public static Discipline CreateNew(
        string code,
        string title,
        string? description,
        string semester,
        Guid ownerTeacherId,
        Guid? coverAssetId,
        Guid initiatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(semester);
        if (ownerTeacherId == Guid.Empty)
        {
            throw new ArgumentException("Owner teacher id is required.", nameof(ownerTeacherId));
        }

        var now = DateTimeOffset.UtcNow;
        var discipline = new Discipline
        {
            Id = Guid.NewGuid(),
            Code = code.Trim(),
            Title = title.Trim(),
            Description = NormalizeOptionalText(description),
            Semester = semester.Trim(),
            OwnerTeacherId = ownerTeacherId,
            CoverAssetId = coverAssetId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var ownerEnrollment = Enrollment.Create(
            discipline.Id,
            ownerTeacherId,
            DisciplineRole.Teacher,
            initiatedBy == Guid.Empty ? ownerTeacherId : initiatedBy,
            now);
        discipline._enrollments.Add(ownerEnrollment);

        discipline._domainEvents.Add(new DisciplineCreatedEvent(
            discipline.Id,
            discipline.Code,
            discipline.Title,
            discipline.Description,
            discipline.Semester,
            discipline.OwnerTeacherId,
            discipline.CoverAssetId));
        discipline._domainEvents.Add(new UserEnrolledEvent(
            discipline.Id,
            ownerTeacherId,
            DisciplineRole.Teacher,
            ownerEnrollment.EnrolledBy));

        return discipline;
    }

    public void Update(
        string code,
        string title,
        string? description,
        string semester,
        Guid? coverAssetId)
    {
        EnsureNotArchived();
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(semester);

        Code = code.Trim();
        Title = title.Trim();
        Description = NormalizeOptionalText(description);
        Semester = semester.Trim();
        CoverAssetId = coverAssetId;
        Touch();

        _domainEvents.Add(new DisciplineUpdatedEvent(
            Id,
            Code,
            Title,
            Description,
            Semester,
            OwnerTeacherId,
            CoverAssetId));
    }

    public void Archive()
    {
        if (IsArchived)
        {
            return;
        }

        ArchivedAtUtc = DateTimeOffset.UtcNow;
        Touch();
        _domainEvents.Add(new DisciplineDeletedEvent(Id));
    }

    public Enrollment Enroll(Guid userId, DisciplineRole role, Guid enrolledBy)
    {
        EnsureNotArchived();
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (_enrollments.Any(e => e.UserId == userId))
        {
            throw new EnrollmentExistsException(Id, userId);
        }

        var enrollment = Enrollment.Create(Id, userId, role, enrolledBy, DateTimeOffset.UtcNow);
        _enrollments.Add(enrollment);
        Touch();
        _domainEvents.Add(new UserEnrolledEvent(Id, userId, role, enrolledBy));
        return enrollment;
    }

    public void Unenroll(Guid userId)
    {
        EnsureNotArchived();
        var enrollment = _enrollments.FirstOrDefault(e => e.UserId == userId)
            ?? throw new EnrollmentNotFoundException(Id, userId);

        if (userId == OwnerTeacherId)
        {
            throw new OwnerEnrollmentRemovalException(Id, userId);
        }

        if (enrollment.Role == DisciplineRole.Teacher
            && _enrollments.Count(e => e.Role == DisciplineRole.Teacher) <= 1)
        {
            throw new LastTeacherRemovalException(Id, userId);
        }

        _enrollments.Remove(enrollment);
        Touch();
        _domainEvents.Add(new UserUnenrolledEvent(Id, userId));
    }

    public void ChangeRole(Guid userId, DisciplineRole newRole)
    {
        EnsureNotArchived();
        var enrollment = _enrollments.FirstOrDefault(e => e.UserId == userId)
            ?? throw new EnrollmentNotFoundException(Id, userId);

        if (userId == OwnerTeacherId && newRole != DisciplineRole.Teacher)
        {
            throw new OwnerRoleChangeException(Id, userId);
        }

        if (enrollment.Role == newRole)
        {
            return;
        }

        if (enrollment.Role == DisciplineRole.Teacher
            && newRole != DisciplineRole.Teacher
            && _enrollments.Count(e => e.Role == DisciplineRole.Teacher) <= 1)
        {
            throw new LastTeacherRemovalException(Id, userId);
        }

        var oldRole = enrollment.Role;
        enrollment.SetRole(newRole);
        Touch();
        _domainEvents.Add(new EnrollmentRoleChangedEvent(Id, userId, oldRole, newRole));
    }

    public bool IsTeacher(Guid userId)
        => _enrollments.Any(e => e.UserId == userId && e.Role == DisciplineRole.Teacher);

    public bool HasMember(Guid userId)
        => _enrollments.Any(e => e.UserId == userId);

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void Touch() => UpdatedAtUtc = DateTimeOffset.UtcNow;

    private void EnsureNotArchived()
    {
        if (IsArchived)
        {
            throw new DisciplineArchivedException(Id);
        }
    }

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
