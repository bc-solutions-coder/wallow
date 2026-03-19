namespace Foundry.Api.Extensions;

public static class RateLimitDefaults
{
    public const int AuthPermitLimit = 3;
    public const int AuthWindowMinutes = 10;
    public const int UploadPermitLimit = 10;
    public const int UploadWindowHours = 1;
    public const int ScimPermitLimit = 30;
    public const int ScimWindowMinutes = 1;
    public const int GlobalPermitLimit = 1000;
    public const int GlobalWindowHours = 1;
    public const int DeveloperAppRegistrationPermitLimit = 5;
    public const int DeveloperAppRegistrationWindowHours = 1;
}
