using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Infrastructure.Scim;

namespace Wallow.Identity.Tests.Infrastructure;

public class ScimAttributeMapperTests
{
    private readonly ScimAttributeMapper _sut = new ScimAttributeMapper();

    [Fact]
    public void Translate_NullFilter_ReturnsEmptyParams()
    {
        ScimFilterParams result = _sut.Translate(null);

        result.UserName.Should().BeNull();
        result.Email.Should().BeNull();
        result.FirstName.Should().BeNull();
        result.LastName.Should().BeNull();
        result.Search.Should().BeNull();
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_EmptyFilter_ReturnsEmptyParams()
    {
        ScimFilterParams result = _sut.Translate("");

        result.UserName.Should().BeNull();
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_WhitespaceFilter_ReturnsEmptyParams()
    {
        ScimFilterParams result = _sut.Translate("   ");

        result.UserName.Should().BeNull();
    }

    [Fact]
    public void Translate_UserNameEq_SetsUserName()
    {
        ScimFilterParams result = _sut.Translate("userName eq \"john\"");

        result.UserName.Should().Be("john");
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_EmailsValueEq_SetsEmail()
    {
        ScimFilterParams result = _sut.Translate("emails.value eq \"test@example.com\"");

        result.Email.Should().Be("test@example.com");
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_NameGivenNameEq_SetsFirstName()
    {
        ScimFilterParams result = _sut.Translate("name.givenName eq \"John\"");

        result.FirstName.Should().Be("John");
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_NameFamilyNameEq_SetsLastName()
    {
        ScimFilterParams result = _sut.Translate("name.familyName eq \"Doe\"");

        result.LastName.Should().Be("Doe");
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_UserNameCo_SetsSearch()
    {
        ScimFilterParams result = _sut.Translate("userName co \"john\"");

        result.Search.Should().Be("john");
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_EmailCo_SetsSearch()
    {
        ScimFilterParams result = _sut.Translate("emails.value co \"example\"");

        result.Search.Should().Be("example");
    }

    [Fact]
    public void Translate_UnknownAttributeEq_CreatesInMemoryFilter()
    {
        ScimFilterParams result = _sut.Translate("active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();
        result.UserName.Should().BeNull();
    }

    [Fact]
    public void Translate_UnknownOperator_CreatesInMemoryFilter()
    {
        ScimFilterParams result = _sut.Translate("userName sw \"j\"");

        result.InMemoryFilter.Should().NotBeNull();
    }

    [Fact]
    public void Translate_PresenceNode_CreatesInMemoryFilter()
    {
        ScimFilterParams result = _sut.Translate("userName pr");

        result.InMemoryFilter.Should().NotBeNull();
    }

    [Fact]
    public void Translate_PresenceNode_UserName_MatchesUserWithUserName()
    {
        ScimFilterParams result = _sut.Translate("userName pr");

        ScimUser user = CreateScimUser(userName: "john");
        result.InMemoryFilter!(user).Should().BeTrue();
    }

    [Fact]
    public void Translate_PresenceNode_UserName_DoesNotMatchEmptyUserName()
    {
        ScimFilterParams result = _sut.Translate("userName pr");

        ScimUser user = CreateScimUser(userName: "");
        result.InMemoryFilter!(user).Should().BeFalse();
    }

    [Fact]
    public void Translate_PresenceNode_Email_MatchesUserWithEmails()
    {
        ScimFilterParams result = _sut.Translate("emails.value pr");

        ScimUser user = CreateScimUser(email: "test@example.com");
        result.InMemoryFilter!(user).Should().BeTrue();
    }

    [Fact]
    public void Translate_PresenceNode_Email_DoesNotMatchUserWithoutEmails()
    {
        ScimFilterParams result = _sut.Translate("emails.value pr");

        ScimUser user = CreateScimUser();
        result.InMemoryFilter!(user).Should().BeFalse();
    }

    [Fact]
    public void Translate_PresenceNode_FirstName_MatchesUserWithGivenName()
    {
        ScimFilterParams result = _sut.Translate("name.givenName pr");

        ScimUser user = CreateScimUser(firstName: "John");
        result.InMemoryFilter!(user).Should().BeTrue();
    }

    [Fact]
    public void Translate_PresenceNode_LastName_MatchesUserWithFamilyName()
    {
        ScimFilterParams result = _sut.Translate("name.familyName pr");

        ScimUser user = CreateScimUser(lastName: "Doe");
        result.InMemoryFilter!(user).Should().BeTrue();
    }

    [Fact]
    public void Translate_PresenceNode_UnknownAttribute_ReturnsFalse()
    {
        ScimFilterParams result = _sut.Translate("unknownAttr pr");

        ScimUser user = CreateScimUser(userName: "john");
        result.InMemoryFilter!(user).Should().BeFalse();
    }

    [Fact]
    public void Translate_NotNode_NegatesInnerExpression()
    {
        ScimFilterParams result = _sut.Translate("not (userName eq \"john\")");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser john = CreateScimUser(userName: "john");
        ScimUser jane = CreateScimUser(userName: "jane");

        result.InMemoryFilter!(john).Should().BeFalse();
        result.InMemoryFilter!(jane).Should().BeTrue();
    }

    [Fact]
    public void Translate_LogicalAnd_BothDbParams_CreatesCombinedInMemoryFilter()
    {
        // When both sides of AND have db params, the mapper falls back to in-memory
        // because both HasAnyParam checks return true
        ScimFilterParams result = _sut.Translate("userName eq \"john\" and name.familyName eq \"Doe\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser johnDoe = CreateScimUser(userName: "john", lastName: "Doe");
        ScimUser johnSmith = CreateScimUser(userName: "john", lastName: "Smith");

        result.InMemoryFilter!(johnDoe).Should().BeTrue();
        result.InMemoryFilter!(johnSmith).Should().BeFalse();
    }

    [Fact]
    public void Translate_LogicalAnd_WithInMemoryFilter_CreatesCombinedFilter()
    {
        ScimFilterParams result = _sut.Translate("userName eq \"john\" and active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();
    }

    [Fact]
    public void Translate_LogicalOr_TwoDbParams_CreatesCombinedFilter()
    {
        // Two different db params in an OR need in-memory evaluation
        ScimFilterParams result = _sut.Translate("userName eq \"john\" or userName eq \"jane\"");

        result.InMemoryFilter.Should().NotBeNull();
    }

    [Fact]
    public void Translate_LogicalAnd_CombinedFilter_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("userName eq \"john\" and active eq \"true\"");

        ScimUser activeJohn = CreateScimUser(userName: "john", active: true);
        ScimUser inactiveJohn = CreateScimUser(userName: "john", active: false);
        ScimUser activeJane = CreateScimUser(userName: "jane", active: true);

        result.InMemoryFilter!(activeJohn).Should().BeTrue();
        result.InMemoryFilter!(inactiveJohn).Should().BeFalse();
        result.InMemoryFilter!(activeJane).Should().BeFalse();
    }

    [Fact]
    public void Translate_LogicalOr_CombinedFilter_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("userName eq \"john\" or userName eq \"jane\"");

        ScimUser john = CreateScimUser(userName: "john");
        ScimUser jane = CreateScimUser(userName: "jane");
        ScimUser bob = CreateScimUser(userName: "bob");

        result.InMemoryFilter!(john).Should().BeTrue();
        result.InMemoryFilter!(jane).Should().BeTrue();
        result.InMemoryFilter!(bob).Should().BeFalse();
    }

    [Fact]
    public void Translate_NeOperator_InMemoryFilter_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("userName ne \"john\"");

        ScimUser john = CreateScimUser(userName: "john");
        ScimUser jane = CreateScimUser(userName: "jane");

        result.InMemoryFilter!(john).Should().BeFalse();
        result.InMemoryFilter!(jane).Should().BeTrue();
    }

    [Fact]
    public void Translate_SwOperator_InMemoryFilter_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("userName sw \"jo\"");

        ScimUser john = CreateScimUser(userName: "john");
        ScimUser jane = CreateScimUser(userName: "jane");

        result.InMemoryFilter!(john).Should().BeTrue();
        result.InMemoryFilter!(jane).Should().BeFalse();
    }

    [Fact]
    public void Translate_EwOperator_InMemoryFilter_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("userName ew \"hn\"");

        ScimUser john = CreateScimUser(userName: "john");
        ScimUser jane = CreateScimUser(userName: "jane");

        result.InMemoryFilter!(john).Should().BeTrue();
        result.InMemoryFilter!(jane).Should().BeFalse();
    }

    [Fact]
    public void Translate_GtOperator_InMemoryFilter_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("userName gt \"jane\"");

        ScimUser john = CreateScimUser(userName: "john");
        ScimUser alice = CreateScimUser(userName: "alice");

        result.InMemoryFilter!(john).Should().BeTrue();
        result.InMemoryFilter!(alice).Should().BeFalse();
    }

    [Fact]
    public void Translate_GeOperator_InMemoryFilter_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("userName ge \"john\"");

        ScimUser john = CreateScimUser(userName: "john");
        ScimUser alice = CreateScimUser(userName: "alice");

        result.InMemoryFilter!(john).Should().BeTrue();
        result.InMemoryFilter!(alice).Should().BeFalse();
    }

    [Fact]
    public void Translate_LtOperator_InMemoryFilter_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("userName lt \"john\"");

        ScimUser alice = CreateScimUser(userName: "alice");
        ScimUser zara = CreateScimUser(userName: "zara");

        result.InMemoryFilter!(alice).Should().BeTrue();
        result.InMemoryFilter!(zara).Should().BeFalse();
    }

    [Fact]
    public void Translate_LeOperator_InMemoryFilter_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("userName le \"john\"");

        ScimUser john = CreateScimUser(userName: "john");
        ScimUser zara = CreateScimUser(userName: "zara");

        result.InMemoryFilter!(john).Should().BeTrue();
        result.InMemoryFilter!(zara).Should().BeFalse();
    }

    [Fact]
    public void Translate_CoOperator_UnknownAttribute_CreatesInMemoryFilter()
    {
        ScimFilterParams result = _sut.Translate("active co \"tr\"");

        result.InMemoryFilter.Should().NotBeNull();
    }

    [Fact]
    public void Translate_ActiveEqBoolean_InMemoryFilter_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("active eq true");

        ScimUser activeUser = CreateScimUser(active: true);
        ScimUser inactiveUser = CreateScimUser(active: false);

        result.InMemoryFilter!(activeUser).Should().BeTrue();
        result.InMemoryFilter!(inactiveUser).Should().BeFalse();
    }

