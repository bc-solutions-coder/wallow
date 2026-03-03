using Ganss.Xss;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Shared.Infrastructure.Services;

public interface IHtmlSanitizationService
{
    string Sanitize(string html);
}

internal sealed class HtmlSanitizationService : IHtmlSanitizationService
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizationService()
    {
        _sanitizer = new HtmlSanitizer();

        // Allow safe formatting tags only
        _sanitizer.AllowedTags.Clear();
        foreach (string tag in new[]
        {
            "p", "br", "b", "i", "u", "em", "strong", "small",
            "ul", "ol", "li", "blockquote", "pre", "code",
            "h1", "h2", "h3", "h4", "h5", "h6",
            "a", "span", "div", "sub", "sup", "hr", "table",
            "thead", "tbody", "tr", "th", "td"
        })
        {
            _sanitizer.AllowedTags.Add(tag);
        }

        // Allow safe attributes
        _sanitizer.AllowedAttributes.Clear();
        foreach (string attr in new[] { "href", "title", "class", "id", "colspan", "rowspan" })
        {
            _sanitizer.AllowedAttributes.Add(attr);
        }

        // Only allow safe URI schemes for links
        _sanitizer.AllowedSchemes.Clear();
        foreach (string scheme in new[] { "http", "https", "mailto" })
        {
            _sanitizer.AllowedSchemes.Add(scheme);
        }
    }

    public string Sanitize(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return _sanitizer.Sanitize(html);
    }
}

public static class HtmlSanitizationServiceExtensions
{
    public static IServiceCollection AddHtmlSanitization(this IServiceCollection services)
    {
        services.AddSingleton<IHtmlSanitizationService, HtmlSanitizationService>();
        return services;
    }
}
