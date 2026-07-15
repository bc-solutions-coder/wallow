using Wallow.Shared.Contracts;
using Wallow.Shared.Contracts.Identity.Events;

namespace Wallow.Identity.Tests.Contracts;

#pragma warning disable CA1034 // Nested types should not be visible (xUnit test grouping pattern)
public static class EmailChangeEventTests
{
    public class UserEmailChangeRequestedEventTests
    {
        [Fact]
        public void Can_be_constructed_with_valid_property_values()
        {
            Guid userId = Guid.NewGuid();
            Guid tenantId = Guid.NewGuid();
            string newEmail = "new@example.com";
            string confirmationUrl = "https://example.com/confirm?token=abc123";
            DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddHours(24);

            UserEmailChangeRequestedEvent sut = new()
            {
                UserId = userId,
                TenantId = tenantId,
                NewEmail = newEmail,
                ConfirmationUrl = confirmationUrl,
                ExpiresAt = expiresAt
            };

            sut.UserId.Should().Be(userId);
            sut.TenantId.Should().Be(tenantId);
            sut.NewEmail.Should().Be(newEmail);
            sut.ConfirmationUrl.Should().Be(confirmationUrl);
            sut.ExpiresAt.Should().Be(expiresAt);
            sut.EventId.Should().NotBeEmpty();
            sut.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Carries_confirmation_url()
        {
            string confirmationUrl = "https://app.example.com/email/confirm?token=xyz";

            UserEmailChangeRequestedEvent sut = new()
            {
                UserId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                NewEmail = "new@example.com",
                ConfirmationUrl = confirmationUrl,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            };

            sut.ConfirmationUrl.Should().Be(confirmationUrl);
        }

        [Fact]
        public void ExpiresAt_reflects_the_value_passed_in()
        {
            DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddHours(48);

            UserEmailChangeRequestedEvent sut = new()
            {
                UserId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                NewEmail = "new@example.com",
                ConfirmationUrl = "https://example.com/confirm",
                ExpiresAt = expiresAt
            };

            sut.ExpiresAt.Should().Be(expiresAt);
        }

        [Fact]
        public void Inherits_from_IntegrationEvent()
        {
            UserEmailChangeRequestedEvent sut = new()
            {
                UserId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                NewEmail = "new@example.com",
                ConfirmationUrl = "https://example.com/confirm",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            };

            sut.Should().BeAssignableTo<IntegrationEvent>();
            sut.Should().BeAssignableTo<IIntegrationEvent>();
        }
    }

    public class UserEmailChangedEventTests
    {
        [Fact]
        public void Can_be_constructed_with_valid_property_values()
        {
            Guid userId = Guid.NewGuid();
            Guid tenantId = Guid.NewGuid();
            string oldEmail = "old@example.com";
            string newEmail = "new@example.com";

            UserEmailChangedEvent sut = new()
            {
                UserId = userId,
                TenantId = tenantId,
                OldEmail = oldEmail,
                NewEmail = newEmail
            };

            sut.UserId.Should().Be(userId);
            sut.TenantId.Should().Be(tenantId);
            sut.OldEmail.Should().Be(oldEmail);
            sut.NewEmail.Should().Be(newEmail);
            sut.EventId.Should().NotBeEmpty();
            sut.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Carries_both_old_and_new_email()
        {
            string oldEmail = "previous@example.com";
            string newEmail = "current@example.com";

            UserEmailChangedEvent sut = new()
            {
                UserId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                OldEmail = oldEmail,
                NewEmail = newEmail
            };

            sut.OldEmail.Should().Be(oldEmail);
            sut.NewEmail.Should().Be(newEmail);
        }

        [Fact]
        public void Inherits_from_IntegrationEvent()
        {
            UserEmailChangedEvent sut = new()
            {
                UserId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                OldEmail = "old@example.com",
                NewEmail = "new@example.com"
            };

            sut.Should().BeAssignableTo<IntegrationEvent>();
            sut.Should().BeAssignableTo<IIntegrationEvent>();
        }
    }
}
