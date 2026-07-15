using Microsoft.AspNetCore.Identity;

namespace Wallow.Identity.Domain.Entities;

public class WallowRole : IdentityRole<Guid>
{
    public Guid TenantId { get; set; }
}
