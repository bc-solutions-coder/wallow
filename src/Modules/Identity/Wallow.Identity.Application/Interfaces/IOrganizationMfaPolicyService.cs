namespace Wallow.Identity.Application.Interfaces;

public record OrgMfaPolicyResult(bool RequiresMfa, bool IsInGracePeriod);

public interface IOrganizationMfaPolicyService
{
    Task<OrgMfaPolicyResult> CheckAsync(Guid userId, CancellationToken ct);
}
