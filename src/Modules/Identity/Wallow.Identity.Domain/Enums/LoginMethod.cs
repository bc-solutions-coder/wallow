namespace Wallow.Identity.Domain.Enums;

[Flags]
public enum LoginMethod
{
    None = 0,
    Password = 1,
    MagicLink = 2,
    Otp = 4,
    Google = 8,
    Microsoft = 16,
    GitHub = 32,
    Apple = 64,
    All = Password | MagicLink | Otp | Google | Microsoft | GitHub | Apple
}
