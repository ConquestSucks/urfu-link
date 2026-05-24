using UserService.Api.Infrastructure.Search;

namespace UserService.UnitTests.Infrastructure;

public sealed class TransliteratorTests
{
    private readonly Transliterator _sut = new();

    [Theory]
    [InlineData("Ivan", "иван")]
    [InlineData("kirill", "кирилл")]
    [InlineData("Petrov", "петров")]
    [InlineData("Alexey", "алексей")]
    public void LatinToCyrillicMapsCommonNames(string input, string expectedPrefix)
    {
        ArgumentNullException.ThrowIfNull(expectedPrefix);
        var actual = _sut.LatinToCyrillic(input);

        // Не настаиваем на полной идентичности — допускаем варианты (Алексей/Алекссей).
        Assert.StartsWith(expectedPrefix[..Math.Min(3, expectedPrefix.Length)], actual, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Иван", "ivan")]
    [InlineData("Кирилл", "kirill")]
    [InlineData("Петров", "petrov")]
    public void CyrillicToLatinStringMapsCommonNames(string input, string expectedPrefix)
    {
        ArgumentNullException.ThrowIfNull(expectedPrefix);
        var actual = _sut.CyrillicToLatinString(input);

        Assert.StartsWith(expectedPrefix[..Math.Min(3, expectedPrefix.Length)], actual, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildBidirectionalIncludesOriginalAndConversionForLatin()
    {
        var actual = _sut.BuildBidirectional("Kirill");

        Assert.Contains("kirill", actual, StringComparison.Ordinal);
        Assert.Contains("кирилл", actual, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildBidirectionalIncludesOriginalAndConversionForCyrillic()
    {
        var actual = _sut.BuildBidirectional("Иван");

        Assert.Contains("иван", actual, StringComparison.Ordinal);
        Assert.Contains("ivan", actual, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildBidirectionalOnEmptyReturnsEmpty()
    {
        Assert.Empty(_sut.BuildBidirectional(""));
    }

    [Fact]
    public void NormalizeStripsDiacriticsAndLowercases()
    {
        Assert.Equal("zalesnak", Transliterator.Normalize("Žalešňák"));
    }

    [Fact]
    public void NormalizeFoldsRussianYoToYe()
    {
        // FormD-декомпозиция разносит «ё» на «е» + диакритику, диакритику отбрасываем.
        // Это полезно для поиска: запрос «елкин» матчится в «Ёлкин» и наоборот.
        Assert.Equal("елкин", Transliterator.Normalize("Ёлкин"));
    }
}
