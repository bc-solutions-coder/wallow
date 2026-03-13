using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Infrastructure.Scim;

namespace Foundry.Identity.Tests.Infrastructure;

public class ScimToKeycloakTranslatorGapTests
{
    private readonly ScimToKeycloakTranslator _translator = new ScimToKeycloakTranslator();

    #region Null/Empty/Whitespace Filter Edge Cases

    [Fact]
    public void Translate_WhitespaceOnlyFilter_ReturnsEmptyParams()
    {
        KeycloakQueryParams result = _translator.Translate("   ");

        result.Username.Should().BeNull();
        result.Email.Should().BeNull();
        result.FirstName.Should().BeNull();
        result.LastName.Should().BeNull();
        result.Search.Should().BeNull();
        result.InMemoryFilter.Should().BeNull();
    }

    #endregion

    #region Email Attribute Path Variants

    [Fact]
    public void Translate_EmailsValueEq_ReturnsEmailParam()
    {
        KeycloakQueryParams result = _translator.Translate("emails.value eq \"work@example.com\"");

        result.Email.Should().Be("work@example.com");
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_EmailsValueNe_ReturnsInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("emails.value ne \"old@example.com\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser matchingUser = new ScimUser()
        {
            UserName = "test",
            Emails = new[] { new ScimEmail { Value = "new@example.com", Primary = true } }
        };
        ScimUser nonMatchingUser = new ScimUser()
        {
            UserName = "test",
            Emails = new[] { new ScimEmail { Value = "old@example.com", Primary = true } }
        };

        result.InMemoryFilter!(matchingUser).Should().BeTrue();
        result.InMemoryFilter!(nonMatchingUser).Should().BeFalse();
    }

    #endregion

    #region In-Memory Comparison: Email Attribute

    [Fact]
    public void Translate_EmailSwOperator_UsesInMemoryWithEmailValue()
    {
        KeycloakQueryParams result = _translator.Translate("emails.value sw \"john\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser userWithEmail = new ScimUser()
        {
            UserName = "test",
            Emails = new[] { new ScimEmail { Value = "john@example.com", Primary = true } }
        };
        ScimUser userWithDifferentEmail = new ScimUser()
        {
            UserName = "test",
            Emails = new[] { new ScimEmail { Value = "alice@example.com", Primary = true } }
        };
        ScimUser userWithNoEmails = new ScimUser() { UserName = "test" };

        result.InMemoryFilter!(userWithEmail).Should().BeTrue();
        result.InMemoryFilter!(userWithDifferentEmail).Should().BeFalse();
        result.InMemoryFilter!(userWithNoEmails).Should().BeFalse();
    }

    [Fact]
    public void Translate_EmailEwOperator_UsesInMemoryWithEmailValue()
    {
        KeycloakQueryParams result = _translator.Translate("emails.value ew \"example.com\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser userWithEmail = new ScimUser()
        {
            UserName = "test",
            Emails = new[] { new ScimEmail { Value = "john@example.com", Primary = true } }
        };

        result.InMemoryFilter!(userWithEmail).Should().BeTrue();
    }

    [Fact]
    public void Translate_EmailEmptyList_ReturnsNullUserValue()
    {
        KeycloakQueryParams result = _translator.Translate("emails.value eq \"test@example.com\"");

        // When the filter resolves directly to Email param, test in-memory via sw to force in-memory path
        KeycloakQueryParams swResult = _translator.Translate("emails.value sw \"test\"");

        ScimUser userWithEmptyEmails = new ScimUser()
        {
            UserName = "test",
            Emails = Array.Empty<ScimEmail>()
        };

        swResult.InMemoryFilter!(userWithEmptyEmails).Should().BeFalse();
    }

    #endregion

    #region In-Memory Comparison: Active Attribute

    [Fact]
    public void Translate_ActiveNeOperator_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("active ne \"true\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser activeUser = new ScimUser() { Active = true, UserName = "test" };
        ScimUser inactiveUser = new ScimUser() { Active = false, UserName = "test" };

        result.InMemoryFilter!(activeUser).Should().BeFalse();
        result.InMemoryFilter!(inactiveUser).Should().BeTrue();
    }

    [Fact]
    public void Translate_ActiveCoOperator_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("active co \"tru\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser activeUser = new ScimUser() { Active = true, UserName = "test" };
        ScimUser inactiveUser = new ScimUser() { Active = false, UserName = "test" };

        result.InMemoryFilter!(activeUser).Should().BeTrue();
        result.InMemoryFilter!(inactiveUser).Should().BeFalse();
    }

    #endregion

    #region In-Memory Comparison: FirstName and LastName

    [Fact]
    public void Translate_FirstNameSwOperator_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("name.givenName sw \"Jo\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser matching = new ScimUser() { UserName = "john", Name = new ScimName { GivenName = "John" } };
        ScimUser nonMatching = new ScimUser() { UserName = "alice", Name = new ScimName { GivenName = "Alice" } };
        ScimUser noName = new ScimUser() { UserName = "test" };

        result.InMemoryFilter!(matching).Should().BeTrue();
        result.InMemoryFilter!(nonMatching).Should().BeFalse();
        result.InMemoryFilter!(noName).Should().BeFalse();
    }

    [Fact]
    public void Translate_LastNameEwOperator_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("name.familyName ew \"oe\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser matching = new ScimUser() { UserName = "john", Name = new ScimName { FamilyName = "Doe" } };
        ScimUser nonMatching = new ScimUser() { UserName = "alice", Name = new ScimName { FamilyName = "Smith" } };

        result.InMemoryFilter!(matching).Should().BeTrue();
        result.InMemoryFilter!(nonMatching).Should().BeFalse();
    }

    #endregion

    #region Unknown Operator Fallback

    [Fact]
    public void Translate_UnknownOperator_ReturnsFalseInMemoryFilter()
    {
        // Construct a filter that exercises an unknown operator by using a complex filter
        // that will produce an in-memory filter with unknown attribute
        KeycloakQueryParams result = _translator.Translate("unknownAttr sw \"val\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser user = new ScimUser() { UserName = "test" };

        // Unknown attribute resolves to null userValue, so returns false
        result.InMemoryFilter!(user).Should().BeFalse();
    }

    #endregion

    #region Logical Combinations: Merge Params (non-overlapping simple params)

    [Fact]
    public void Translate_AndWithNonOverlappingSimpleParams_MergesCorrectly()
    {
        // One side has username, other side has in-memory (active)
        // When one side has in-memory, both go to combined in-memory
        KeycloakQueryParams result = _translator.Translate("userName eq \"john\" and active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser match = new ScimUser() { UserName = "john", Active = true };
        ScimUser wrongActive = new ScimUser() { UserName = "john", Active = false };

        result.InMemoryFilter!(match).Should().BeTrue();
        result.InMemoryFilter!(wrongActive).Should().BeFalse();
    }

    [Fact]
    public void Translate_OrWithBothInMemory_CombinesCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("active eq \"true\" or userName sw \"admin\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser activeUser = new ScimUser() { Active = true, UserName = "regular" };
        ScimUser adminUser = new ScimUser() { Active = false, UserName = "admin_user" };
        ScimUser neitherUser = new ScimUser() { Active = false, UserName = "regular" };

        result.InMemoryFilter!(activeUser).Should().BeTrue();
        result.InMemoryFilter!(adminUser).Should().BeTrue();
        result.InMemoryFilter!(neitherUser).Should().BeFalse();
    }

    #endregion

    #region ApplyFilter: Simple Keycloak Params In Combined Context

    [Fact]
    public void Translate_AndWithUsernameAndInMemory_AppliesUsernameMatch()
    {
        KeycloakQueryParams result = _translator.Translate("userName eq \"john\" and active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser caseInsensitiveMatch = new ScimUser() { UserName = "JOHN", Active = true };

        // ApplyFilter uses OrdinalIgnoreCase for username comparison
        result.InMemoryFilter!(caseInsensitiveMatch).Should().BeTrue();
    }

    [Fact]
    public void Translate_AndWithEmailAndInMemory_AppliesEmailMatch()
    {
        KeycloakQueryParams result = _translator.Translate("emails.value eq \"john@test.com\" and active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser matchingUser = new ScimUser()
        {
            UserName = "john",
            Active = true,
            Emails = new[] { new ScimEmail { Value = "john@test.com", Primary = true } }
        };
        ScimUser noEmailUser = new ScimUser()
        {
            UserName = "john",
            Active = true
        };

        result.InMemoryFilter!(matchingUser).Should().BeTrue();
        result.InMemoryFilter!(noEmailUser).Should().BeFalse();
    }

    [Fact]
    public void Translate_AndWithFirstNameAndInMemory_AppliesFirstNameMatch()
    {
        KeycloakQueryParams result = _translator.Translate("name.givenName eq \"John\" and active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser matching = new ScimUser()
        {
            UserName = "john",
            Active = true,
            Name = new ScimName { GivenName = "John" }
        };
        ScimUser wrongName = new ScimUser()
        {
            UserName = "john",
            Active = true,
            Name = new ScimName { GivenName = "Jane" }
        };

        result.InMemoryFilter!(matching).Should().BeTrue();
        result.InMemoryFilter!(wrongName).Should().BeFalse();
    }

    [Fact]
    public void Translate_AndWithLastNameAndInMemory_AppliesLastNameMatch()
    {
        KeycloakQueryParams result = _translator.Translate("name.familyName eq \"Doe\" and active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser matching = new ScimUser()
        {
            UserName = "john",
            Active = true,
            Name = new ScimName { FamilyName = "Doe" }
        };
        ScimUser noLastName = new ScimUser()
        {
            UserName = "john",
            Active = true,
            Name = new ScimName { FamilyName = null }
        };

        result.InMemoryFilter!(matching).Should().BeTrue();
        result.InMemoryFilter!(noLastName).Should().BeFalse();
    }

    [Fact]
    public void Translate_AndWithSearchAndInMemory_AppliesSearchMatch()
    {
        KeycloakQueryParams result = _translator.Translate("userName co \"john\" and active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser matchByUsername = new ScimUser() { UserName = "john_doe", Active = true };
        ScimUser matchByEmail = new ScimUser()
        {
            UserName = "other",
            Active = true,
            Emails = new[] { new ScimEmail { Value = "john@test.com", Primary = true } }
        };
        ScimUser matchByFirstName = new ScimUser()
        {
            UserName = "other",
            Active = true,
            Name = new ScimName { GivenName = "John" }
        };
        ScimUser matchByLastName = new ScimUser()
        {
            UserName = "other",
            Active = true,
            Name = new ScimName { FamilyName = "Johnson" }
        };
        ScimUser noMatch = new ScimUser() { UserName = "other", Active = true };

        result.InMemoryFilter!(matchByUsername).Should().BeTrue();
        result.InMemoryFilter!(matchByEmail).Should().BeTrue();
        result.InMemoryFilter!(matchByFirstName).Should().BeTrue();
        result.InMemoryFilter!(matchByLastName).Should().BeTrue();
        result.InMemoryFilter!(noMatch).Should().BeFalse();
    }

    #endregion

    #region Not Filter with Presence and Logical Nodes

    [Fact]
    public void Translate_NotPresenceFilter_ReturnsInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("not (userName pr)");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser withUsername = new ScimUser() { UserName = "john" };
        ScimUser withoutUsername = new ScimUser() { UserName = "" };

        result.InMemoryFilter!(withUsername).Should().BeFalse();
        result.InMemoryFilter!(withoutUsername).Should().BeTrue();
    }

    [Fact]
    public void Translate_NotOrFilter_ReturnsInMemoryFilter()
    {
        KeycloakQueryParams result = _translator.Translate("not (userName eq \"admin\" or userName eq \"root\")");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser admin = new ScimUser() { UserName = "admin" };
        ScimUser root = new ScimUser() { UserName = "root" };
        ScimUser regular = new ScimUser() { UserName = "user" };

        result.InMemoryFilter!(admin).Should().BeFalse();
        result.InMemoryFilter!(root).Should().BeFalse();
        result.InMemoryFilter!(regular).Should().BeTrue();
    }

    #endregion

    #region EvaluateFilter: Presence in Nested Context

    [Fact]
    public void Translate_NotPresenceEmail_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("not (emails.value pr)");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser withEmail = new ScimUser()
        {
            UserName = "john",
            Emails = new[] { new ScimEmail { Value = "john@test.com", Primary = true } }
        };
        ScimUser withoutEmail = new ScimUser() { UserName = "john" };

        result.InMemoryFilter!(withEmail).Should().BeFalse();
        result.InMemoryFilter!(withoutEmail).Should().BeTrue();
    }

    [Fact]
    public void Translate_NotPresenceUnknownAttr_ReturnsTrue()
    {
        KeycloakQueryParams result = _translator.Translate("not (unknownField pr)");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser user = new ScimUser() { UserName = "john" };

        // not(false) = true
        result.InMemoryFilter!(user).Should().BeTrue();
    }

    [Fact]
    public void Translate_NotPresenceFirstName_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("not (name.givenName pr)");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser withName = new ScimUser() { UserName = "john", Name = new ScimName { GivenName = "John" } };
        ScimUser withoutName = new ScimUser() { UserName = "john" };

        result.InMemoryFilter!(withName).Should().BeFalse();
        result.InMemoryFilter!(withoutName).Should().BeTrue();
    }

    [Fact]
    public void Translate_NotPresenceLastName_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("not (name.familyName pr)");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser withName = new ScimUser() { UserName = "john", Name = new ScimName { FamilyName = "Doe" } };
        ScimUser withoutName = new ScimUser() { UserName = "john", Name = new ScimName { FamilyName = "" } };

        result.InMemoryFilter!(withName).Should().BeFalse();
        result.InMemoryFilter!(withoutName).Should().BeTrue();
    }

    #endregion

    #region Complex Nested Logical Filters

    [Fact]
    public void Translate_NestedAndOr_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate(
            "(userName eq \"john\" and active eq \"true\") or userName eq \"admin\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser activeJohn = new ScimUser() { UserName = "john", Active = true };
        ScimUser inactiveJohn = new ScimUser() { UserName = "john", Active = false };
        ScimUser admin = new ScimUser() { UserName = "admin", Active = false };
        ScimUser other = new ScimUser() { UserName = "other", Active = true };

        result.InMemoryFilter!(activeJohn).Should().BeTrue();
        result.InMemoryFilter!(inactiveJohn).Should().BeFalse();
        result.InMemoryFilter!(admin).Should().BeTrue();
        result.InMemoryFilter!(other).Should().BeFalse();
    }

    [Fact]
    public void Translate_DoubleNot_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("not (not (userName eq \"admin\"))");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser admin = new ScimUser() { UserName = "admin" };
        ScimUser regular = new ScimUser() { UserName = "user" };

        result.InMemoryFilter!(admin).Should().BeTrue();
        result.InMemoryFilter!(regular).Should().BeFalse();
    }

    #endregion

    #region Multi-Value Email Filtering

    [Fact]
    public void Translate_EmailInMemory_UsesFirstEmailValue()
    {
        KeycloakQueryParams result = _translator.Translate("emails.value sw \"work\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser userWithMultipleEmails = new ScimUser()
        {
            UserName = "test",
            Emails = new[]
            {
                new ScimEmail { Value = "work@example.com", Type = "work", Primary = true },
                new ScimEmail { Value = "personal@example.com", Type = "home", Primary = false }
            }
        };

        // EvaluateComparison uses Emails[0].Value
        result.InMemoryFilter!(userWithMultipleEmails).Should().BeTrue();
    }

    [Fact]
    public void Translate_EmailInMemory_FirstEmailDoesNotMatch_ReturnsFalse()
    {
        KeycloakQueryParams result = _translator.Translate("emails.value sw \"personal\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser userWithMultipleEmails = new ScimUser()
        {
            UserName = "test",
            Emails = new[]
            {
                new ScimEmail { Value = "work@example.com", Type = "work", Primary = true },
                new ScimEmail { Value = "personal@example.com", Type = "home", Primary = false }
            }
        };

        // Only checks Emails[0], so "personal" won't match
        result.InMemoryFilter!(userWithMultipleEmails).Should().BeFalse();
    }

    #endregion

    #region ApplyFilter: Email Matching Uses Any()

    [Fact]
    public void Translate_CombinedEmailParam_MatchesAnyEmail()
    {
        KeycloakQueryParams result = _translator.Translate(
            "emails.value eq \"second@test.com\" and active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser userWithSecondEmail = new ScimUser()
        {
            UserName = "test",
            Active = true,
            Emails = new[]
            {
                new ScimEmail { Value = "first@test.com", Primary = true },
                new ScimEmail { Value = "second@test.com", Primary = false }
            }
        };

        // ApplyFilter uses Any() for email matching, so second email matches
        result.InMemoryFilter!(userWithSecondEmail).Should().BeTrue();
    }

    #endregion

    #region Comparison Operators on Non-Username Attributes

    [Fact]
    public void Translate_FirstNameGtOperator_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("name.givenName gt \"B\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser greaterUser = new ScimUser() { UserName = "test", Name = new ScimName { GivenName = "Charlie" } };
        ScimUser lesserUser = new ScimUser() { UserName = "test", Name = new ScimName { GivenName = "Alice" } };

        result.InMemoryFilter!(greaterUser).Should().BeTrue();
        result.InMemoryFilter!(lesserUser).Should().BeFalse();
    }

    [Fact]
    public void Translate_LastNameLeOperator_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("name.familyName le \"Doe\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser equalUser = new ScimUser() { UserName = "test", Name = new ScimName { FamilyName = "Doe" } };
        ScimUser lesserUser = new ScimUser() { UserName = "test", Name = new ScimName { FamilyName = "Brown" } };
        ScimUser greaterUser = new ScimUser() { UserName = "test", Name = new ScimName { FamilyName = "Smith" } };

        result.InMemoryFilter!(equalUser).Should().BeTrue();
        result.InMemoryFilter!(lesserUser).Should().BeTrue();
        result.InMemoryFilter!(greaterUser).Should().BeFalse();
    }

    [Fact]
    public void Translate_EmailLtOperator_FiltersCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("emails.value lt \"m\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser lesserUser = new ScimUser()
        {
            UserName = "test",
            Emails = new[] { new ScimEmail { Value = "alice@example.com" } }
        };
        ScimUser greaterUser = new ScimUser()
        {
            UserName = "test",
            Emails = new[] { new ScimEmail { Value = "zack@example.com" } }
        };

        result.InMemoryFilter!(lesserUser).Should().BeTrue();
        result.InMemoryFilter!(greaterUser).Should().BeFalse();
    }

    #endregion

    #region NormalizeAttributePath: Case Insensitivity

    [Fact]
    public void Translate_UpperCaseUserName_NormalizesCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("USERNAME eq \"test\"");

        // Should normalize to "username" and map to Username param
        result.Username.Should().Be("test");
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_MixedCaseGivenName_NormalizesCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("Name.GivenName eq \"John\"");

        result.FirstName.Should().Be("John");
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_MixedCaseFamilyName_NormalizesCorrectly()
    {
        KeycloakQueryParams result = _translator.Translate("Name.FamilyName eq \"Doe\"");

        result.LastName.Should().Be("Doe");
        result.InMemoryFilter.Should().BeNull();
    }

    #endregion

    #region HasMultipleKeycloakParams Edge Cases

    [Fact]
    public void Translate_AndWithThreeKeycloakParams_FallsToInMemory()
    {
        KeycloakQueryParams result = _translator.Translate(
            "userName eq \"john\" and (name.givenName eq \"John\" and name.familyName eq \"Doe\")");

        // Multiple Keycloak params -> falls to in-memory
        result.InMemoryFilter.Should().NotBeNull();

        ScimUser fullMatch = new ScimUser()
        {
            UserName = "john",
            Name = new ScimName { GivenName = "John", FamilyName = "Doe" }
        };
        ScimUser partialMatch = new ScimUser()
        {
            UserName = "john",
            Name = new ScimName { GivenName = "Jane", FamilyName = "Doe" }
        };

        result.InMemoryFilter!(fullMatch).Should().BeTrue();
        result.InMemoryFilter!(partialMatch).Should().BeFalse();
    }

    #endregion

    #region Comparison with null Name

    [Fact]
    public void Translate_FirstNameEq_UserWithNullName_InMemoryReturnsFalse()
    {
        KeycloakQueryParams result = _translator.Translate("name.givenName sw \"Jo\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser userWithNullName = new ScimUser() { UserName = "test", Name = null };

        result.InMemoryFilter!(userWithNullName).Should().BeFalse();
    }

    [Fact]
    public void Translate_LastNameEq_UserWithNullFamilyName_InMemoryReturnsFalse()
    {
        KeycloakQueryParams result = _translator.Translate("name.familyName sw \"Do\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser userWithNullFamilyName = new ScimUser()
        {
            UserName = "test",
            Name = new ScimName { GivenName = "John", FamilyName = null }
        };

        result.InMemoryFilter!(userWithNullFamilyName).Should().BeFalse();
    }

    #endregion
}
