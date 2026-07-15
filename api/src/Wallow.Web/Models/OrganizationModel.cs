namespace Wallow.Web.Models;

public record OrganizationModel(Guid Id, string Name, string? Domain, int MemberCount);
