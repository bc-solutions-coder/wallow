using Foundry.Storage.Domain.Enums;
using Foundry.Storage.Domain.ValueObjects;

namespace Foundry.Storage.Tests.Domain.Entities;

public class RetentionPolicyTests
{
    [Fact]
    public void Constructor_WithValidDaysAndDeleteAction_SetsProperties()
    {
        RetentionPolicy policy = new(30, RetentionAction.Delete);

        policy.Days.Should().Be(30);
        policy.Action.Should().Be(RetentionAction.Delete);
    }

    [Fact]
    public void Constructor_WithValidDaysAndArchiveAction_SetsProperties()
    {
        RetentionPolicy policy = new(90, RetentionAction.Archive);

        policy.Days.Should().Be(90);
        policy.Action.Should().Be(RetentionAction.Archive);
    }

    [Fact]
    public void Constructor_WithZeroDays_SetsProperties()
    {
        RetentionPolicy policy = new(0, RetentionAction.Delete);

        policy.Days.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithLargeDays_SetsProperties()
    {
        RetentionPolicy policy = new(365, RetentionAction.Archive);

        policy.Days.Should().Be(365);
        policy.Action.Should().Be(RetentionAction.Archive);
    }

    [Fact]
    public void Equality_SameDaysAndAction_AreEqual()
    {
        RetentionPolicy policy1 = new(30, RetentionAction.Delete);
        RetentionPolicy policy2 = new(30, RetentionAction.Delete);

        policy1.Should().Be(policy2);
    }

    [Fact]
    public void Equality_DifferentDays_AreNotEqual()
    {
        RetentionPolicy policy1 = new(30, RetentionAction.Delete);
        RetentionPolicy policy2 = new(60, RetentionAction.Delete);

        policy1.Should().NotBe(policy2);
    }

    [Fact]
    public void Equality_DifferentAction_AreNotEqual()
    {
        RetentionPolicy policy1 = new(30, RetentionAction.Delete);
        RetentionPolicy policy2 = new(30, RetentionAction.Archive);

        policy1.Should().NotBe(policy2);
    }

    [Fact]
    public void ToString_ReturnsReadableRepresentation()
    {
        RetentionPolicy policy = new(30, RetentionAction.Delete);

        string result = policy.ToString();

        result.Should().Contain("30");
        result.Should().Contain("Delete");
    }

    [Fact]
    public void GetHashCode_EqualPolicies_ReturnSameHashCode()
    {
        RetentionPolicy policy1 = new(30, RetentionAction.Delete);
        RetentionPolicy policy2 = new(30, RetentionAction.Delete);

        policy1.GetHashCode().Should().Be(policy2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentPolicies_ReturnDifferentHashCode()
    {
        RetentionPolicy policy1 = new(30, RetentionAction.Delete);
        RetentionPolicy policy2 = new(60, RetentionAction.Archive);

        policy1.GetHashCode().Should().NotBe(policy2.GetHashCode());
    }
}
