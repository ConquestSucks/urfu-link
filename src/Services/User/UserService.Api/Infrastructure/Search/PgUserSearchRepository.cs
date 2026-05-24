using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using UserService.Api.Infrastructure.Persistence;

namespace UserService.Api.Infrastructure.Search;

public sealed class PgUserSearchRepository(
    UserDbContext dbContext,
    Transliterator transliterator) : IUserSearchRepository
{
    public async Task<IReadOnlyList<UserSearchHit>> SearchAsync(
        string query,
        Guid requesterId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalized = Transliterator.Normalize(query);
        if (normalized.Length == 0)
            return Array.Empty<UserSearchHit>();

        var translit = transliterator.BuildBidirectional(query);

        // Композитный скор — приоритет точному совпадению username/email,
        // затем prefix-match (через LIKE), затем trgm-similarity, затем ts_rank_cd.
        // Trigram match (%) использует GIN-индекс ix_..._search_text_trgm, что делает
        // запрос быстрым даже без selectivity-hint от Postgres.
        var sql = @"
WITH q AS (
    SELECT
        @p_norm::text AS norm,
        @p_translit::text AS norm_translit,
        plainto_tsquery('russian', @p_query) AS tsq
)
SELECT
    p.user_id,
    p.display_name::text AS display_name,
    p.username::text AS username
FROM users.user_search_projection AS p, q
WHERE p.deleted_at_utc IS NULL
  AND p.user_id <> @p_requester
  AND (
        p.search_text % q.norm
     OR p.search_text_translit % q.norm_translit
     OR p.search_vector @@ q.tsq
     OR p.username = q.norm
     OR p.email    = q.norm
  )
ORDER BY (
        CASE WHEN p.username = q.norm THEN 1.0 ELSE 0.0 END
      + CASE WHEN p.email    = q.norm THEN 1.0 ELSE 0.0 END
      + CASE WHEN p.search_text LIKE q.norm || '%' THEN 0.5 ELSE 0.0 END
      + similarity(p.search_text, q.norm) * 0.3
      + ts_rank_cd(p.search_vector, q.tsq) * 0.2
    ) DESC,
    p.display_name ASC
LIMIT @p_limit OFFSET @p_offset;
";

        var conn = dbContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new NpgsqlParameter("@p_norm", NpgsqlDbType.Text) { Value = normalized });
        cmd.Parameters.Add(new NpgsqlParameter("@p_translit", NpgsqlDbType.Text) { Value = translit });
        cmd.Parameters.Add(new NpgsqlParameter("@p_query", NpgsqlDbType.Text) { Value = query });
        cmd.Parameters.Add(new NpgsqlParameter("@p_requester", NpgsqlDbType.Uuid) { Value = requesterId });
        cmd.Parameters.Add(new NpgsqlParameter("@p_limit", NpgsqlDbType.Integer) { Value = limit });
        cmd.Parameters.Add(new NpgsqlParameter("@p_offset", NpgsqlDbType.Integer) { Value = offset });

        var results = new List<UserSearchHit>(limit);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new UserSearchHit(
                UserId: reader.GetGuid(0),
                DisplayName: reader.GetString(1),
                Username: reader.GetString(2)));
        }

        return results;
    }

    public async Task UpsertAsync(UserSearchUpsert item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        // INSERT ... ON CONFLICT DO UPDATE — Postgres-нативный UPSERT.
        // Условие DO UPDATE: новый keycloak_modified_ms >= хранимого, либо
        // запись была soft-deleted (тогда «оживляем» независимо от ms).
        const string sql = @"
INSERT INTO users.user_search_projection (
    user_id, username, first_name, last_name, email, display_name,
    search_text, search_text_translit, keycloak_modified_ms,
    deleted_at_utc, updated_at_utc
) VALUES (
    @p_id, @p_username, @p_first, @p_last, @p_email, @p_display,
    @p_search, @p_translit, @p_ms,
    NULL, now()
)
ON CONFLICT (user_id) DO UPDATE SET
    username = EXCLUDED.username,
    first_name = EXCLUDED.first_name,
    last_name = EXCLUDED.last_name,
    email = EXCLUDED.email,
    display_name = EXCLUDED.display_name,
    search_text = EXCLUDED.search_text,
    search_text_translit = EXCLUDED.search_text_translit,
    keycloak_modified_ms = EXCLUDED.keycloak_modified_ms,
    deleted_at_utc = NULL,
    updated_at_utc = now()
WHERE users.user_search_projection.keycloak_modified_ms <= EXCLUDED.keycloak_modified_ms
   OR users.user_search_projection.deleted_at_utc IS NOT NULL;
";

        var conn = dbContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new NpgsqlParameter("@p_id", NpgsqlDbType.Uuid) { Value = item.UserId });
        cmd.Parameters.Add(new NpgsqlParameter("@p_username", NpgsqlDbType.Citext) { Value = item.Username });
        cmd.Parameters.Add(new NpgsqlParameter("@p_first", NpgsqlDbType.Citext)
        {
            Value = (object?)item.FirstName ?? DBNull.Value,
        });
        cmd.Parameters.Add(new NpgsqlParameter("@p_last", NpgsqlDbType.Citext)
        {
            Value = (object?)item.LastName ?? DBNull.Value,
        });
        cmd.Parameters.Add(new NpgsqlParameter("@p_email", NpgsqlDbType.Citext)
        {
            Value = (object?)item.Email ?? DBNull.Value,
        });
        cmd.Parameters.Add(new NpgsqlParameter("@p_display", NpgsqlDbType.Citext) { Value = item.DisplayName });
        cmd.Parameters.Add(new NpgsqlParameter("@p_search", NpgsqlDbType.Text) { Value = item.SearchText });
        cmd.Parameters.Add(new NpgsqlParameter("@p_translit", NpgsqlDbType.Text) { Value = item.SearchTextTranslit });
        cmd.Parameters.Add(new NpgsqlParameter("@p_ms", NpgsqlDbType.Bigint) { Value = item.KeycloakModifiedMs });

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> SoftDeleteMissingAsync(
        IReadOnlyCollection<Guid> existingInKeycloak,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(existingInKeycloak);

        // UPDATE с антиджойном против пришедшего snapshot-а. Используем uuid[],
        // чтобы передать список одним параметром (избегаем IN-листа с тысячами id).
        const string sql = @"
UPDATE users.user_search_projection
SET deleted_at_utc = now()
WHERE deleted_at_utc IS NULL
  AND user_id <> ALL(@p_ids);
";

        var conn = dbContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new NpgsqlParameter("@p_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = existingInKeycloak.ToArray(),
        });

        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DateTimeOffset?> GetUpdatedAtUtcAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.UserSearchProjections
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.DeletedAtUtc == null)
            .Select(x => (DateTimeOffset?)x.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, UserSearchSummary>> BatchGetSummariesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
            return new Dictionary<Guid, UserSearchSummary>();

        var distinct = userIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (distinct.Length == 0)
            return new Dictionary<Guid, UserSearchSummary>();

        var rows = await dbContext.UserSearchProjections
            .AsNoTracking()
            .Where(x => distinct.Contains(x.UserId) && x.DeletedAtUtc == null)
            .Select(x => new
            {
                x.UserId,
                x.DisplayName,
                x.Email,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(
            r => r.UserId,
            r => new UserSearchSummary(
                r.UserId,
                r.DisplayName,
                r.Email ?? string.Empty));
    }
}
