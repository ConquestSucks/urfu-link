using DisciplineService.Api.Domain.Exceptions;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

namespace DisciplineService.Api.Domain.Aggregates;

public sealed class Discipline
{
    public const string DefaultSubgroupName = "Подгруппа 1";

    private readonly List<IIntegrationEvent> _domainEvents = [];
    private readonly List<Enrollment> _enrollments = [];
    private readonly List<DisciplineSubgroup> _subgroups = [];

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

    public IReadOnlyList<DisciplineSubgroup> Subgroups => _subgroups;

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
            subgroupId: null,
            initiatedBy == Guid.Empty ? ownerTeacherId : initiatedBy,
            now);
        discipline._enrollments.Add(ownerEnrollment);
        var defaultSubgroup = DisciplineSubgroup.Create(discipline.Id, DefaultSubgroupName, now);
        discipline._subgroups.Add(defaultSubgroup);

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
        discipline._domainEvents.Add(new DisciplineSubgroupCreatedEvent(
            discipline.Id,
            defaultSubgroup.Id,
            discipline.Title,
            discipline.CoverAssetId,
            defaultSubgroup.Name,
            [ownerTeacherId],
            []));

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

    public DisciplineSubgroup CreateSubgroup(string name)
    {
        EnsureNotArchived();
        var now = DateTimeOffset.UtcNow;
        var subgroup = DisciplineSubgroup.Create(Id, name, now);
        _subgroups.Add(subgroup);
        Touch();
        _domainEvents.Add(new DisciplineSubgroupCreatedEvent(
            Id,
            subgroup.Id,
            Title,
            CoverAssetId,
            subgroup.Name,
            _enrollments.Where(e => e.Role == DisciplineRole.Teacher).Select(e => e.UserId).ToList(),
            []));
        return subgroup;
    }

    public void RenameSubgroup(Guid subgroupId, string name)
    {
        EnsureNotArchived();
        var subgroup = GetActiveSubgroupOrThrow(subgroupId);
        subgroup.Rename(name);
        Touch();
        _domainEvents.Add(new DisciplineSubgroupUpdatedEvent(Id, subgroup.Id, subgroup.Name));
    }

    public void ArchiveSubgroup(Guid subgroupId)
    {
        EnsureNotArchived();
        var subgroup = GetActiveSubgroupOrThrow(subgroupId);
        if (_enrollments.Any(e => e.Role == DisciplineRole.Student && e.SubgroupId == subgroupId))
        {
            throw new DisciplineSubgroupNotEmptyException(Id, subgroupId);
        }

        subgroup.Archive();
        Touch();
        _domainEvents.Add(new DisciplineSubgroupArchivedEvent(Id, subgroup.Id));
    }

    public Enrollment Enroll(Guid userId, DisciplineRole role, Guid? subgroupId, Guid enrolledBy)
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

        ValidateRoleSubgroup(userId, role, subgroupId);
        var enrollment = Enrollment.Create(Id, userId, role, subgroupId, enrolledBy, DateTimeOffset.UtcNow);
        _enrollments.Add(enrollment);
        Touch();
        _domainEvents.Add(new UserEnrolledEvent(Id, userId, role, enrolledBy, subgroupId));
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

        var role = enrollment.Role;
        var subgroupId = enrollment.SubgroupId;
        _enrollments.Remove(enrollment);
        Touch();
        _domainEvents.Add(new UserUnenrolledEvent(Id, userId, role, subgroupId));
    }

    public void ChangeRole(Guid userId, DisciplineRole newRole, Guid? subgroupId)
    {
        EnsureNotArchived();
        var enrollment = _enrollments.FirstOrDefault(e => e.UserId == userId)
            ?? throw new EnrollmentNotFoundException(Id, userId);

        if (userId == OwnerTeacherId && newRole != DisciplineRole.Teacher)
        {
            throw new OwnerRoleChangeException(Id, userId);
        }

        ValidateRoleSubgroup(userId, newRole, subgroupId);

        if (enrollment.Role == newRole)
        {
            if (newRole == DisciplineRole.Student && subgroupId.HasValue && enrollment.SubgroupId != subgroupId)
            {
                AssignStudentSubgroup(userId, subgroupId.Value);
            }

            return;
        }

        if (enrollment.Role == DisciplineRole.Teacher
            && newRole != DisciplineRole.Teacher
            && _enrollments.Count(e => e.Role == DisciplineRole.Teacher) <= 1)
        {
            throw new LastTeacherRemovalException(Id, userId);
        }

        var oldRole = enrollment.Role;
        var oldSubgroupId = enrollment.SubgroupId;
        enrollment.SetRole(newRole, newRole == DisciplineRole.Student ? subgroupId : null);
        Touch();
        _domainEvents.Add(new EnrollmentRoleChangedEvent(
            Id,
            userId,
            oldRole,
            newRole,
            oldSubgroupId,
            enrollment.SubgroupId));
    }

    public void AssignStudentSubgroup(Guid userId, Guid subgroupId)
    {
        EnsureNotArchived();
        var enrollment = _enrollments.FirstOrDefault(e => e.UserId == userId)
            ?? throw new EnrollmentNotFoundException(Id, userId);
        if (enrollment.Role != DisciplineRole.Student)
        {
            throw new TeacherSubgroupNotAllowedException(Id, userId);
        }

        GetActiveSubgroupOrThrow(subgroupId);
        if (enrollment.SubgroupId == subgroupId)
        {
            return;
        }

        var oldSubgroupId = enrollment.SubgroupId;
        enrollment.SetSubgroup(subgroupId);
        Touch();
        _domainEvents.Add(new EnrollmentSubgroupChangedEvent(Id, userId, oldSubgroupId, subgroupId));
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

    private DisciplineSubgroup GetActiveSubgroupOrThrow(Guid subgroupId)
    {
        var subgroup = _subgroups.FirstOrDefault(s => s.Id == subgroupId && !s.IsArchived);
        return subgroup ?? throw new DisciplineSubgroupNotFoundException(Id, subgroupId);
    }

    private void ValidateRoleSubgroup(Guid userId, DisciplineRole role, Guid? subgroupId)
    {
        if (role == DisciplineRole.Teacher)
        {
            if (subgroupId.HasValue)
            {
                throw new TeacherSubgroupNotAllowedException(Id, userId);
            }

            return;
        }

        if (!subgroupId.HasValue)
        {
            throw new StudentSubgroupRequiredException(Id, userId);
        }

        GetActiveSubgroupOrThrow(subgroupId.Value);
    }

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
