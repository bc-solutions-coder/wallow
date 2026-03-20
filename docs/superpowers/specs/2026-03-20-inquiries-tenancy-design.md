# Add Multi-Tenancy to Inquiries Module

**Date:** 2026-03-20
**Status:** Approved
**Module:** Inquiries

## Problem

The Inquiries module lacks tenant isolation. `Inquiry` does not implement `ITenantScoped`, the `InquiriesDbContext` extends plain `DbContext` instead of `TenantAwareDbContext<T>`, and no global query filters or save-changes interceptors enforce tenant boundaries. `InquiryComment` already implements `ITenantScoped`, but without a tenant-aware DbContext, its `TenantId` is neither auto-stamped nor filtered.

## Decision

Follow the established tenancy pattern used by Billing, Notifications, and other modules. All inquiry submissions require authentication; the tenant resolves from the JWT `org_id` claim.

## Changes

### Domain Layer

Add `ITenantScoped` to `Inquiry`:

```csharp
public sealed class Inquiry : AggregateRoot<InquiryId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    // ... rest unchanged
}
```

`InquiryComment` already implements `ITenantScoped` — no changes needed.

The `Create` factory method does not accept `TenantId` as a parameter. The `TenantSaveChangesInterceptor` stamps it automatically on insert.

### Infrastructure Layer

**InquiriesDbContext** — inherit from `TenantAwareDbContext<InquiriesDbContext>`:

```csharp
public sealed class InquiriesDbContext : TenantAwareDbContext<InquiriesDbContext>
{
    public InquiriesDbContext(
        DbContextOptions<InquiriesDbContext> options,
        ITenantContext tenantContext)
        : base(options, tenantContext)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("inquiries");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InquiriesDbContext).Assembly);
        ApplyTenantQueryFilters(modelBuilder);
    }
}
```

**InquiryConfiguration** — map the `TenantId` column and add an index, matching `InquiryCommentConfiguration`:

```csharp
builder.Property(i => i.TenantId)
    .HasConversion(new StronglyTypedIdConverter<TenantId>())
    .HasColumnName("tenant_id")
    .IsRequired();

builder.HasIndex(i => i.TenantId);
```

**InquiriesDbContextFactory** — update the design-time factory to pass a `DesignTimeTenantContext` to the new constructor, matching the pattern in `BillingDbContextFactory`.

**InquiriesInfrastructureExtensions** — register `TenantSaveChangesInterceptor`:

```csharp
services.AddDbContext<InquiriesDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString, ...);
    options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
});
```

### Migration

The database is fresh. Delete the existing migration files (`InitialCreate.cs`, `InitialCreate.Designer.cs`, `InquiriesDbContextModelSnapshot.cs`) from `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Migrations/`, then regenerate:

```bash
dotnet ef migrations add InitialCreate \
    --project src/Modules/Inquiries/Wallow.Inquiries.Infrastructure \
    --startup-project src/Wallow.Api \
    --context InquiriesDbContext
```

The new migration will include `tenant_id` on the `inquiries` table from the start.

## Out of Scope

`InquiryComment.Create` captures `TenantId.Value` in a domain event before the interceptor stamps it, so the event payload contains `Guid.Empty`. This is a pre-existing bug unrelated to this change and should be tracked separately.

## What Does Not Change

- `InquiryComment` domain entity (already has `ITenantScoped`)
- Repository interfaces and implementations (global query filters handle tenant filtering transparently)
- Command/query handlers (tenant context is resolved from JWT by existing middleware)
- No new abstractions, packages, or infrastructure components
