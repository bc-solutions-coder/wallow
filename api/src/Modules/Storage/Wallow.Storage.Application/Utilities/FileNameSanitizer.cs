using System.Text.RegularExpressions;

namespace Wallow.Storage.Application.Utilities;

public static partial class FileNameSanitizer
{
    private const int MaxFileNameLength = 255;
    private const string DefaultFileName = "unnamed";

    public static string Sanitize(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return DefaultFileName;
        }

        // Normalize backslashes so Path.GetFileName works cross-platform
        string normalized = fileName.Replace('\\', '/');

        // Strip path components — only keep the final segment
        int lastSlash = normalized.LastIndexOf('/');
        string name = lastSlash >= 0 ? normalized[(lastSlash + 1)..] : normalized;

        if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
        {
            return DefaultFileName;
        }

        // Remove dangerous characters: control chars, null bytes, quotes, semicolons, newlines
        name = DangerousCharsRegex().Replace(name, string.Empty);

        // Replace remaining invalid filename chars with underscore
        char[] invalidChars = Path.GetInvalidFileNameChars();
        foreach (char c in invalidChars)
        {
            name = name.Replace(c, '_');
        }

        // Trim whitespace that may have been exposed
        name = name.Trim();

        if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
        {
            return DefaultFileName;
        }

        // Limit length while preserving extension
        if (name.Length > MaxFileNameLength)
        {
            string extension = Path.GetExtension(name);
            int stemMaxLength = MaxFileNameLength - extension.Length;

            if (stemMaxLength <= 0)
            {
                name = name[..MaxFileNameLength];
            }
            else
            {
                string stem = Path.GetFileNameWithoutExtension(name)[..stemMaxLength];
                name = stem + extension;
            }
        }

        return name;
    }

    [GeneratedRegex(@"[\x00-\x1F\x7F"";]")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("MA0009", "MA0009:Regular expressions should not be vulnerable to Denial of Service attacks", Justification = "GeneratedRegex is compile-time safe; no backtracking vulnerability.")]
    private static partial Regex DangerousCharsRegex();
}
