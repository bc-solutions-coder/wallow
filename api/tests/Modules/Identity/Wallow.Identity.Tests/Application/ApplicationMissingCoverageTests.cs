using Wallow.Identity.Application.Settings;

namespace Wallow.Identity.Tests.Application;

public class ApplicationMissingCoverageTests
{
    #region IdentitySettingKeys

    [Fact]
    public void IdentitySettingKeys_Timezone_HasExpectedKey()
    {
        IdentitySettingKeys.Timezone.Key.Should().Be("identity.timezone");
        IdentitySettingKeys.Timezone.DefaultValue.Should().Be("UTC");
    }

    [Fact]
    public void IdentitySettingKeys_Locale_HasExpectedKey()
    {
        IdentitySettingKeys.Locale.Key.Should().Be("identity.locale");
        IdentitySettingKeys.Locale.DefaultValue.Should().Be("en-US");
    }

    [Fact]
    public void IdentitySettingKeys_DateFormat_HasExpectedKey()
    {
        IdentitySettingKeys.DateFormat.Key.Should().Be("identity.date_format");
    }

    [Fact]
    public void IdentitySettingKeys_Theme_HasExpectedKey()
    {
        IdentitySettingKeys.Theme.Key.Should().Be("identity.theme");
        IdentitySettingKeys.Theme.DefaultValue.Should().Be("light");
    }

    [Fact]
    public void IdentitySettingKeys_ModuleName_IsIdentity()
    {
        IdentitySettingKeys keys = new();
        keys.ModuleName.Should().Be("identity");
    }

    #endregion

}
