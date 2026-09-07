namespace Wallow.TelemetryGateway;

internal static class RegistrationValidation
{
    public static bool IsValid(DesiredRegistration desired)
    {
        return desired.Revision > 0 && Enum.IsDefined(desired.State)
            && IsIdentity(desired.ClientId) && IsIdentity(desired.ApplicationId)
            && IsIdentity(desired.BrowserService) && IsIdentity(desired.ServerService)
            && desired.Environments is { Count: > 0 and <= 16 }
            && desired.Environments.All(IsIdentity)
            && desired.Environments.Distinct(StringComparer.Ordinal).Count() == desired.Environments.Count
            && desired.Credentials is { Count: <= 4 }
            && desired.Credentials.All(credential => credential is not null && IsIdentity(credential.Id) && !credential.Id.Contains('.', StringComparison.Ordinal)
                && credential.Verifier is { Length: 64 } && credential.Verifier.All(char.IsAsciiHexDigit))
            && desired.Credentials.Select(credential => credential.Id).Distinct(StringComparer.Ordinal).Count() == desired.Credentials.Count
            && (desired.State != RegistrationState.Enabled || desired.Credentials.Count > 0);
    }

    private static bool IsIdentity(string value)
    {
        return value is { Length: > 0 and <= 128 }
            && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
    }
}
