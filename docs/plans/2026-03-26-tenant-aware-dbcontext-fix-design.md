# Tenant-Aware DbContext Fix

**Date:** 2026-03-26
**Status:** Approved

## Problem

Comments on inquiries always return 0 results despite being saved successfully. Root cause: the scoped `DbContext` factory reads `ITenantContext.TenantId` at DI resolution time, but the tenant context is `default` (empty GUID) at that point because Wolverine's `InvokeAsync` resolves handler dependencies before the `TenantRestoringMiddleware` runs.

This causes:
- `DbContext._tenantId = default` for both saves and queries
- Entities without explicit `TenantId` in their factory method (e.g., `Inquiry`) get saved with `00000000...` and match the default query filter
- Entities with explicit `TenantId` (e.g., `InquiryComment`) get saved with the real tenant but don't match the default query filter
- Result: inquiries appear to work, comments silently return empty

## Changes

### 1. Shared extension method for tenant-aware DbContext registration

Extract the repeated scoped factory pattern from all 9 modules into a single `AddTenantAwareScopedContext<TContext>` extension method in `Wallow.Shared.Infrastructure.Core`. The method uses `AmbientTenant.Current` as fallback when `ITenantContext` is not yet resolved.

**Modules affected:** ApiKeys, Billing, Branding, Identity, Inquiries, Storage, Messaging, Notifications, Announcements

### 2. Fix TenantSaveChangesInterceptor

Add `AmbientTenant.Current` as a third-tier fallback in `ResolveTenantId()`, after checking the DbContext's `CurrentTenantId` and `ITenantContext`.

### 3. Remove TenantId from InquiryComment command flow

Remove `TenantId` parameter from `AddInquiryCommentCommand`, `InquiryComment.Create()`, the handler, and the controller. The comment gets its tenant from the `TenantSaveChangesInterceptor`, same as `Inquiry`.

### 4. Data fix

SQL update to fix existing inquiries saved with empty tenant_id (development environment only; production would need a migration).
