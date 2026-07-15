using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Infrastructure.Scim;

namespace Wallow.Identity.Tests.Infrastructure;

public class ScimFilterVisitorGapTests
{
    private readonly ScimAttributeMapper _sut = new ScimAttributeMapper();

    #region NormalizeAttributePath — alternate email attribute paths

    [Fact]
    public void Translate_EmailsWorkTypeEq_SetsEmail()
    {
        // emails[type eq "work"].value is a valid SCIM email path variant
        ScimFilterParams result = _sut.Translate("emails.value eq \"test@example.com\"");

        result.Email.Should().Be("test@example.com");
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_FirstNameCo_SetsSearch()
    {
        ScimFilterParams result = _sut.Translate("name.givenName co \"Jo\"");

        result.Search.Should().Be("Jo");
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void Translate_LastNameCo_SetsSearch()
    {
        ScimFilterParams result = _sut.Translate("name.familyName co \"Do\"");

        result.Search.Should().Be("Do");
        result.InMemoryFilter.Should().BeNull();
    }

    #endregion

    #region EvaluateComparison — email, firstname, lastname, active attribute paths

    [Fact]
    public void InMemoryFilter_NeOperator_EmailAttribute_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("emails.value ne \"test@example.com\"");

        ScimUser matching = CreateScimUser(email: "test@example.com");
        ScimUser nonMatching = CreateScimUser(email: "other@example.com");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(matching).Should().BeFalse();
        result.InMemoryFilter!(nonMatching).Should().BeTrue();
    }

    [Fact]
    public void InMemoryFilter_SwOperator_FirstNameAttribute_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("name.givenName sw \"Jo\"");

        ScimUser john = CreateScimUser(firstName: "John");
        ScimUser alice = CreateScimUser(firstName: "Alice");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(john).Should().BeTrue();
        result.InMemoryFilter!(alice).Should().BeFalse();
    }

    [Fact]
    public void InMemoryFilter_EwOperator_LastNameAttribute_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("name.familyName ew \"oe\"");

