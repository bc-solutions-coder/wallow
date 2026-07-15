using System.Text.RegularExpressions;
using Wallow.Notifications.Domain.Channels.Sms.Exceptions;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Notifications.Domain.Channels.Sms.ValueObjects;

public sealed partial class PhoneNumber : ValueObject
{
    public string Value { get; }

    private PhoneNumber(string value)
    {
        Value = value;
    }

    public static PhoneNumber Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidPhoneNumberException("Phone number cannot be empty");
        }

        value = value.Trim();

        if (!E164Regex().IsMatch(value))
        {
            throw new InvalidPhoneNumberException($"'{value}' is not a valid E.164 phone number");
        }

        return new PhoneNumber(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(PhoneNumber phone) => phone.Value;

    [GeneratedRegex(@"^\+[1-9]\d{6,14}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex E164Regex();
}
