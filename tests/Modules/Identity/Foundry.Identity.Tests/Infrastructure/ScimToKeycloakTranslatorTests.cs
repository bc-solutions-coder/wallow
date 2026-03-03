using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Infrastructure.Scim;

namespace Foundry.Identity.Tests.Infrastructure;

public class ScimToKeycloakTranslatorTests
{
    private readonly ScimToKeycloakTranslator _translator = new();

    [Fact]
    public void Translate_NullFilter_ReturnsEmptyParams()
    {
        KeycloakQueryParams result = _translator.Translate(null);

        result.Username.Should().BeNull();
        result.Email.Should().BeNull();
        result.FirstName.Should().BeNull();
        result.LastName.Should().BeNull();
        result.Search.Should().BeNull();
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_EmptyFilter_ReturnsEmptyParams()
    {
        KeycloakQueryParams result = _translator.Translate("");

        result.Username.Should().BeNull();
        result.Email.Should().BeNull();
    }

    [Fact]
    public void Translate_UsernameEq_ReturnsUsernameParam()
    {
        KeycloakQueryParams result = _translator.Translate("userName eq \"john.doe\"");

        result.Username.Should().Be("john.doe");
        result.Email.Should().BeNull();
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_EmailEq_ReturnsEmailParam()
    {
        KeycloakQueryParams result = _translator.Translate("emails.value eq \"test@example.com\"");

        result.Email.Should().Be("test@example.com");
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_UsernameCo_ReturnsSearchParam()
    {
        KeycloakQueryParams result = _translator.Translate("userName co \"john\"");

        result.Search.Should().Be("john");
    }

    [Fact]
    public void Translate_ActiveEq_ReturnsInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("active eq \"true\"");

        // active is not a Keycloak query param, so it falls to in-memory
        result.InMemoryFilter.Should().NotBeNull();
    }

    [Fact]
    public void Translate_InMemoryFilter_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("active eq \"true\"");

        ScimUser activeUser = new ScimUser { Active = true, UserName = "test" };
        ScimUser inactiveUser = new ScimUser { Active = false, UserName = "test2" };

        result.InMemoryFilter!(activeUser).Should().BeTrue();
        result.InMemoryFilter!(inactiveUser).Should().BeFalse();
    }

    [Fact]
    public void Translate_NotFilter_ReturnsInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("not (userName eq \"admin\")");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser adminUser = new ScimUser { UserName = "admin" };
        ScimUser regularUser = new ScimUser { UserName = "user" };

        result.InMemoryFilter!(adminUser).Should().BeFalse();
        result.InMemoryFilter!(regularUser).Should().BeTrue();
    }

    [Fact]
    public void Translate_PresenceFilter_ReturnsInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("userName pr");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser withUsername = new ScimUser { UserName = "john" };
        ScimUser withoutUsername = new ScimUser { UserName = "" };

        result.InMemoryFilter!(withUsername).Should().BeTrue();
        result.InMemoryFilter!(withoutUsername).Should().BeFalse();
    }

    [Fact]
    public void Translate_SwOperator_ReturnsInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("userName sw \"jo\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser matchingUser = new ScimUser { UserName = "john.doe" };
        ScimUser nonMatchingUser = new ScimUser { UserName = "alice.smith" };

        result.InMemoryFilter!(matchingUser).Should().BeTrue();
        result.InMemoryFilter!(nonMatchingUser).Should().BeFalse();
    }

    [Fact]
    public void Translate_NeOperator_ReturnsInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("userName ne \"admin\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser adminUser = new ScimUser { UserName = "admin" };
        ScimUser regularUser = new ScimUser { UserName = "user" };

        result.InMemoryFilter!(adminUser).Should().BeFalse();
        result.InMemoryFilter!(regularUser).Should().BeTrue();
    }

    [Fact]
    public void Translate_GivenNameEq_ReturnsFirstNameParam()
    {
        KeycloakQueryParams result = _translator.Translate("name.givenName eq \"John\"");

        result.FirstName.Should().Be("John");
    }

    [Fact]
    public void Translate_FamilyNameEq_ReturnsLastNameParam()
    {
        KeycloakQueryParams result = _translator.Translate("name.familyName eq \"Doe\"");

        result.LastName.Should().Be("Doe");
    }
}
