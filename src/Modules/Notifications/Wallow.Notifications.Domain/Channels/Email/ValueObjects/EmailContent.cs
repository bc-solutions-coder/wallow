using Wallow.Shared.Kernel.Domain;

namespace Wallow.Notifications.Domain.Channels.Email.ValueObjects;

public sealed class EmailContent : ValueObject
{
    public string Subject { get; }
    public string Body { get; }

    private EmailContent(string subject, string body)
    {
        Subject = subject;
        Body = body;
    }

    public static EmailContent Create(string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Email subject cannot be empty", nameof(subject));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Email body cannot be empty", nameof(body));
        }

        return new EmailContent(subject.Trim(), body.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Subject;
        yield return Body;
    }
}
