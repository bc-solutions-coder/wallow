using Foundry.Storage.Application.Utilities;

namespace Foundry.Storage.Tests.Application;

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
    public void Sanitize_PathTraversalAttempts_StripsPathComponents(string input, string expected)
    {
        string result = FileNameSanitizer.Sanitize(input);

        result.Should().Be(expected);
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
    public void Sanitize_VeryLongName_TruncatesPreservingExtension()
    {
        string longStem = new('a', 300);
        string input = longStem + ".pdf";

        string result = FileNameSanitizer.Sanitize(input);

        result.Length.Should().BeLessThanOrEqualTo(255);
        result.Should().EndWith(".pdf");
    }

    [Fact]
    public void Sanitize_VeryLongNameWithoutExtension_TruncatesTo255()
    {
        string input = new('b', 300);

        string result = FileNameSanitizer.Sanitize(input);

        result.Length.Should().Be(255);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sanitize_EmptyOrWhitespace_ReturnsUnnamed(string? input)
    {
        string result = FileNameSanitizer.Sanitize(input!);

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
    public void Sanitize_ExtensionPreservedOnTruncation()
    {
        string longStem = new('x', 260);
        string input = longStem + ".docx";

        string result = FileNameSanitizer.Sanitize(input);

        result.Should().EndWith(".docx");
        Path.GetExtension(result).Should().Be(".docx");
        result.Length.Should().BeLessThanOrEqualTo(255);
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
    public void Sanitize_NewlinesInFileName_RemovesThem()
    {
        string input = "file\nname\r.txt";

        string result = FileNameSanitizer.Sanitize(input);

        result.Should().Be("filename.txt");
    }
}
