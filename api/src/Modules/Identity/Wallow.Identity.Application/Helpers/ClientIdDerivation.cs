using System.Globalization;
using System.Text;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Errors;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Application.Helpers;

/// <summary>
/// Derives immutable organization client IDs as app-&lt;org-slug&gt;-&lt;name-slug&gt;
/// or sa-&lt;org-slug&gt;-&lt;name-slug&gt; according to client kind.
/// </summary>
public static class ClientIdDerivation
{
    public const string ApplicationPrefix = "app-";
    public const string ServiceAccountPrefix = "sa-";

    /// <summary>
    /// Rejects names whose normalized slug is empty, avoiding collisions on a bare prefix.
    /// </summary>
    public static string DeriveClientId(RegisteredClientKind kind, string organizationSlug, string name)
    {
        string nameSlug = Slugify(name);
        if (nameSlug.Length == 0)
        {
            throw new BusinessRuleException(IdentityErrors.ClientNameUnusable);
        }

        return PrefixOf(kind) + Slugify(organizationSlug) + "-" + nameSlug;
    }

    public static string PrefixOf(RegisteredClientKind kind) =>
        kind == RegisteredClientKind.ServiceAccount ? ServiceAccountPrefix : ApplicationPrefix;

    /// <summary>
    /// Normalizes to lowercase ASCII letters and digits. Removes decomposed combining marks,
    /// uses one hyphen between other character runs, and omits leading/trailing hyphens.
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
