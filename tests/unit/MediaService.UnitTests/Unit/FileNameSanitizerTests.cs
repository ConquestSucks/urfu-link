using FluentAssertions;
using MediaService.Api.Application.Storage;

namespace MediaService.UnitTests.Unit;

public class FileNameSanitizerTests
{
    [Theory]
    [InlineData("photo.png", "photo.png")]
    [InlineData("My Holiday Photo.PNG", "MyHolidayPhoto.PNG")]
    [InlineData("../etc/passwd", "etcpasswd")]
    [InlineData("..\\windows\\system32\\cmd.exe", "windowssystem32cmd.exe")]
    [InlineData("файл.png", "png")] // non-ASCII stripped, leading dot trimmed, extension survives
    [InlineData("‮evil.exe", "evil.exe")] // RTL override stripped
    [InlineData("name with\nnewline.txt", "namewithnewline.txt")]
    [InlineData("name\twith\ttabs.txt", "namewithtabs.txt")]
    [InlineData("simple-name_v2.tar.gz", "simple-name_v2.tar.gz")]
    public void Sanitize_StripsDisallowedCharacters(string input, string expected)
    {
        FileNameSanitizer.Sanitize(input).Should().Be(expected);
    }

    [Fact]
    public void Sanitize_TruncatesOverLongNames()
    {
        var input = new string('a', 500) + ".txt";

        var sanitized = FileNameSanitizer.Sanitize(input);

        sanitized.Length.Should().BeLessThanOrEqualTo(200);
    }

    [Fact]
    public void Sanitize_FallsBackToDefaultWhenEmpty()
    {
        FileNameSanitizer.Sanitize("///\\\\??**").Should().Be("file");
        FileNameSanitizer.Sanitize(string.Empty).Should().Be("file");
    }
}
