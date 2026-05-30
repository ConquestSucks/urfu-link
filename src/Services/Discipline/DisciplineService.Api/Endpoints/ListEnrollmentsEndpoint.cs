using System.Buffers.Text;
using System.Globalization;
using System.Text;
using DisciplineService.Api.Application.Authorization;
using DisciplineService.Api.Application.Contracts.Responses;
using DisciplineService.Api.Domain.Interfaces;
using FastEndpoints;

namespace DisciplineService.Api.Endpoints;

public sealed class ListEnrollmentsRouteRequest
{
    public Guid Id { get; set; }

    public string? Cursor { get; set; }

    public int Limit { get; set; } = 100;
}

public sealed class ListEnrollmentsResponse
{
    public IReadOnlyList<EnrollmentResponse> Items { get; init; } = [];

    public string? NextCursor { get; init; }
}

public sealed class ListEnrollmentsEndpoint(
    IDisciplineRepository disciplineRepository,
    IEnrollmentRepository enrollmentRepository,
    DisciplineAuthorizationService authorization)
    : Endpoint<ListEnrollmentsRouteRequest, ListEnrollmentsResponse>
{
    private const int MaxLimit = 200;
    private const int DefaultLimit = 100;

    public override void Configure()
    {
        Get("{id:guid}/enrollments");
        Group<DisciplinesGroup>();
        Summary(s => s.Summary = "Paginated list of enrollments. Cursor is opaque, omit for page 1.");
    }

    public override async Task HandleAsync(ListEnrollmentsRouteRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        var discipline = await disciplineRepository.GetByIdAsync(req.Id, ct).ConfigureAwait(false);
        if (discipline is null)
        {
            await Send.NotFoundAsync(ct).ConfigureAwait(false);
            return;
        }

        if (!authorization.CanRead(User, discipline))
        {
            await Send.ForbiddenAsync(ct).ConfigureAwait(false);
            return;
        }

        if (!EnrollmentCursorCodec.TryDecode(req.Cursor, out var cursor))
        {
            AddError(r => r.Cursor, "Invalid cursor.");
            await Send.ErrorsAsync(cancellation: ct).ConfigureAwait(false);
            return;
        }

        var limit = NormalizeLimit(req.Limit);
        var page = await enrollmentRepository
            .ListEnrollmentsAsync(req.Id, cursor, limit, ct)
            .ConfigureAwait(false);

        await Send.OkAsync(
            new ListEnrollmentsResponse
            {
                Items = page.Items
                    .Select(e => new EnrollmentResponse(e.UserId, e.Role, e.SubgroupId, e.EnrolledAtUtc, e.EnrolledBy))
                    .ToList(),
                NextCursor = EnrollmentCursorCodec.Encode(page.NextCursor),
            },
            cancellation: ct).ConfigureAwait(false);
    }

    private static int NormalizeLimit(int limit) => limit switch
    {
        <= 0 => DefaultLimit,
        > MaxLimit => MaxLimit,
        _ => limit,
    };
}

internal static class EnrollmentCursorCodec
{
    private const char Separator = '|';

    public static string? Encode(EnrollmentCursor? cursor)
    {
        if (cursor is null)
        {
            return null;
        }

        var raw = string.Create(
            CultureInfo.InvariantCulture,
            $"{cursor.EnrolledAtUtc.UtcTicks}{Separator}{cursor.EnrollmentId:N}");
        var bytes = Encoding.UTF8.GetBytes(raw);
        return Convert.ToBase64String(bytes);
    }

    public static bool TryDecode(string? encoded, out EnrollmentCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrEmpty(encoded))
        {
            return true;
        }

        Span<byte> buffer = stackalloc byte[256];
        if (!Convert.TryFromBase64String(encoded, buffer, out var written))
        {
            return false;
        }

        var raw = Encoding.UTF8.GetString(buffer[..written]);
        var parts = raw.Split(Separator);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
        {
            return false;
        }

        if (!Guid.TryParseExact(parts[1], "N", out var id))
        {
            return false;
        }

        cursor = new EnrollmentCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        return true;
    }
}
