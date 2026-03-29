using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Tests.Domain;

public class WallowUserMfaLockoutTests
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void IsMfaLockedOut_BeforeAnyFailures_ReturnsFalse()
    {
        WallowUser user = CreateUser();

        bool result = user.IsMfaLockedOut(_timeProvider);

        result.Should().BeFalse();
        user.MfaFailedAttempts.Should().Be(0);
        user.MfaLockoutEnd.Should().BeNull();
        user.MfaLockoutCount.Should().Be(0);
    }

    [Fact]
    public void RecordMfaFailure_BelowThreshold_IncrementsCounterButDoesNotLockOut()
    {
        WallowUser user = CreateUser();
        int maxAttempts = 5;

        user.RecordMfaFailure(maxAttempts, _timeProvider);

        user.MfaFailedAttempts.Should().Be(1);
        user.MfaLockoutEnd.Should().BeNull();
        user.MfaLockoutCount.Should().Be(0);
        user.IsMfaLockedOut(_timeProvider).Should().BeFalse();
    }

    [Fact]
    public void RecordMfaFailure_MultipleBelowThreshold_IncrementsWithoutLockout()
    {
        WallowUser user = CreateUser();
        int maxAttempts = 5;

        for (int i = 0; i < maxAttempts - 1; i++)
        {
            user.RecordMfaFailure(maxAttempts, _timeProvider);
        }

        user.MfaFailedAttempts.Should().Be(4);
        user.MfaLockoutEnd.Should().BeNull();
        user.MfaLockoutCount.Should().Be(0);
        user.IsMfaLockedOut(_timeProvider).Should().BeFalse();
    }

    [Fact]
    public void RecordMfaFailure_AtThreshold_SetsLockoutAndIncrementsLockoutCount()
    {
        WallowUser user = CreateUser();
        int maxAttempts = 5;

        for (int i = 0; i < maxAttempts; i++)
        {
            user.RecordMfaFailure(maxAttempts, _timeProvider);
        }

        user.MfaFailedAttempts.Should().Be(maxAttempts);
        user.MfaLockoutEnd.Should().NotBeNull();
        user.MfaLockoutEnd.Should().BeAfter(_timeProvider.GetUtcNow());
        user.MfaLockoutCount.Should().Be(1);
        user.IsMfaLockedOut(_timeProvider).Should().BeTrue();
    }

    [Fact]
    public void RecordMfaFailure_SecondLockout_DoublesBackoffDuration()
    {
        WallowUser user = CreateUser();
        int maxAttempts = 3;

        // First lockout
        for (int i = 0; i < maxAttempts; i++)
        {
            user.RecordMfaFailure(maxAttempts, _timeProvider);
        }

        DateTimeOffset firstLockoutEnd = user.MfaLockoutEnd!.Value;
        TimeSpan firstDuration = firstLockoutEnd - _timeProvider.GetUtcNow();

        // Advance past first lockout
        _timeProvider.Advance(firstDuration + TimeSpan.FromSeconds(1));
        user.ResetMfaAttempts();

        // Second lockout
        for (int i = 0; i < maxAttempts; i++)
        {
            user.RecordMfaFailure(maxAttempts, _timeProvider);
        }

        DateTimeOffset secondLockoutEnd = user.MfaLockoutEnd!.Value;
        TimeSpan secondDuration = secondLockoutEnd - _timeProvider.GetUtcNow();

        user.MfaLockoutCount.Should().Be(2);
        secondDuration.Should().BeCloseTo(firstDuration * 2, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ResetMfaAttempts_ClearsCounterAndLockoutEnd()
    {
        WallowUser user = CreateUser();
        int maxAttempts = 3;

        // Trigger a lockout
        for (int i = 0; i < maxAttempts; i++)
        {
            user.RecordMfaFailure(maxAttempts, _timeProvider);
        }

        user.ResetMfaAttempts();

        user.MfaFailedAttempts.Should().Be(0);
        user.MfaLockoutEnd.Should().BeNull();
    }

    [Fact]
    public void IsMfaLockedOut_WhenLockoutEndInFuture_ReturnsTrue()
    {
        WallowUser user = CreateUser();
        int maxAttempts = 3;

        for (int i = 0; i < maxAttempts; i++)
        {
            user.RecordMfaFailure(maxAttempts, _timeProvider);
        }

        user.IsMfaLockedOut(_timeProvider).Should().BeTrue();
    }

    [Fact]
    public void IsMfaLockedOut_AfterLockoutExpires_ReturnsFalse()
    {
        WallowUser user = CreateUser();
        int maxAttempts = 3;

        for (int i = 0; i < maxAttempts; i++)
        {
            user.RecordMfaFailure(maxAttempts, _timeProvider);
        }

        DateTimeOffset lockoutEnd = user.MfaLockoutEnd!.Value;
        TimeSpan remaining = lockoutEnd - _timeProvider.GetUtcNow();
        _timeProvider.Advance(remaining + TimeSpan.FromSeconds(1));

        user.IsMfaLockedOut(_timeProvider).Should().BeFalse();
    }

    private WallowUser CreateUser() =>
        WallowUser.Create(Guid.NewGuid(), "John", "Doe", "john@example.com", _timeProvider);
}
