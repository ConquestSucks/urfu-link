using System.Globalization;
using System.Text;

namespace UserService.Api.Infrastructure.Search;

// Двунаправленная транслитерация ru ↔ en для покрытия раскладочных опечаток в поиске.
// При UPSERT мы храним конкатенацию исходного и инвертированного представления,
// поэтому запрос «kirill» найдёт пользователя «Кирилл», а «иван» — «Ivan».
//
// Это not-a-library: нам нужны не строгие правила ГОСТ/BGN/ICAO, а толерантная карта,
// которая ловит наиболее частые написания (например, «ж» → «zh», «ч» → «ch»).
public sealed class Transliterator
{
    // Длинные триграммы/диграммы должны идти раньше односимвольных, иначе
    // «sch» сначала превратится в «с»+«х». Сохраняем порядок через массив.
    private static readonly (string From, string To)[] LatinToCyrillicPairs =
    [
        ("shch", "щ"),
        ("yo", "ё"),
        ("zh", "ж"),
        ("kh", "х"),
        ("ts", "ц"),
        ("ch", "ч"),
        ("sh", "ш"),
        ("yu", "ю"),
        ("ya", "я"),
        ("iy", "ий"),
        ("ye", "е"),
        ("ee", "и"),
        ("a", "а"),
        ("b", "б"),
        ("v", "в"),
        ("g", "г"),
        ("d", "д"),
        ("e", "е"),
        ("z", "з"),
        ("i", "и"),
        ("y", "й"),
        ("k", "к"),
        ("l", "л"),
        ("m", "м"),
        ("n", "н"),
        ("o", "о"),
        ("p", "п"),
        ("r", "р"),
        ("s", "с"),
        ("t", "т"),
        ("u", "у"),
        ("f", "ф"),
        ("h", "х"),
        ("c", "к"),
        ("j", "дж"),
        ("q", "к"),
        ("w", "в"),
        ("x", "кс"),
    ];

    private static readonly Dictionary<char, string> CyrillicToLatin = new()
    {
        ['а'] = "a",
        ['б'] = "b",
        ['в'] = "v",
        ['г'] = "g",
        ['д'] = "d",
        ['е'] = "e",
        ['ё'] = "yo",
        ['ж'] = "zh",
        ['з'] = "z",
        ['и'] = "i",
        ['й'] = "y",
        ['к'] = "k",
        ['л'] = "l",
        ['м'] = "m",
        ['н'] = "n",
        ['о'] = "o",
        ['п'] = "p",
        ['р'] = "r",
        ['с'] = "s",
        ['т'] = "t",
        ['у'] = "u",
        ['ф'] = "f",
        ['х'] = "kh",
        ['ц'] = "ts",
        ['ч'] = "ch",
        ['ш'] = "sh",
        ['щ'] = "shch",
        ['ъ'] = "",
        ['ы'] = "y",
        ['ь'] = "",
        ['э'] = "e",
        ['ю'] = "yu",
        ['я'] = "ya",
    };

    public string LatinToCyrillic(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // CA1308: lowercase нужен для денормализованного представления поискового индекса,
        // не для сравнения. Кейс-инсенситивность достигается в БД через citext + unaccent.
#pragma warning disable CA1308
        var lower = input.ToLowerInvariant();
#pragma warning restore CA1308
        foreach (var (from, to) in LatinToCyrillicPairs)
        {
            lower = lower.Replace(from, to, StringComparison.Ordinal);
        }

        return lower;
    }

    public string CyrillicToLatinString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var sb = new StringBuilder(input.Length * 2);
#pragma warning disable CA1308
        foreach (var ch in input.ToLowerInvariant())
#pragma warning restore CA1308
        {
            if (CyrillicToLatin.TryGetValue(ch, out var mapped))
            {
                sb.Append(mapped);
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    // Возвращает «оригинал в нижнем регистре» + " " + «другая раскладка».
    // Если строка целиком латиница — добавим кириллический эквивалент, и наоборот.
    // Если строка смешанная — добавляем оба перевода, чтобы не упустить ни один кейс.
    public string BuildBidirectional(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var normalized = Normalize(input);
        var toCyrillic = LatinToCyrillic(normalized);
        var toLatin = CyrillicToLatinString(normalized);

        var sb = new StringBuilder(normalized.Length * 3);
        sb.Append(normalized);

        if (!string.Equals(toCyrillic, normalized, StringComparison.Ordinal))
        {
            sb.Append(' ').Append(toCyrillic);
        }

        if (!string.Equals(toLatin, normalized, StringComparison.Ordinal)
            && !string.Equals(toLatin, toCyrillic, StringComparison.Ordinal))
        {
            sb.Append(' ').Append(toLatin);
        }

        return sb.ToString();
    }

    // Нижний регистр + раздавим диакритику в base-форму («Ёлкин» → «ёлкин» здесь
    // не трогаем, потому что русская «ё» — отдельная буква; unaccent в БД делает
    // та же работа для латинских диакритиков. Этот метод просто нормализует case.)
    public static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // FormD разделяет составные символы на base + diacritic, отбрасываем NonSpacingMark.
        var formD = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

#pragma warning disable CA1308
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
#pragma warning restore CA1308
    }
}