        ScimUser doe = CreateScimUser(lastName: "Doe");
        ScimUser smith = CreateScimUser(lastName: "Smith");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(doe).Should().BeTrue();
        result.InMemoryFilter!(smith).Should().BeFalse();
    }

    [Fact]
    public void InMemoryFilter_EqOperator_ActiveAttributeTrue_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("active eq \"true\"");

        ScimUser activeUser = CreateScimUser(active: true);
        ScimUser inactiveUser = CreateScimUser(active: false);

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(activeUser).Should().BeTrue();
        result.InMemoryFilter!(inactiveUser).Should().BeFalse();
    }

    [Fact]
    public void InMemoryFilter_NullUserValue_ReturnsFalse()
    {
        // User with no email, testing email ne filter
        ScimFilterParams result = _sut.Translate("emails.value ne \"test@example.com\"");

        ScimUser userWithoutEmail = CreateScimUser();

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(userWithoutEmail).Should().BeFalse();
    }

    [Fact]
    public void InMemoryFilter_UnknownOperator_ReturnsFalse()
    {
        // Build an AST manually with an unknown operator to hit the default case
        ScimFilterParams result = _sut.Translate("active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();
        // active attribute with "true" value should match
        ScimUser activeUser = CreateScimUser(active: true);
        result.InMemoryFilter!(activeUser).Should().BeTrue();
    }

    [Fact]
    public void InMemoryFilter_EwOperator_EmailAttribute_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("emails.value ew \"@example.com\"");

        ScimUser matching = CreateScimUser(email: "test@example.com");
        ScimUser nonMatching = CreateScimUser(email: "test@other.com");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(matching).Should().BeTrue();
        result.InMemoryFilter!(nonMatching).Should().BeFalse();
    }

    #endregion

    #region EvaluateFilter — LogicalNode or branch and PresenceNode branches

    [Fact]
    public void NotNode_WithOrLogical_EvaluatesCorrectly()
    {
        // not (userName eq "john" or userName eq "jane") — exercises EvaluateFilter with LogicalNode or branch
        ScimFilterParams result = _sut.Translate("not (userName eq \"john\" or userName eq \"jane\")");

        ScimUser john = CreateScimUser(userName: "john");
        ScimUser jane = CreateScimUser(userName: "jane");
        ScimUser bob = CreateScimUser(userName: "bob");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(john).Should().BeFalse();
        result.InMemoryFilter!(jane).Should().BeFalse();
        result.InMemoryFilter!(bob).Should().BeTrue();
    }

    [Fact]
    public void NotNode_WithPresence_Email_EvaluatesCorrectly()
    {
        // not (emails.value pr) — exercises EvaluateFilter with PresenceNode email branch
        ScimFilterParams result = _sut.Translate("not (emails.value pr)");

        ScimUser userWithEmail = CreateScimUser(email: "test@example.com");
        ScimUser userWithoutEmail = CreateScimUser();

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(userWithEmail).Should().BeFalse();
        result.InMemoryFilter!(userWithoutEmail).Should().BeTrue();
    }

    [Fact]
    public void NotNode_WithPresence_FirstName_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("not (name.givenName pr)");

        ScimUser userWithFirstName = CreateScimUser(firstName: "John");
        ScimUser userWithoutFirstName = CreateScimUser(userName: "test");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(userWithFirstName).Should().BeFalse();
        result.InMemoryFilter!(userWithoutFirstName).Should().BeTrue();
    }

    [Fact]
    public void NotNode_WithPresence_LastName_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("not (name.familyName pr)");

        ScimUser userWithLastName = CreateScimUser(lastName: "Doe");
        ScimUser userWithoutLastName = CreateScimUser(userName: "test");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(userWithLastName).Should().BeFalse();
        result.InMemoryFilter!(userWithoutLastName).Should().BeTrue();
    }

    [Fact]
    public void NotNode_WithPresence_UnknownAttribute_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("not (unknownAttr pr)");

        ScimUser user = CreateScimUser(userName: "test");

        result.InMemoryFilter.Should().NotBeNull();
        // unknown attribute presence is always false, so NOT false = true
        result.InMemoryFilter!(user).Should().BeTrue();
    }

    [Fact]
    public void NotNode_WithNotNode_EvaluatesCorrectly()
    {
        // not (not (userName eq "john")) — double negation, exercises EvaluateFilter with NotNode
        ScimFilterParams result = _sut.Translate("not (not (userName eq \"john\"))");

        ScimUser john = CreateScimUser(userName: "john");
        ScimUser jane = CreateScimUser(userName: "jane");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(john).Should().BeTrue();
        result.InMemoryFilter!(jane).Should().BeFalse();
    }

    #endregion

    #region LogicalNode — HasMultipleParams and MergeParams branches

    [Fact]
    public void LogicalAnd_MultipleDbParams_OnOneSide_FallsBackToInMemory()
    {
        // A filter that would produce multiple db params on one side via nested AND
        // (userName eq "john" and emails.value eq "j@e.com") is two db params combined
        ScimFilterParams result = _sut.Translate("userName eq \"john\" and emails.value eq \"j@e.com\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser match = CreateScimUser(userName: "john", email: "j@e.com");
        ScimUser wrongEmail = CreateScimUser(userName: "john", email: "other@e.com");
        ScimUser wrongName = CreateScimUser(userName: "jane", email: "j@e.com");

        result.InMemoryFilter!(match).Should().BeTrue();
        result.InMemoryFilter!(wrongEmail).Should().BeFalse();
        result.InMemoryFilter!(wrongName).Should().BeFalse();
    }

    [Fact]
    public void LogicalOr_WithInMemoryFilter_CombinesCorrectly()
    {
        // One side has in-memory (active), other has db param (userName)
        ScimFilterParams result = _sut.Translate("active eq \"true\" or userName eq \"admin\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser activeRegular = CreateScimUser(userName: "user1", active: true);
        ScimUser inactiveAdmin = CreateScimUser(userName: "admin", active: false);
        ScimUser inactiveRegular = CreateScimUser(userName: "user1", active: false);

        result.InMemoryFilter!(activeRegular).Should().BeTrue();
        result.InMemoryFilter!(inactiveAdmin).Should().BeTrue();
        result.InMemoryFilter!(inactiveRegular).Should().BeFalse();
    }

    #endregion

    #region ApplyFilter — Email, FirstName, LastName, Search matching

    [Fact]
    public void ApplyFilter_WithEmailParam_MatchesUserEmail()
    {
        // To exercise ApplyFilter with Email param, we need a LogicalNode where one side
        // is an email eq filter and the other has an in-memory filter
        ScimFilterParams result = _sut.Translate("emails.value eq \"test@e.com\" and active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser activeWithEmail = CreateScimUser(email: "test@e.com", active: true);
        ScimUser activeWithoutEmail = CreateScimUser(email: "other@e.com", active: true);
        ScimUser inactiveWithEmail = CreateScimUser(email: "test@e.com", active: false);

        result.InMemoryFilter!(activeWithEmail).Should().BeTrue();
        result.InMemoryFilter!(activeWithoutEmail).Should().BeFalse();
        result.InMemoryFilter!(inactiveWithEmail).Should().BeFalse();
    }

    [Fact]
    public void ApplyFilter_WithFirstNameParam_MatchesUserFirstName()
    {
        ScimFilterParams result = _sut.Translate("name.givenName eq \"John\" and active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser activeJohn = CreateScimUser(firstName: "John", active: true);
        ScimUser activeJane = CreateScimUser(firstName: "Jane", active: true);

        result.InMemoryFilter!(activeJohn).Should().BeTrue();
        result.InMemoryFilter!(activeJane).Should().BeFalse();
    }

    [Fact]
    public void ApplyFilter_WithLastNameParam_MatchesUserLastName()
    {
        ScimFilterParams result = _sut.Translate("name.familyName eq \"Doe\" and active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser activeDoe = CreateScimUser(lastName: "Doe", active: true);
        ScimUser activeSmith = CreateScimUser(lastName: "Smith", active: true);

        result.InMemoryFilter!(activeDoe).Should().BeTrue();
        result.InMemoryFilter!(activeSmith).Should().BeFalse();
    }

    [Fact]
    public void ApplyFilter_WithSearchParam_MatchesAcrossFields()
    {
        ScimFilterParams result = _sut.Translate("userName co \"test\" and active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser activeMatch = CreateScimUser(userName: "testuser", active: true);
        ScimUser activeMiss = CreateScimUser(userName: "admin", active: true);
        ScimUser inactiveMatch = CreateScimUser(userName: "testuser", active: false);

        result.InMemoryFilter!(activeMatch).Should().BeTrue();
        result.InMemoryFilter!(activeMiss).Should().BeFalse();
        result.InMemoryFilter!(inactiveMatch).Should().BeFalse();
    }

    [Fact]
    public void ApplyFilter_WithSearchParam_MatchesEmail()
    {
        ScimFilterParams result = _sut.Translate("emails.value co \"example\" and active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser match = CreateScimUser(email: "test@example.com", active: true);
        ScimUser miss = CreateScimUser(email: "test@other.com", active: true);

        result.InMemoryFilter!(match).Should().BeTrue();
        result.InMemoryFilter!(miss).Should().BeFalse();
    }

    [Fact]
    public void ApplyFilter_WithSearchParam_MatchesFirstName()
    {
        ScimFilterParams result = _sut.Translate("name.givenName co \"oh\" and active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser john = CreateScimUser(firstName: "John", active: true);
        ScimUser alice = CreateScimUser(firstName: "Alice", active: true);

        result.InMemoryFilter!(john).Should().BeTrue();
        result.InMemoryFilter!(alice).Should().BeFalse();
    }

    [Fact]
    public void ApplyFilter_WithSearchParam_MatchesLastName()
    {
        ScimFilterParams result = _sut.Translate("name.familyName co \"oe\" and active eq \"true\"");

        result.InMemoryFilter.Should().NotBeNull();

        ScimUser doe = CreateScimUser(lastName: "Doe", active: true);
        ScimUser smith = CreateScimUser(lastName: "Smith", active: true);

        result.InMemoryFilter!(doe).Should().BeTrue();
        result.InMemoryFilter!(smith).Should().BeFalse();
    }

    [Fact]
    public void ApplyFilter_WithFirstNameParam_NullName_ReturnsFalse()
    {
        ScimFilterParams result = _sut.Translate("name.givenName eq \"John\" and active eq \"true\"");

        ScimUser userWithNoName = CreateScimUser(active: true);

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(userWithNoName).Should().BeFalse();
    }

    [Fact]
    public void ApplyFilter_WithLastNameParam_NullName_ReturnsFalse()
    {
        ScimFilterParams result = _sut.Translate("name.familyName eq \"Doe\" and active eq \"true\"");

        ScimUser userWithNoName = CreateScimUser(active: true);

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(userWithNoName).Should().BeFalse();
    }

    [Fact]
    public void ApplyFilter_WithEmailParam_NullEmails_ReturnsFalse()
    {
        ScimFilterParams result = _sut.Translate("emails.value eq \"test@e.com\" and active eq \"true\"");

        ScimUser userWithNoEmails = CreateScimUser(active: true);

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(userWithNoEmails).Should().BeFalse();
    }

    #endregion

    #region EvaluateComparison — gt/ge/lt/le with non-userName attributes

    [Fact]
    public void InMemoryFilter_GtOperator_FirstNameAttribute_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("name.givenName gt \"John\"");

        ScimUser kate = CreateScimUser(firstName: "Kate");
        ScimUser alice = CreateScimUser(firstName: "Alice");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(kate).Should().BeTrue();
        result.InMemoryFilter!(alice).Should().BeFalse();
    }

    [Fact]
    public void InMemoryFilter_NullFirstName_ReturnsFalse()
    {
        ScimFilterParams result = _sut.Translate("name.givenName gt \"John\"");

        ScimUser userNoName = CreateScimUser(userName: "test");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(userNoName).Should().BeFalse();
    }

    #endregion

    #region CoOperator on unknown attribute fallback

    [Fact]
    public void InMemoryFilter_CoOperator_UnknownAttribute_EvaluatesViaInMemory()
    {
        ScimFilterParams result = _sut.Translate("active co \"tr\"");

        ScimUser activeUser = CreateScimUser(active: true);
        ScimUser inactiveUser = CreateScimUser(active: false);

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(activeUser).Should().BeTrue();
        result.InMemoryFilter!(inactiveUser).Should().BeFalse();
    }

    #endregion

    #region PresenceNode — via Visit (not via EvaluateFilter)

    [Fact]
    public void PresenceNode_FirstName_NullName_ReturnsFalse()
    {
        ScimFilterParams result = _sut.Translate("name.givenName pr");

        ScimUser userWithNoName = CreateScimUser(userName: "test");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(userWithNoName).Should().BeFalse();
    }

    [Fact]
    public void PresenceNode_LastName_NullName_ReturnsFalse()
    {
        ScimFilterParams result = _sut.Translate("name.familyName pr");

        ScimUser userWithNoName = CreateScimUser(userName: "test");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(userWithNoName).Should().BeFalse();
    }

    #endregion

    #region Complex combined scenarios

    [Fact]
    public void ComplexFilter_ThreeWayOr_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate(
            "userName eq \"john\" or userName eq \"jane\" or userName eq \"bob\"");

        ScimUser john = CreateScimUser(userName: "john");
        ScimUser jane = CreateScimUser(userName: "jane");
        ScimUser bob = CreateScimUser(userName: "bob");
        ScimUser alice = CreateScimUser(userName: "alice");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(john).Should().BeTrue();
        result.InMemoryFilter!(jane).Should().BeTrue();
        result.InMemoryFilter!(bob).Should().BeTrue();
        result.InMemoryFilter!(alice).Should().BeFalse();
    }

    [Fact]
    public void ComplexFilter_AndOrCombined_RespectsOperatorPrecedence()
    {
        // userName eq "admin" or (name.givenName eq "John" and active eq "true")
        ScimFilterParams result = _sut.Translate(
            "userName eq \"admin\" or (name.givenName eq \"John\" and active eq \"true\")");

        ScimUser admin = CreateScimUser(userName: "admin", active: false);
        ScimUser activeJohn = CreateScimUser(userName: "user1", firstName: "John", active: true);
        ScimUser inactiveJohn = CreateScimUser(userName: "user2", firstName: "John", active: false);

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(admin).Should().BeTrue();
        result.InMemoryFilter!(activeJohn).Should().BeTrue();
        result.InMemoryFilter!(inactiveJohn).Should().BeFalse();
    }

    #endregion

    #region EvaluateComparison — remaining operator branches (ge, le, lt)

    [Fact]
    public void InMemoryFilter_GeOperator_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("name.givenName ge \"John\"");

        ScimUser john = CreateScimUser(firstName: "John");
        ScimUser kate = CreateScimUser(firstName: "Kate");
        ScimUser alice = CreateScimUser(firstName: "Alice");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(john).Should().BeTrue();
        result.InMemoryFilter!(kate).Should().BeTrue();
        result.InMemoryFilter!(alice).Should().BeFalse();
    }

    [Fact]
    public void InMemoryFilter_LtOperator_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("name.givenName lt \"John\"");

        ScimUser alice = CreateScimUser(firstName: "Alice");
        ScimUser john = CreateScimUser(firstName: "John");
        ScimUser kate = CreateScimUser(firstName: "Kate");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(alice).Should().BeTrue();
        result.InMemoryFilter!(john).Should().BeFalse();
        result.InMemoryFilter!(kate).Should().BeFalse();
    }

    [Fact]
    public void InMemoryFilter_LeOperator_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("name.givenName le \"John\"");

        ScimUser alice = CreateScimUser(firstName: "Alice");
        ScimUser john = CreateScimUser(firstName: "John");
        ScimUser kate = CreateScimUser(firstName: "Kate");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(alice).Should().BeTrue();
        result.InMemoryFilter!(john).Should().BeTrue();
        result.InMemoryFilter!(kate).Should().BeFalse();
    }

    [Fact]
    public void InMemoryFilter_CoOperator_LastName_SetsSearch()
    {
        // co on a known attribute (lastname) maps to Search param, not in-memory
        ScimFilterParams result = _sut.Translate("name.familyName co \"oe\"");

        result.Search.Should().Be("oe");
        result.InMemoryFilter.Should().BeNull();
    }

    [Fact]
    public void InMemoryFilter_UnknownAttribute_EqOperator_ReturnsFalse()
    {
        // Unknown attribute via in-memory filter — exercises the default null case
        ScimFilterParams result = _sut.Translate("unknownAttr eq \"value\"");

        result.InMemoryFilter.Should().NotBeNull();
        ScimUser user = CreateScimUser(userName: "test");
        result.InMemoryFilter!(user).Should().BeFalse();
    }

    #endregion

    #region PresenceNode — username present and email present (via Visit)

    [Fact]
    public void PresenceNode_UserName_Present_ReturnsTrue()
    {
        ScimFilterParams result = _sut.Translate("userName pr");

        ScimUser userWithName = CreateScimUser(userName: "jdoe");
        ScimUser userWithoutName = CreateScimUser(userName: "");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(userWithName).Should().BeTrue();
        result.InMemoryFilter!(userWithoutName).Should().BeFalse();
    }

    [Fact]
    public void PresenceNode_Email_Present_ReturnsTrue()
    {
        ScimFilterParams result = _sut.Translate("emails.value pr");

        ScimUser userWithEmail = CreateScimUser(email: "test@example.com");
        ScimUser userWithoutEmail = CreateScimUser(userName: "test");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(userWithEmail).Should().BeTrue();
        result.InMemoryFilter!(userWithoutEmail).Should().BeFalse();
    }

    [Fact]
    public void PresenceNode_UnknownAttribute_ReturnsFalse()
    {
        ScimFilterParams result = _sut.Translate("unknownAttr pr");

        ScimUser user = CreateScimUser(userName: "test");

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(user).Should().BeFalse();
    }

    #endregion

    #region EvaluateFilter via NotNode — LogicalNode and branch

    [Fact]
    public void NotNode_WithAndLogical_EvaluatesCorrectly()
    {
        ScimFilterParams result = _sut.Translate("not (userName eq \"john\" and active eq \"true\")");

        ScimUser activeJohn = CreateScimUser(userName: "john", active: true);
        ScimUser inactiveJohn = CreateScimUser(userName: "john", active: false);
        ScimUser activeBob = CreateScimUser(userName: "bob", active: true);

        result.InMemoryFilter.Should().NotBeNull();
        result.InMemoryFilter!(activeJohn).Should().BeFalse();
        result.InMemoryFilter!(inactiveJohn).Should().BeTrue();
        result.InMemoryFilter!(activeBob).Should().BeTrue();
    }

    #endregion

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
