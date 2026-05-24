using UserService.Api.Infrastructure.Search;

namespace UserService.UnitTests.Infrastructure;

public sealed class UserSearchTextBuilderTests
{
    private readonly UserSearchTextBuilder _sut = new(new Transliterator());

    [Fact]
    public void BuildIncludesAllNonEmptyParts()
    {
        var (searchText, _) = _sut.Build(
            username: "ivanov",
            firstName: "Иван",
            lastName: "Иванов",
            email: "ivanov@urfu.ru");

        Assert.Contains("иван", searchText, StringComparison.Ordinal);
        Assert.Contains("иванов", searchText, StringComparison.Ordinal);
        Assert.Contains("ivanov", searchText, StringComparison.Ordinal);
        Assert.Contains("ivanov@urfu.ru", searchText, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSkipsNullOrEmptyParts()
    {
        var (searchText, _) = _sut.Build(
            username: "user",
            firstName: null,
            lastName: "",
            email: null);

        Assert.Equal("user", searchText);
    }

    [Fact]
    public void BuildTransliteratesBothDirections()
    {
        var (_, translit) = _sut.Build(
            username: "kirill",
            firstName: null,
            lastName: null,
            email: null);

        Assert.Contains("kirill", translit, StringComparison.Ordinal);
        Assert.Contains("кирилл", translit, StringComparison.Ordinal);
    }
}
