using Wallow.Identity.Api.Contracts.Enums;
using Wallow.Identity.Api.Mappings;
using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Tests.Api.Mappings;

public class EnumMappingsTests
{
    #region ToDomain

    [Theory]
    [InlineData(ApiSamlNameIdFormat.Email, SamlNameIdFormat.Email)]
    [InlineData(ApiSamlNameIdFormat.Persistent, SamlNameIdFormat.Persistent)]
    [InlineData(ApiSamlNameIdFormat.Transient, SamlNameIdFormat.Transient)]
    [InlineData(ApiSamlNameIdFormat.Unspecified, SamlNameIdFormat.Unspecified)]
    public void ToDomain_MapsAllValues(ApiSamlNameIdFormat api, SamlNameIdFormat expected)
    {
        SamlNameIdFormat result = api.ToDomain();

        result.Should().Be(expected);
    }

    [Fact]
    public void ToDomain_WithInvalidValue_ThrowsArgumentOutOfRange()
    {
        ApiSamlNameIdFormat invalid = (ApiSamlNameIdFormat)99;

        Action act = () => invalid.ToDomain();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("api");
    }

    #endregion

    #region ToApi

    [Theory]
    [InlineData(SamlNameIdFormat.Email, ApiSamlNameIdFormat.Email)]
    [InlineData(SamlNameIdFormat.Persistent, ApiSamlNameIdFormat.Persistent)]
    [InlineData(SamlNameIdFormat.Transient, ApiSamlNameIdFormat.Transient)]
    [InlineData(SamlNameIdFormat.Unspecified, ApiSamlNameIdFormat.Unspecified)]
    public void ToApi_MapsAllValues(SamlNameIdFormat domain, ApiSamlNameIdFormat expected)
    {
        ApiSamlNameIdFormat result = domain.ToApi();

        result.Should().Be(expected);
    }

    [Fact]
    public void ToApi_WithInvalidValue_ThrowsArgumentOutOfRange()
    {
        SamlNameIdFormat invalid = (SamlNameIdFormat)99;

        Action act = () => invalid.ToApi();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("domain");
    }

    #endregion

    #region Roundtrip

    [Theory]
    [InlineData(ApiSamlNameIdFormat.Email)]
    [InlineData(ApiSamlNameIdFormat.Persistent)]
    [InlineData(ApiSamlNameIdFormat.Transient)]
    [InlineData(ApiSamlNameIdFormat.Unspecified)]
    public void Roundtrip_ApiToDomainAndBack_ReturnsOriginal(ApiSamlNameIdFormat original)
    {
        ApiSamlNameIdFormat roundtripped = original.ToDomain().ToApi();

        roundtripped.Should().Be(original);
    }

    #endregion
}
