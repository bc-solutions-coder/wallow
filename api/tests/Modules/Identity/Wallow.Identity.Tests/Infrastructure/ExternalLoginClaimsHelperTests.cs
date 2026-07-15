using System.Security.Claims;
using Wallow.Identity.Application.Helpers;

namespace Wallow.Identity.Tests.Infrastructure;

public class ExternalLoginClaimsHelperTests
{
    [Fact]
    public void ExtractName_WithGivenNameAndSurname_ReturnsCorrectNames()
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.GivenName, "John"),
            new Claim(ClaimTypes.Surname, "Doe")
        ];

        (string firstName, string lastName) = ExternalLoginClaimsHelper.ExtractName(claims, "john@example.com");

        firstName.Should().Be("John");
        lastName.Should().Be("Doe");
    }

    [Fact]
    public void ExtractName_WithSingleNameClaim_SplitsOnFirstSpace()
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.Name, "John Michael Doe")
        ];

        (string firstName, string lastName) = ExternalLoginClaimsHelper.ExtractName(claims, "john@example.com");

        firstName.Should().Be("John");
        lastName.Should().Be("Michael Doe");
    }

    [Fact]
    public void ExtractName_WithSingleWordNameClaim_UsesNameAsFirstNameAndEmailLocal()
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.Name, "John")
        ];

        (string firstName, string lastName) = ExternalLoginClaimsHelper.ExtractName(claims, "johndoe@example.com");

        firstName.Should().Be("John");
        lastName.Should().Be("johndoe");
    }

    [Fact]
    public void ExtractName_WithNoClaims_FallsBackToEmailLocal()
    {
        List<Claim> claims = [];

        (string firstName, string lastName) = ExternalLoginClaimsHelper.ExtractName(claims, "jane.doe@example.com");

        firstName.Should().Be("User");
        lastName.Should().Be("jane.doe");
    }

    [Fact]
    public void ExtractEmail_WithEmailClaim_ReturnsEmail()
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.Email, "john@example.com")
        ];

        string? email = ExternalLoginClaimsHelper.ExtractEmail(claims);

        email.Should().Be("john@example.com");
    }

    [Fact]
    public void ExtractEmail_WithNoClaim_ReturnsNull()
    {
        List<Claim> claims = [];

        string? email = ExternalLoginClaimsHelper.ExtractEmail(claims);

        email.Should().BeNull();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void IsEmailVerified_ReturnsExpected(string? claimValue, bool expected)
    {
        List<Claim> claims = [];
        if (claimValue is not null)
        {
            claims.Add(new Claim("email_verified", claimValue));
        }

        bool result = ExternalLoginClaimsHelper.IsEmailVerified(claims);

        result.Should().Be(expected);
    }
}
