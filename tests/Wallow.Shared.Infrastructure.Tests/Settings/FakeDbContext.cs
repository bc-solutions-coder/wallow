using Microsoft.EntityFrameworkCore;
using Wallow.Shared.Infrastructure.Core.Persistence;

namespace Wallow.Shared.Infrastructure.Tests.Settings;

// Minimal concrete DbContext used only as a type parameter discriminator in generic tests
public sealed class FakeDbContext(DbContextOptions<FakeDbContext> options)
    : TenantAwareDbContext<FakeDbContext>(options);
