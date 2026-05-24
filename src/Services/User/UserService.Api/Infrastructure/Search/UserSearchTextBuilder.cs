namespace UserService.Api.Infrastructure.Search;

// Конструирует пару (search_text, search_text_translit) — то, что попадает
// в индексируемые колонки. Общий для reconciler-а (KC pageItem) и lazy-upsert-а
// (JWT claims), поэтому вынесен в отдельный singleton.
public sealed class UserSearchTextBuilder(Transliterator transliterator)
{
    public (string SearchText, string SearchTextTranslit) Build(
        string username,
        string? firstName,
        string? lastName,
        string? email)
    {
        var parts = new[] { firstName, lastName, username, email }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => Transliterator.Normalize(s!));

        var searchText = string.Join(' ', parts);
        var translit = transliterator.BuildBidirectional(searchText);

        return (searchText, translit);
    }
}
