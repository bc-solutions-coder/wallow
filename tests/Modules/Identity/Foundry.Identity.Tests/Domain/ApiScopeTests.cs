using Foundry.Identity.Domain.Entities;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Identity.Tests.Domain;

public class ApiScopeTests
{
    [Fact]
    public void Create_WithValidParameters_CreatesApiScope()
    {
        // Arrange
        string code = "invoices.read";
        string displayName = "Read Invoices";
        string category = "Billing";
        string description = "Allows reading invoice data";

        // Act
        ApiScope scope = ApiScope.Create(code, displayName, category, description, isDefault: true);

        // Assert
        scope.Code.Should().Be(code);
        scope.DisplayName.Should().Be(displayName);
        scope.Category.Should().Be(category);
        scope.Description.Should().Be(description);
        scope.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Create_WithDefaultParameters_CreatesNonDefaultScope()
    {
        // Act
        ApiScope scope = ApiScope.Create("test.read", "Test Read", "Test");

        // Assert
        scope.IsDefault.Should().BeFalse();
        scope.Description.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyCode_ThrowsBusinessRuleException()
    {
        // Act
        Func<ApiScope> act = () => ApiScope.Create("", "Display Name", "Category");

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*code*");
    }

    [Fact]
    public void Create_WithWhitespaceCode_ThrowsBusinessRuleException()
    {
        // Act
        Func<ApiScope> act = () => ApiScope.Create("   ", "Display Name", "Category");

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*code*");
    }

    [Fact]
    public void Create_WithEmptyDisplayName_ThrowsBusinessRuleException()
    {
        // Act
        Func<ApiScope> act = () => ApiScope.Create("test.read", "", "Category");

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*display name*");
    }

    [Fact]
    public void Create_WithWhitespaceDisplayName_ThrowsBusinessRuleException()
    {
        // Act
        Func<ApiScope> act = () => ApiScope.Create("test.read", "   ", "Category");

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*display name*");
    }

    [Fact]
    public void Create_WithEmptyCategory_ThrowsBusinessRuleException()
    {
        // Act
        Func<ApiScope> act = () => ApiScope.Create("test.read", "Display Name", "");

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*category*");
    }

    [Fact]
    public void Create_WithWhitespaceCategory_ThrowsBusinessRuleException()
    {
        // Act
        Func<ApiScope> act = () => ApiScope.Create("test.read", "Display Name", "   ");

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*category*");
    }
}