    [Fact]
    public void Translate_KnownAttributeEq_SetsDbParam_NotInMemoryFilter()
    {
        ScimFilterParams result = _sut.Translate("name.givenName eq \"John\"");

        result.InMemoryFilter.Should().BeNull();
        result.FirstName.Should().Be("John");
    }

    [Fact]
    public void Translate_UnknownOperator_OnUnknownAttribute_ReturnsFalse()
    {
        ScimFilterParams result = _sut.Translate("unknownAttr eq \"value\"");

        ScimUser user = CreateScimUser(userName: "test");
        result.InMemoryFilter!(user).Should().BeFalse();
    }

    [Fact]
    public void Translate_NotLogicalNode_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("not (userName eq \"john\" and active eq \"true\")");

        ScimUser activeJohn = CreateScimUser(userName: "john", active: true);
        ScimUser inactiveJohn = CreateScimUser(userName: "john", active: false);

        result.InMemoryFilter!(activeJohn).Should().BeFalse();
        result.InMemoryFilter!(inactiveJohn).Should().BeTrue();
    }

    [Fact]
    public void Translate_NotPresenceNode_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("not (userName pr)");

        ScimUser userWithName = CreateScimUser(userName: "john");
        ScimUser userWithoutName = CreateScimUser(userName: "");

        result.InMemoryFilter!(userWithName).Should().BeFalse();
        result.InMemoryFilter!(userWithoutName).Should().BeTrue();
    }

    [Fact]
    public void Translate_ComplexLogicalExpression_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("userName eq \"john\" or userName eq \"jane\" and active eq \"true\"");

        // 'and' has higher precedence than 'or', so: john OR (jane AND active)
        ScimUser activeJohn = CreateScimUser(userName: "john", active: true);
        ScimUser activeJane = CreateScimUser(userName: "jane", active: true);
        ScimUser inactiveJane = CreateScimUser(userName: "jane", active: false);

        result.InMemoryFilter!(activeJohn).Should().BeTrue();
        result.InMemoryFilter!(activeJane).Should().BeTrue();
        result.InMemoryFilter!(inactiveJane).Should().BeFalse();
    }

    private static ScimUser CreateScimUser(
        string userName = "",
        string? email = null,
        string? firstName = null,
        string? lastName = null,
        bool active = true)
    {
        return new ScimUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = userName,
            Name = (firstName != null || lastName != null)
                ? new ScimName { GivenName = firstName, FamilyName = lastName }
                : null,
            Emails = email != null
                ? [new ScimEmail { Value = email, Type = "work", Primary = true }]
                : null,
            Active = active
        };
    }
}
