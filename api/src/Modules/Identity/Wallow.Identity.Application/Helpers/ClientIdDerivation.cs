using System.Globalization;
using System.Text;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Errors;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Application.Helpers;

/// <summary>
/// Derives the immutable client id of an organization-registered client from its kind, the
/// organization's slug and the client's name: <c>app-&lt;org-slug&gt;-&lt;name-slug&gt;</c> for a
/// developer application, <c>sa-&lt;org-slug&gt;-&lt;name-slug&gt;</c> for a service account. The
/// prefix is what the authorization pipeline keys on to expand a client's scopes into permissions.
/// </summary>
public static class ClientIdDerivation
{
    public const string ApplicationPrefix = "app-";
    public const string ServiceAccountPrefix = "sa-";

    /// <summary>
    /// Refuses a name with no letter or digit in it: the id would end in a bare hyphen and every
    /// such name would collide with every other.
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
