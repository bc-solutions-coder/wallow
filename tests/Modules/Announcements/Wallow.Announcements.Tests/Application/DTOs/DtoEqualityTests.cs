using Wallow.Announcements.Application.Announcements.DTOs;
using Wallow.Announcements.Application.Announcements.Queries.GetActiveAnnouncements;
using Wallow.Announcements.Application.Changelogs.DTOs;
using Wallow.Announcements.Application.Changelogs.Queries.GetChangelog;
using Wallow.Announcements.Application.Changelogs.Queries.GetChangelogEntry;
using Wallow.Announcements.Domain.Announcements.Enums;
using Wallow.Announcements.Domain.Changelogs.Enums;

namespace Wallow.Announcements.Tests.Application.DTOs;

public class AnnouncementDtoTests
{
    private static AnnouncementDto CreateDto(Guid? id = null)
    {
        return new AnnouncementDto(
            id ?? Guid.NewGuid(),
            "Title", "Content", AnnouncementType.Feature, AnnouncementTarget.All,
            null, null, null, false, true, null, null, null,
            AnnouncementStatus.Draft, DateTime.UtcNow);
    }

    [Fact]
    public void AnnouncementDto_WithSameValues_AreEqual()
    {
        Guid id = Guid.NewGuid();
        DateTime createdAt = DateTime.UtcNow;
        AnnouncementDto dto1 = new(id, "Title", "Content", AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null, false, true,
            null, null, null, AnnouncementStatus.Draft, createdAt);
        AnnouncementDto dto2 = new(id, "Title", "Content", AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null, false, true,
            null, null, null, AnnouncementStatus.Draft, createdAt);

        dto1.Should().Be(dto2);
    }

    [Fact]
    public void AnnouncementDto_WithDifferentIds_AreNotEqual()
    {
        AnnouncementDto dto1 = CreateDto(Guid.NewGuid());
        AnnouncementDto dto2 = CreateDto(Guid.NewGuid());

        dto1.Should().NotBe(dto2);
    }

    [Fact]
    public void AnnouncementDto_ToString_ContainsTypeName()
    {
        AnnouncementDto dto = CreateDto();

        string result = dto.ToString();

        result.Should().Contain("AnnouncementDto");
    }

    [Fact]
    public void AnnouncementDto_GetHashCode_IsDeterministic()
    {
        Guid id = Guid.NewGuid();
        DateTime createdAt = DateTime.UtcNow;
        AnnouncementDto dto1 = new(id, "Title", "Content", AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null, false, true,
            null, null, null, AnnouncementStatus.Draft, createdAt);
        AnnouncementDto dto2 = new(id, "Title", "Content", AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null, false, true,
            null, null, null, AnnouncementStatus.Draft, createdAt);

        dto1.GetHashCode().Should().Be(dto2.GetHashCode());
    }

    [Fact]
    public void AnnouncementDto_WithExpression_CreatesModifiedCopy()
    {
        AnnouncementDto original = CreateDto();

        AnnouncementDto modified = original with { Title = "New Title" };

        modified.Title.Should().Be("New Title");
        modified.Id.Should().Be(original.Id);
    }
}

public class ChangelogItemDtoTests
{
    [Fact]
    public void ChangelogItemDto_WithSameValues_AreEqual()
    {
        Guid id = Guid.NewGuid();
        ChangelogItemDto dto1 = new(id, "Fixed something", ChangeType.Fix);
        ChangelogItemDto dto2 = new(id, "Fixed something", ChangeType.Fix);

        dto1.Should().Be(dto2);
    }

    [Fact]
    public void ChangelogItemDto_ToString_ContainsTypeName()
    {
        ChangelogItemDto dto = new(Guid.NewGuid(), "Feature added", ChangeType.Feature);

        string result = dto.ToString();

        result.Should().Contain("ChangelogItemDto");
    }
}

public class GetLatestChangelogQueryTests
{
    [Fact]
    public void GetLatestChangelogQuery_TwoInstances_AreEqual()
    {
        GetLatestChangelogQuery query1 = new();
        GetLatestChangelogQuery query2 = new();

        query1.Should().Be(query2);
    }

    [Fact]
    public void GetLatestChangelogQuery_GetHashCode_IsConsistent()
    {
        GetLatestChangelogQuery query = new();

        int hash1 = query.GetHashCode();
        int hash2 = query.GetHashCode();

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetLatestChangelogQuery_ToString_ContainsTypeName()
    {
        GetLatestChangelogQuery query = new();

        string result = query.ToString();

        result.Should().Contain("GetLatestChangelogQuery");
    }
}

public class GetChangelogQueryTests
{
    [Fact]
    public void GetChangelogQuery_DefaultLimit_Is50()
    {
        GetChangelogQuery query = new();

        query.Limit.Should().Be(50);
    }

    [Fact]
    public void GetChangelogQuery_WithLimit_StoresLimit()
    {
        GetChangelogQuery query = new(25);

        query.Limit.Should().Be(25);
    }

    [Fact]
    public void GetChangelogQuery_WithSameValues_AreEqual()
    {
        GetChangelogQuery query1 = new(10);
        GetChangelogQuery query2 = new(10);

        query1.Should().Be(query2);
    }
}

public class GetActiveAnnouncementsQueryTests
{
    [Fact]
    public void GetActiveAnnouncementsQuery_StoresAllFields()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        List<string> roles = new() { "admin", "user" };

        GetActiveAnnouncementsQuery query = new(userId, tenantId, "pro", roles);

        query.UserId.Should().Be(userId);
        query.TenantId.Should().Be(tenantId);
        query.PlanName.Should().Be("pro");
        query.Roles.Should().BeEquivalentTo(roles);
    }

    [Fact]
    public void GetActiveAnnouncementsQuery_WithNullPlan_StoresNull()
    {
        GetActiveAnnouncementsQuery query = new(Guid.NewGuid(), Guid.NewGuid(), null, []);

        query.PlanName.Should().BeNull();
    }
}
