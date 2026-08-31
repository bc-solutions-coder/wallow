namespace Wallow.Api.Extensions;

public static class RateLimitDefaults
{
    public const int AuthPermitLimit = 30;
    public const int AuthWindowMinutes = 1;
    public const int UploadPermitLimit = 10;
    public const int UploadWindowHours = 1;
    public const int GlobalPermitLimit = 1000;
    public const int GlobalWindowHours = 1;
    public const int RegistrationPermitLimit = 5;
    public const int RegistrationWindowHours = 1;
}
