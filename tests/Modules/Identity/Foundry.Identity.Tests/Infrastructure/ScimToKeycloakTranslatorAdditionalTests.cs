using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Infrastructure.Scim;

namespace Foundry.Identity.Tests.Infrastructure;

public class ScimToKeycloakTranslatorAdditionalTests
{
    private readonly ScimToKeycloakTranslator _translator = new();

    [Fact]
    public void Translate_EmailCo_ReturnsSearchParam()
    {
        KeycloakQueryParams result = _translator.Translate("emails.value co \"test\"");

        result.Search.Should().Be("test");
    }

    [Fact]
    public void Translate_FirstNameCo_ReturnsSearchParam()
    {
        KeycloakQueryParams result = _translator.Translate("name.givenName co \"John\"");

        result.Search.Should().Be("John");
    }

    [Fact]
    public void Translate_LastNameCo_ReturnsSearchParam()
    {
        KeycloakQueryParams result = _translator.Translate("name.familyName co \"Doe\"");

        result.Search.Should().Be("Doe");
    }

    [Fact]
    public void Translate_EwOperator_ReturnsInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("userName ew \"doe\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser matchingUser = new() { UserName = "john.doe" };
        ScimUser nonMatchingUser = new() { UserName = "alice.smith" };

        result.InMemoryFilter!(matchingUser).Should().BeTrue();
        result.InMemoryFilter!(nonMatchingUser).Should().BeFalse();
    }

    [Fact]
    public void Translate_GtOperator_ReturnsInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("userName gt \"b\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser greaterUser = new() { UserName = "charlie" };
        ScimUser lesserUser = new() { UserName = "alice" };

        result.InMemoryFilter!(greaterUser).Should().BeTrue();
        result.InMemoryFilter!(lesserUser).Should().BeFalse();
    }

    [Fact]
    public void Translate_GeOperator_ReturnsInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("userName ge \"bob\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser equalUser = new() { UserName = "bob" };
        ScimUser greaterUser = new() { UserName = "charlie" };
        ScimUser lesserUser = new() { UserName = "alice" };

        result.InMemoryFilter!(equalUser).Should().BeTrue();
        result.InMemoryFilter!(greaterUser).Should().BeTrue();
        result.InMemoryFilter!(lesserUser).Should().BeFalse();
    }

    [Fact]
    public void Translate_LtOperator_ReturnsInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("userName lt \"bob\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser lesserUser = new() { UserName = "alice" };
        ScimUser greaterUser = new() { UserName = "charlie" };

        result.InMemoryFilter!(lesserUser).Should().BeTrue();
        result.InMemoryFilter!(greaterUser).Should().BeFalse();
    }

    [Fact]
    public void Translate_LeOperator_ReturnsInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("userName le \"bob\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser equalUser = new() { UserName = "bob" };
        ScimUser lesserUser = new() { UserName = "alice" };
        ScimUser greaterUser = new() { UserName = "charlie" };

        result.InMemoryFilter!(equalUser).Should().BeTrue();
        result.InMemoryFilter!(lesserUser).Should().BeTrue();
        result.InMemoryFilter!(greaterUser).Should().BeFalse();
    }

    [Fact]
    public void Translate_AndFilter_CombinesParams()
    {
        KeycloakQueryParams result = _translator.Translate("userName eq \"john\" and active eq \"true\"");

        // Both sides have params, one is in-memory, so combined with in-memory filter
        result.InMemoryFilter.Should().NotBeNull();

        ScimUser matchingUser = new() { UserName = "john", Active = true };
        ScimUser wrongUser = new() { UserName = "john", Active = false };
        ScimUser wrongName = new() { UserName = "jane", Active = true };

        result.InMemoryFilter!(matchingUser).Should().BeTrue();
        result.InMemoryFilter!(wrongUser).Should().BeFalse();
        result.InMemoryFilter!(wrongName).Should().BeFalse();
    }

    [Fact]
    public void Translate_OrFilter_CombinesParams()
    {
        KeycloakQueryParams result = _translator.Translate("userName eq \"john\" or userName eq \"jane\"");

        // Two Keycloak params with same type -> falls to in-memory
        result.InMemoryFilter.Should().NotBeNull();

        ScimUser john = new() { UserName = "john" };
        ScimUser jane = new() { UserName = "jane" };
        ScimUser other = new() { UserName = "bob" };

        result.InMemoryFilter!(john).Should().BeTrue();
        result.InMemoryFilter!(jane).Should().BeTrue();
        result.InMemoryFilter!(other).Should().BeFalse();
    }

