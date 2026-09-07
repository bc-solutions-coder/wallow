using Wallow.Announcements.Application.Announcements.DTOs;
using Wallow.Announcements.Application.Announcements.Queries.GetActiveAnnouncements;
using Wallow.Announcements.Domain.Announcements.Enums;

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

public class GetActiveAnnouncementsQueryTests
{
    [Fact]
    public void GetActiveAnnouncementsQuery_StoresAllFields()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        List<string> roles = new() { "admin", "user" };

        GetActiveAnnouncementsQuery query = new(userId, tenantId, roles);

        query.UserId.Should().Be(userId);
        query.TenantId.Should().Be(tenantId);
        query.Roles.Should().BeEquivalentTo(roles);
    }
}
