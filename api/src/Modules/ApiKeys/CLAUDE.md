# ApiKeys module

No CQRS (deliberate — see `api/CLAUDE.md`).

- **Dual-write order**: PostgreSQL first, then Valkey. Validation reads Valkey first, falls back
  to PostgreSQL on cache miss and repopulates.
- `GetByHashAsync` with `Guid.Empty` tenantId searches ALL tenants — the cache-miss validation
  path.
- Scope validation has two layers: permission-based (all users) and scope-subset (service
  accounts only, identified by the `sa-` prefix on `clientId`); `IScopeSubsetValidator` comes
  from Identity via `Shared.Contracts`.
- Type names keep the `Redis` prefix because they wrap `StackExchange.Redis` (Valkey) — do not
  rename them. New Valkey operations go in both `IRedisDatabase` and `RedisDatabaseWrapper`.
