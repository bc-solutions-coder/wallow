using Wallow.Shared.Infrastructure.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Shared.Infrastructure.Tests.Settings;

// Minimal concrete DbContext used only as a type parameter discriminator in generic tests
public sealed class FakeDbContext(DbContextOptions<FakeDbContext> options)
    : TenantAwareDbContext<FakeDbContext>(options);
