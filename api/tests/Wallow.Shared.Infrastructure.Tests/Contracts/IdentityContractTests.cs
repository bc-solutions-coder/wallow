using Wallow.Shared.Contracts.Identity;

namespace Wallow.Shared.Infrastructure.Tests.Contracts;

public class IdentityContractTests
{
    // ── ScopeValidationResult ─────────────────────────────────────────────

    [Fact]
    public void ScopeValidationResult_Success_ReturnsIsSuccessTrueAndNullErrorMessage()
    {
        ScopeValidationResult result = ScopeValidationResult.Success();

        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ScopeValidationResult_Failure_ReturnsIsSuccessFalseAndPreservesErrorMessage()
    {
        ScopeValidationResult result = ScopeValidationResult.Failure("Scope 'admin' is not permitted");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Scope 'admin' is not permitted");
    }

    [Fact]
    public void ScopeValidationResult_PositionalConstruction_SetsAllProperties()
    {
        ScopeValidationResult result = new(true, "some message");

        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().Be("some message");
    }

    // ── UserSearchItem ────────────────────────────────────────────────────

    [Fact]
    public void UserSearchItem_ActiveUserWithRoles_HasCorrectValues()
    {
        Guid userId = Guid.NewGuid();
        List<string> roles = ["Admin", "Editor"];

        UserSearchItem item = new(userId, "alice@example.com", "Alice", "Smith", true, roles);

        item.Id.Should().Be(userId);
        item.Email.Should().Be("alice@example.com");
        item.FirstName.Should().Be("Alice");
        item.LastName.Should().Be("Smith");
        item.IsActive.Should().BeTrue();
        item.Roles.Should().HaveCount(2);
        item.Roles.Should().Contain("Admin");
        item.Roles.Should().Contain("Editor");
    }

    [Fact]
    public void UserSearchItem_InactiveUserWithEmptyRoles_HasCorrectValues()
    {
        Guid userId = Guid.NewGuid();
        List<string> roles = [];

        UserSearchItem item = new(userId, "bob@example.com", "Bob", "Jones", false, roles);

        item.Id.Should().Be(userId);
        item.Email.Should().Be("bob@example.com");
        item.FirstName.Should().Be("Bob");
        item.LastName.Should().Be("Jones");
        item.IsActive.Should().BeFalse();
        item.Roles.Should().BeEmpty();
    }

    // ── UserSearchPageResult ──────────────────────────────────────────────

    [Fact]
    public void UserSearchPageResult_WithMultipleItems_PreservesAllProperties()
    {
        List<UserSearchItem> items =
        [
            new(Guid.NewGuid(), "alice@example.com", "Alice", "Smith", true, ["Admin"]),
            new(Guid.NewGuid(), "bob@example.com", "Bob", "Jones", false, []),
            new(Guid.NewGuid(), "carol@example.com", "Carol", "Lee", true, ["Viewer"])
        ];

        UserSearchPageResult result = new(items, TotalCount: 25, Page: 2, PageSize: 10);

        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(25);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.Items[0].Email.Should().Be("alice@example.com");
        result.Items[1].IsActive.Should().BeFalse();
        result.Items[2].FirstName.Should().Be("Carol");
    }
}
