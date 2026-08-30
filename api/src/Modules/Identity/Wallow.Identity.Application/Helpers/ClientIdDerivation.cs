using System.Globalization;
using System.Text;

namespace Wallow.Identity.Application.Helpers;

/// <summary>
/// Derives the immutable client id of an organization-registered client from the organization's
/// slug and the client's name: <c>app-&lt;org-slug&gt;-&lt;name-slug&gt;</c>.
/// </summary>
public static class ClientIdDerivation
{
    public const string ApplicationPrefix = "app-";

    public static string DeriveApplicationClientId(string organizationSlug, string name) =>
        ApplicationPrefix + Slugify(organizationSlug) + "-" + Slugify(name);

    /// <summary>
    /// Lowercase ASCII letters and digits with runs of anything else collapsed to one hyphen; the
    /// same shape the organization slug takes so the two halves of a client id read alike.
    /// </summary>
    public static string Slugify(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        StringBuilder builder = new(value.Length);
        bool pendingHyphen = false;
        foreach (char c in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(c))
            {
                if (pendingHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                }

                pendingHyphen = false;
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                pendingHyphen = true;
            }
        }

        return builder.ToString();
    }
}
