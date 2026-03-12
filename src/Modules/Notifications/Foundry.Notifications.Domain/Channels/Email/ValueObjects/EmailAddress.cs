using System.Text.RegularExpressions;
using Foundry.Notifications.Domain.Channels.Email.Exceptions;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Notifications.Domain.Channels.Email.ValueObjects;

public sealed partial class EmailAddress : ValueObject
{
    public string Value { get; }

    private EmailAddress(string value)
    {
        Value = value.ToLowerInvariant();
    }

    public static EmailAddress Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidEmailAddressException("Email address cannot be empty");
        }

        email = email.Trim();

        if (!EmailRegex().IsMatch(email))
        {
            throw new InvalidEmailAddressException($"'{email}' is not a valid email address");
        }

        return new EmailAddress(email);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(EmailAddress email) => email.Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailRegex();
}
