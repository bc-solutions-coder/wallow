using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Tests.Domain;

public class ApiScopeTests
{
    [Fact]
    public void Create_WithValidParameters_CreatesApiScope()
    {

        string code = "invoices.read";
        string displayName = "Read Invoices";
        string category = "Billing";
        string description = "Allows reading invoice data";


        ApiScope scope = ApiScope.Create(code, displayName, category, description, isDefault: true);


        scope.Code.Should().Be(code);
        scope.DisplayName.Should().Be(displayName);
        scope.Category.Should().Be(category);
        scope.Description.Should().Be(description);
        scope.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Create_WithDefaultParameters_CreatesNonDefaultScope()
    {

        ApiScope scope = ApiScope.Create("test.read", "Test Read", "Test");


        scope.IsDefault.Should().BeFalse();
        scope.Description.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyCode_ThrowsBusinessRuleException()
    {

        Func<ApiScope> act = () => ApiScope.Create("", "Display Name", "Category");


        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*code*");
    }

    [Fact]
    public void Create_WithWhitespaceCode_ThrowsBusinessRuleException()
    {

        Func<ApiScope> act = () => ApiScope.Create("   ", "Display Name", "Category");


        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*code*");
    }

    [Fact]
    public void Create_WithEmptyDisplayName_ThrowsBusinessRuleException()
    {

        Func<ApiScope> act = () => ApiScope.Create("test.read", "", "Category");


        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*display name*");
    }

    [Fact]
    public void Create_WithWhitespaceDisplayName_ThrowsBusinessRuleException()
    {

        Func<ApiScope> act = () => ApiScope.Create("test.read", "   ", "Category");


        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*display name*");
    }

    [Fact]
    public void Create_WithEmptyCategory_ThrowsBusinessRuleException()
    {

        Func<ApiScope> act = () => ApiScope.Create("test.read", "Display Name", "");


        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*category*");
    }

    [Fact]
    public void Create_WithWhitespaceCategory_ThrowsBusinessRuleException()
    {

        Func<ApiScope> act = () => ApiScope.Create("test.read", "Display Name", "   ");


        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*category*");
    }
}