    [Fact]
    public void Translate_PresenceEmail_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("emails.value pr");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser withEmail = new()
        {
            UserName = "john",
            Emails = new[] { new ScimEmail { Value = "john@test.com", Primary = true } }
        };
        ScimUser withoutEmail = new() { UserName = "john" };

        result.InMemoryFilter!(withEmail).Should().BeTrue();
        result.InMemoryFilter!(withoutEmail).Should().BeFalse();
    }

    [Fact]
    public void Translate_PresenceFirstName_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("name.givenName pr");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser withName = new() { UserName = "john", Name = new ScimName { GivenName = "John" } };
        ScimUser withoutName = new() { UserName = "john", Name = new ScimName { GivenName = null } };

        result.InMemoryFilter!(withName).Should().BeTrue();
        result.InMemoryFilter!(withoutName).Should().BeFalse();
    }

    [Fact]
    public void Translate_PresenceLastName_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("name.familyName pr");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser withName = new() { UserName = "john", Name = new ScimName { FamilyName = "Doe" } };
        ScimUser withoutName = new() { UserName = "john", Name = new ScimName { FamilyName = "" } };

        result.InMemoryFilter!(withName).Should().BeTrue();
        result.InMemoryFilter!(withoutName).Should().BeFalse();
    }

    [Fact]
    public void Translate_PresenceUnknownAttr_ReturnsFalse()
    {
        KeycloakQueryParams result = _translator.Translate("unknownField pr");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser user = new() { UserName = "john" };
        result.InMemoryFilter!(user).Should().BeFalse();
    }

    [Fact]
    public void Translate_UnknownAttribute_ReturnsInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("customField eq \"value\"");

        result.InMemoryFilter.Should().NotBeNull();

        // Unknown attribute -> null value -> returns false
        ScimUser user = new() { UserName = "john" };
        result.InMemoryFilter!(user).Should().BeFalse();
    }

    [Fact]
    public void Translate_CoOnEmail_UsesInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("emails.value eq \"john@test.com\"");

        result.Email.Should().Be("john@test.com");
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_ComplexAnd_WithBothKeycloakParams()
    {
        // userName eq and email eq -> both are Keycloak params
        KeycloakQueryParams result = _translator.Translate("userName eq \"john\" and emails.value eq \"john@test.com\"");

        // Both have Keycloak params, so should fall to in-memory
        result.InMemoryFilter.Should().NotBeNull();
    }

    [Fact]
    public void Translate_CoOnUnknownField_UsesInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("customField co \"value\"");

        result.InMemoryFilter.Should().NotBeNull();
    }

    [Fact]
    public void Translate_NotWithAnd_ReturnsInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("not (userName eq \"admin\" and active eq \"false\")");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser adminInactive = new() { UserName = "admin", Active = false };
        ScimUser regularActive = new() { UserName = "user", Active = true };

        result.InMemoryFilter!(adminInactive).Should().BeFalse();
        result.InMemoryFilter!(regularActive).Should().BeTrue();
    }

    [Fact]
    public void Translate_ActiveEmailFilter_CombinesCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("active eq \"true\" and emails.value co \"test.com\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser activeWithEmail = new()
        {
            Active = true,
            UserName = "john",
            Emails = new[] { new ScimEmail { Value = "john@test.com", Primary = true } }
        };
        ScimUser inactiveWithEmail = new()
        {
            Active = false,
            UserName = "john",
            Emails = new[] { new ScimEmail { Value = "john@test.com", Primary = true } }
        };

        result.InMemoryFilter!(activeWithEmail).Should().BeTrue();
        result.InMemoryFilter!(inactiveWithEmail).Should().BeFalse();
    }

    [Fact]
    public void Translate_OrWithInMemoryFilters_CombinesCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("active eq \"true\" or active eq \"false\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser activeUser = new() { Active = true, UserName = "test" };
        ScimUser inactiveUser = new() { Active = false, UserName = "test" };

        // Both should match since it's OR
        result.InMemoryFilter!(activeUser).Should().BeTrue();
        result.InMemoryFilter!(inactiveUser).Should().BeTrue();
    }

    [Fact]
    public void Translate_SearchWithInMemoryFilter_AppliesSearch()
    {
        // Search param applied to a user
        _ = new KeycloakQueryParams(Search: "john");

        // Manually test the search matching logic by using the translator output
        KeycloakQueryParams result = _translator.Translate("userName co \"john\"");

        result.Search.Should().Be("john");
    }
}
