using Wallow.Shared.Infrastructure.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Shared.Infrastructure.Tests.Services;

public class HtmlSanitizationServiceTests
{
    private readonly IHtmlSanitizationService _sut;

    public HtmlSanitizationServiceTests()
    {
        ServiceProvider provider = new ServiceCollection()
            .AddHtmlSanitization()
            .BuildServiceProvider();

        _sut = provider.GetRequiredService<IHtmlSanitizationService>();
    }

    [Fact]
    public void Sanitize_WithNullInput_ReturnsEmpty()
    {
        string result = _sut.Sanitize(null!);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_WithEmptyString_ReturnsEmpty()
    {
        string result = _sut.Sanitize(string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_WithWhitespaceOnly_ReturnsEmpty()
    {
        string result = _sut.Sanitize("   ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_WithPlainText_ReturnsUnchanged()
    {
        string result = _sut.Sanitize("Hello, world!");

        result.Should().Be("Hello, world!");
    }

    [Theory]
    [InlineData("p")]
    [InlineData("b")]
    [InlineData("i")]
    [InlineData("u")]
    [InlineData("em")]
    [InlineData("strong")]
    [InlineData("small")]
    [InlineData("br")]
    [InlineData("hr")]
    [InlineData("blockquote")]
    [InlineData("pre")]
    [InlineData("code")]
    [InlineData("span")]
    [InlineData("div")]
    [InlineData("sub")]
    [InlineData("sup")]
    public void Sanitize_WithAllowedFormattingTag_PreservesTag(string tag)
    {
        string html = $"<{tag}>content</{tag}>";

        string result = _sut.Sanitize(html);

        result.Should().Contain($"<{tag}>");
    }

    [Theory]
    [InlineData("h1")]
    [InlineData("h2")]
    [InlineData("h3")]
    [InlineData("h4")]
    [InlineData("h5")]
    [InlineData("h6")]
    public void Sanitize_WithAllowedHeadingTag_PreservesTag(string tag)
    {
        string html = $"<{tag}>Heading</{tag}>";

        string result = _sut.Sanitize(html);

        result.Should().Contain($"<{tag}>Heading</{tag}>");
    }

    [Fact]
    public void Sanitize_WithAllowedListTags_PreservesTags()
    {
        string html = "<ul><li>Item 1</li><li>Item 2</li></ul>";

        string result = _sut.Sanitize(html);

        result.Should().Contain("<ul>");
        result.Should().Contain("<li>");
    }

    [Fact]
    public void Sanitize_WithAllowedTableTags_PreservesTags()
    {
        string html = "<table><thead><tr><th>Header</th></tr></thead><tbody><tr><td>Cell</td></tr></tbody></table>";

        string result = _sut.Sanitize(html);

        result.Should().Contain("<table>");
        result.Should().Contain("<thead>");
        result.Should().Contain("<tbody>");
        result.Should().Contain("<tr>");
        result.Should().Contain("<th>");
        result.Should().Contain("<td>");
    }

    [Fact]
    public void Sanitize_WithAnchorTag_PreservesHref()
    {
        string html = "<a href=\"https://example.com\" title=\"Example\">Link</a>";

        string result = _sut.Sanitize(html);

        result.Should().Contain("<a");
        result.Should().Contain("href=\"https://example.com\"");
        result.Should().Contain("title=\"Example\"");
        result.Should().Contain("Link</a>");
    }

    [Fact]
    public void Sanitize_WithScriptTag_RemovesScript()
    {
        string html = "<p>Safe</p><script>alert('xss')</script>";

        string result = _sut.Sanitize(html);

        result.Should().Contain("<p>Safe</p>");
        result.Should().NotContain("<script>");
        result.Should().NotContain("alert");
    }

    [Fact]
    public void Sanitize_WithOnClickAttribute_RemovesAttribute()
    {
        string html = "<p onclick=\"alert('xss')\">Text</p>";

        string result = _sut.Sanitize(html);

        result.Should().Contain("<p>");
        result.Should().NotContain("onclick");
        result.Should().NotContain("alert");
    }

    [Fact]
    public void Sanitize_WithOnMouseOverAttribute_RemovesAttribute()
    {
        string html = "<div onmouseover=\"steal()\">Hover me</div>";

        string result = _sut.Sanitize(html);

        result.Should().Contain("<div>");
        result.Should().NotContain("onmouseover");
    }

    [Fact]
    public void Sanitize_WithStyleAttribute_RemovesAttribute()
    {
        string html = "<p style=\"color:red\">Styled</p>";

        string result = _sut.Sanitize(html);

        result.Should().Contain("<p>");
        result.Should().NotContain("style");
    }

    [Fact]
    public void Sanitize_WithClassAttribute_PreservesAttribute()
    {
        string html = "<span class=\"highlight\">Text</span>";

        string result = _sut.Sanitize(html);

        result.Should().Contain("class=\"highlight\"");
    }

    [Fact]
    public void Sanitize_WithIdAttribute_PreservesAttribute()
    {
        string html = "<div id=\"section-1\">Content</div>";

        string result = _sut.Sanitize(html);

        result.Should().Contain("id=\"section-1\"");
    }

    [Fact]
    public void Sanitize_WithColspanAndRowspan_PreservesAttributes()
    {
        string html = "<table><tr><td colspan=\"2\" rowspan=\"3\">Cell</td></tr></table>";

        string result = _sut.Sanitize(html);

        result.Should().Contain("colspan=\"2\"");
        result.Should().Contain("rowspan=\"3\"");
    }

    [Fact]
    public void Sanitize_WithNestedScriptInDiv_RemovesScript()
    {
        string html = "<div><p>Safe</p><script>document.cookie</script><p>Also safe</p></div>";

        string result = _sut.Sanitize(html);

        result.Should().Contain("<div>");
        result.Should().Contain("Safe");
        result.Should().Contain("Also safe");
        result.Should().NotContain("<script>");
        result.Should().NotContain("document.cookie");
    }

    [Fact]
    public void Sanitize_WithIframeTag_RemovesIframe()
    {
        string html = "<iframe src=\"https://evil.com\"></iframe><p>Safe</p>";

        string result = _sut.Sanitize(html);

        result.Should().NotContain("<iframe>");
        result.Should().Contain("<p>Safe</p>");
    }

    [Fact]
    public void Sanitize_WithImgTag_RemovesImg()
    {
        string html = "<img src=\"https://evil.com/tracker.gif\" /><p>Text</p>";

        string result = _sut.Sanitize(html);

        result.Should().NotContain("<img");
        result.Should().Contain("<p>Text</p>");
    }

    [Fact]
    public void Sanitize_WithJavascriptSchemeInHref_RemovesHref()
    {
        string html = "<a href=\"javascript:alert('xss')\">Click</a>";

        string result = _sut.Sanitize(html);

        result.Should().NotContain("javascript:");
    }

    [Fact]
    public void Sanitize_WithMailtoScheme_PreservesHref()
    {
        string html = "<a href=\"mailto:user@example.com\">Email</a>";

        string result = _sut.Sanitize(html);

        result.Should().Contain("mailto:user@example.com");
    }

    [Fact]
    public void Sanitize_WithHttpScheme_PreservesHref()
    {
        string html = "<a href=\"http://example.com\">Link</a>";

        string result = _sut.Sanitize(html);

        result.Should().Contain("http://example.com");
    }

    [Fact]
    public void Sanitize_WithFormTag_RemovesForm()
    {
        string html = "<form action=\"/steal\"><input type=\"text\" /></form><p>Safe</p>";

        string result = _sut.Sanitize(html);

        result.Should().NotContain("<form");
        result.Should().NotContain("<input");
        result.Should().Contain("<p>Safe</p>");
    }

    [Fact]
    public void Sanitize_WithMalformedHtml_HandlesGracefully()
    {
        string html = "<p>Unclosed paragraph<b>Bold without close";

        string result = _sut.Sanitize(html);

        result.Should().Contain("Unclosed paragraph");
        result.Should().Contain("Bold without close");
    }

    [Fact]
    public void Sanitize_WithDataAttribute_RemovesAttribute()
    {
        string html = "<div data-value=\"secret\">Content</div>";

        string result = _sut.Sanitize(html);

        result.Should().Contain("<div>");
        result.Should().NotContain("data-value");
    }

    [Fact]
    public void Sanitize_WithObjectTag_RemovesTag()
    {
        string html = "<object data=\"malware.swf\"></object><p>Safe</p>";

        string result = _sut.Sanitize(html);

        result.Should().NotContain("<object");
        result.Should().Contain("<p>Safe</p>");
    }

    [Fact]
    public void Sanitize_WithEmbedTag_RemovesTag()
    {
        string html = "<embed src=\"malware.swf\" /><p>Safe</p>";

        string result = _sut.Sanitize(html);

        result.Should().NotContain("<embed");
        result.Should().Contain("<p>Safe</p>");
    }
}
