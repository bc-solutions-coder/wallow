using Foundry.Communications.Api.Contracts.Email.Enums;
using Foundry.Communications.Api.Mappings;
using Foundry.Communications.Domain.Enums;

namespace Foundry.Communications.Tests.Api.Mappings;

public class EnumMappingsTests
{
    #region ToDomain

    [Theory]
    [InlineData(ApiNotificationType.TaskAssigned, NotificationType.TaskAssigned)]
    [InlineData(ApiNotificationType.TaskCompleted, NotificationType.TaskCompleted)]
    [InlineData(ApiNotificationType.BillingInvoice, NotificationType.BillingInvoice)]
    [InlineData(ApiNotificationType.SystemNotification, NotificationType.SystemNotification)]
    public void ToDomain_MapsCorrectly(ApiNotificationType api, NotificationType expected)
    {
        NotificationType result = api.ToDomain();

        result.Should().Be(expected);
    }

    [Fact]
    public void ToDomain_WithInvalidValue_ThrowsArgumentOutOfRangeException()
    {
        ApiNotificationType invalid = (ApiNotificationType)999;

        Action act = () => invalid.ToDomain();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("api");
    }

    #endregion

    #region ToApi

    [Theory]
    [InlineData(NotificationType.TaskAssigned, ApiNotificationType.TaskAssigned)]
    [InlineData(NotificationType.TaskCompleted, ApiNotificationType.TaskCompleted)]
    [InlineData(NotificationType.BillingInvoice, ApiNotificationType.BillingInvoice)]
    [InlineData(NotificationType.SystemNotification, ApiNotificationType.SystemNotification)]
    public void ToApi_MapsCorrectly(NotificationType domain, ApiNotificationType expected)
    {
        ApiNotificationType result = domain.ToApi();

        result.Should().Be(expected);
    }

    [Fact]
    public void ToApi_WithInvalidValue_ThrowsArgumentOutOfRangeException()
    {
        NotificationType invalid = (NotificationType)999;

        Action act = () => invalid.ToApi();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("domain");
    }

    #endregion
}
