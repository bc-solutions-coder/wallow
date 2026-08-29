# Identity module

- **Service-heavy, thin CQRS** — a deliberate departure from the repo norm: logic lives in
  Infrastructure services (`Wallow.Identity.Infrastructure/Services/`) called from controllers;
  Wolverine commands/queries exist only for service accounts and setup/bootstrap.
- **OIDC flows**: authorization code WITH PKCE required, client credentials, and refresh tokens —
  no password or device flow (policy, not an omission).
- `Organization.Create` mints the tenant id (org.Id == TenantId); seeding/sync must go through
  it — never mint tenant ids ad hoc.
- `TestSupportController` is test-only, not a pattern for production features.

## Wallow.Identity.IntegrationTests

No shorthand: every spec carries `Category=Integration`, so reach it by project path AND the
tier — without the tier the default filter excludes every spec and the run fails on zero tests.

```bash
./scripts/run-tests.sh api/tests/Modules/Identity/Wallow.Identity.IntegrationTests integration
```
