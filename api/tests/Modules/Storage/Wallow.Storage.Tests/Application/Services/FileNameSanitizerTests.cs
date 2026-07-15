using Wallow.Storage.Application.Utilities;

namespace Wallow.Storage.Tests.Application.Services;

public class FileNameSanitizerTests
{
    [Theory]
    [InlineData("document.pdf", "document.pdf")]
    [InlineData("my file.txt", "my file.txt")]
    [InlineData("photo.JPEG", "photo.JPEG")]
    [InlineData("report-2024_Q1.xlsx", "report-2024_Q1.xlsx")]
    public void Sanitize_NormalFileNames_ReturnsUnchanged(string input, string expected)
    {
        string result = FileNameSanitizer.Sanitize(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..\\..\\windows\\system32\\config", "config")]
    [InlineData("/etc/shadow", "shadow")]
    [InlineData("C:\\Users\\admin\\secrets.txt", "secrets.txt")]
    [InlineData("../../../file.txt", "file.txt")]
    [InlineData("foo/bar/baz.txt", "baz.txt")]
    [InlineData("..\\..\\..\\boot.ini", "boot.ini")]
    public void Sanitize_PathTraversalAttempts_StripsPathComponents(string input, string expected)
    {
        string result = FileNameSanitizer.Sanitize(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Sanitize_NullEmptyOrWhitespace_ReturnsUnnamed(string? input)
    {
        string result = FileNameSanitizer.Sanitize(input!);

        result.Should().Be("unnamed");
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public void Sanitize_DotEntries_ReturnsUnnamed(string input)
    {
        string result = FileNameSanitizer.Sanitize(input);

        result.Should().Be("unnamed");
    }

    [Fact]
    public void Sanitize_OnlyDangerousChars_ReturnsUnnamed()
    {
        string input = "\u0000\u0001\u0002";

        string result = FileNameSanitizer.Sanitize(input);

        result.Should().Be("unnamed");
    }

    [Fact]
    public void Sanitize_ControlCharacters_RemovesThem()
    {
        string input = "file\u0000name\u000D\u000A.txt";

        string result = FileNameSanitizer.Sanitize(input);

        result.Should().Be("filename.txt");
    }

    [Fact]
    public void Sanitize_NullByte_RemovesIt()
    {
        string input = "malicious\u0000.exe";

        string result = FileNameSanitizer.Sanitize(input);

        result.Should().Be("malicious.exe");
    }

    [Fact]
    public void Sanitize_HeaderInjectionChars_RemovesThem()
    {
        string input = "file\";name;.txt";

        string result = FileNameSanitizer.Sanitize(input);

        result.Should().Be("filename.txt");
    }

    [Fact]
    public void Sanitize_NewlinesInFileName_RemovesThem()
    {
        string input = "file\nname\r.txt";

        string result = FileNameSanitizer.Sanitize(input);

        result.Should().Be("filename.txt");
    }

    [Fact]
    public void Sanitize_VeryLongName_TruncatesPreservingExtension()
    {
        string longStem = new('a', 300);
        string input = longStem + ".pdf";

        string result = FileNameSanitizer.Sanitize(input);

        result.Length.Should().BeLessThanOrEqualTo(255);
        result.Should().EndWith(".pdf");
        Path.GetExtension(result).Should().Be(".pdf");
    }

    [Fact]
    public void Sanitize_VeryLongNameWithoutExtension_TruncatesTo255()
    {
        string input = new('b', 300);

        string result = FileNameSanitizer.Sanitize(input);

        result.Length.Should().Be(255);
    }

    [Fact]
    public void Sanitize_VeryLongExtension_TruncatesToMaxLength()
    {
        string longExtension = "." + new string('e', 300);
        string input = "f" + longExtension;

        string result = FileNameSanitizer.Sanitize(input);

        result.Length.Should().BeLessThanOrEqualTo(255);
    }

    [Theory]
    [InlineData("café.txt", "café.txt")]
    [InlineData("日本語ファイル.pdf", "日本語ファイル.pdf")]
    [InlineData("Ünïcödë.docx", "Ünïcödë.docx")]
    [InlineData("файл.txt", "файл.txt")]
    [InlineData("emoji\U0001F600.txt", "emoji\U0001F600.txt")]
    public void Sanitize_UnicodeCharacters_PreservesThem(string input, string expected)
    {
        string result = FileNameSanitizer.Sanitize(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Sanitize_TrailingSlash_ReturnsUnnamed()
    {
        string result = FileNameSanitizer.Sanitize("some/path/");

        result.Should().Be("unnamed");
    }

    [Fact]
    public void Sanitize_PathWithDotDotAsFilename_ReturnsUnnamed()
    {
        string result = FileNameSanitizer.Sanitize("/some/path/..");

        result.Should().Be("unnamed");
    }

    [Fact]
    public void Sanitize_LeadingAndTrailingWhitespace_TrimsIt()
    {
        string result = FileNameSanitizer.Sanitize("  document.pdf  ");

        result.Should().Be("document.pdf");
    }
}
