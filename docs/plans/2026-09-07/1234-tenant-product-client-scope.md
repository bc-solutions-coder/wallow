**status: completed**

# Tenant, product and client implementation scope

**Goal:** define the future work needed to implement the agreed model, without changing source code.

**Architecture:** one instance can own several tenants; each tenant owns products and each product
owns application/service-account clients. Users have tenant membership and product access.
Module-owned data stays isolated through scope, actor permission and resource policy.

**Stack:** existing .NET/OpenIddict/EF Core/Wolverine backend and TypeScript BFF/SDK applications.
This scope does not select new libraries or deployables.

The [design](1234-tenant-product-client-design.md) is the behavioral specification. The scope
document is complete; every implementation package below is **not started**. A later execution
plan must resolve the listed policy decisions and expand packages into concrete code changes.
No instruction in this document authorizes running those changes now.

## 1. Scope and release boundaries

The complete target includes nested products, application and machine authorization, multiple
client types, reusable modules, external API consumers and one explicit cross-product read/report
flow. Implementing identity registration alone does not complete multi-tenancy.

| Milestone                                 | Included                                                                                                                   | Exit requirement                                                                         |
| ----------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| A: ownership and authorization foundation | Tenant/product model, membership, client lifecycle, scopes, product-aware persistence and asynchronous boundaries          | Two tenants and sibling products pass the negative-access matrix across existing modules |
| B: adopter integrations                   | Management UI, generated contracts/SDK, web BFF, public mobile flow, external resource server and service-account examples | Independent consumers complete real authorized flows and reject wrong scopes/audiences   |
| C: explicit sharing                       | One module-owned data grant and aggregation/report scenario                                                                | Source and recipient authority, copy semantics and revocation verified                   |

The milestones are verifiable increments, not permission to ship partial isolation. The final
feature claim requires all included milestones. Stronger physical isolation, generalized sharing
and module extraction remain deferred. Because Wallow is pre-release with disposable local data,
reshape schemas and seeds coherently; do not build compatibility aliases, dual writes or backfill
machinery solely to preserve the old organization model.

## 2. Current-code evidence and change map

Paths identify current seams to revisit, not a frozen exhaustive file list. The workspace has
concurrent telemetry changes; refresh the inventory before implementation. This is an architecture
assessment, not a claim that today's system has exploitable cross-product leakage.

| Area and current path                                                                                                                                                                                                                   | Existing assumption                                                                   | Required scope                                                                                                   |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| [Organization](../../../api/src/Modules/Identity/Wallow.Identity.Domain/Entities/Organization.cs)                                                                                                                                       | Factory forces TenantId equal to OrganizationId                                       | Replace organization customer ownership with Tenant; introduce distinct Product; no mechanical rename to Product |
| [Membership](../../../api/src/Modules/Identity/Wallow.Identity.Domain/Entities/Membership.cs)                                                                                                                                           | Roles/status belong to organization membership                                        | Tenant admission plus product access/role scope; prevent cross-parent references                                 |
| [RegisteredClient](../../../api/src/Modules/Identity/Wallow.Identity.Domain/Entities/RegisteredClient.cs)                                                                                                                               | Runtime clients belong to OrganizationId                                              | Required tenant/product ownership and explicit kind/authentication method                                        |
| [Tenant resolution](../../../api/src/Modules/Identity/Wallow.Identity.Infrastructure/MultiTenancy/TenantResolutionMiddleware.cs)                                                                                                        | org_id supplies tenant context; privileged header override                            | Separate validated tenant/product context and controlled management selection                                    |
| [Permission expansion](../../../api/src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/PermissionExpansionMiddleware.cs)                                                                                                 | Prefix-based app/sa branches; role/scope expansion and global administrator expansion | One explicit client ceiling plus actor authorization; no global-user bypass through a product client             |
| [Authorization controller](../../../api/src/Modules/Identity/Wallow.Identity.Api/Controllers/AuthorizationController.cs) and [token controller](../../../api/src/Modules/Identity/Wallow.Identity.Api/Controllers/TokenController.cs)   | Organization enrollment, scope selection, role refresh and fixed API audience         | Product admission/context, both client types, registered audiences, refresh revalidation and bounded revocation  |
| [TenantAwareDbContext](../../../api/src/Shared/Wallow.Shared.Infrastructure.Core/Persistence/TenantAwareDbContext.cs) and [save interceptor](../../../api/src/Shared/Wallow.Shared.Kernel/MultiTenancy/TenantSaveChangesInterceptor.cs) | Tenant-only filters/stamping                                                          | Explicit product ownership and fail-closed writes; constraints, raw/bulk and pooled-context paths                |
| [OrganizationService](../../../api/src/Modules/Identity/Wallow.Identity.Infrastructure/Services/OrganizationService.cs)                                                                                                                 | Lifecycle and deletion assume organization equals tenant                              | Separate tenant/product lifecycle and deletion envelopes                                                         |
| [Branding deletion](../../../api/src/Modules/Branding/Wallow.Branding.Infrastructure/Handlers/OrganizationDeletedHandler.cs)                                                                                                            | Organization deletion enumerates tenant-scoped branding                               | Delete product branding without sibling or tenant-wide data loss                                                 |
| [Message stamping](../../../api/src/Shared/Wallow.Shared.Infrastructure.Core/Middleware/TenantStampingMiddleware.cs) and [restoring](../../../api/src/Shared/Wallow.Shared.Infrastructure.Core/Middleware/TenantRestoringMiddleware.cs) | Tenant-only envelope/ambient context                                                  | Owner and acting product where appropriate, explicit actor authority and isolated job scopes                     |
| [Default connection resolver](../../../api/src/Shared/Wallow.Shared.Kernel/MultiTenancy/DefaultTenantConnectionResolver.cs)                                                                                                             | Returns one configured connection                                                     | Keep shared DB initial model; do not claim database-per-tenant from interface existence                          |

Current registration work also includes telemetry-related changes. Those must be incorporated into
the inventory once settled, not overwritten or silently dropped during a tenant/product rewrite.

## 3. Work packages

### P0. Resolve policy and inventory ownership

Depends on: this design. Produces no runtime feature.

- Resolve the design's platform-client, revocation, external-audience and first-dataset decisions.
- Inventory every entity, endpoint, cache, job, event, file namespace and export as instance-,
  tenant-, product- or person-owned, including combinations. Record owner, actor policy, purge
  predicate and scope source for each. Inspect bypasses rather than assuming a shared abstraction covers them.
- Reconcile the existing integration-events-only glossary with the approved Shared.Contracts
  interfaces in api/CLAUDE without broadening module persistence access.
- Define roles and scope delegation separately for tenant administration, product administration,
  business users and machines. Classify all existing platform-only capabilities.

Acceptance: no unclassified existing record/path before schema changes; each open security policy
has a concrete decision, owner and behavioral example. This package does not require all optional
future features to be designed.

### P1. Establish the identity domain and fresh schema

Depends on: P0.

Affected directories: `api/src/Modules/Identity/Wallow.Identity.Domain`, Identity Application and
Infrastructure persistence/migrations; `api/seed.json`; `api/src/Wallow.SeederService`;
`api/src/Shared/Wallow.Shared.Kernel/Identity`.

- Introduce Tenant, Product, tenant membership and product access; maintain instance-owned users.
- Move runtime client ownership to product and tenant; represent service-account identity explicitly.
- Encode parent consistency, role scope, immutable ownership and unique admission relationships.
- Update invitations, enrollment, setup, MFA/admission policy ownership and administrative delegation.
- Replace local schema/seed assumptions with a clean setup flow; preserve supported lifecycle behavior.

Acceptance: fresh database setup succeeds; tenant/product identities differ; one user can hold
different roles in two products and memberships in two tenants. Removing one relationship preserves
the others. A product/client cannot reference a parent in another tenant.

### P2. Enforce authorization and token contracts

Depends on: P1 and the P0 token/management decisions.

Affected seams: AuthorizationController, TokenController, PermissionExpansionMiddleware,
TenantResolutionMiddleware, ScopeSubsetValidator, ClientAccessPolicy, MembershipRoleResolver,
AccessRevoker, OpenIddict scope seeding/synchronization and Shared.Kernel authorization mappings.

- Separate scope ceilings, actor permissions and resource ownership. Cover platform administrators
  using restricted product apps and machine callers without user claims.
- Introduce explicit tenant/product claim semantics and validate registered audience selection.
- Support authorization-code/PKCE for confidential and public applications, and client credentials
  for machines. Define permission catalog ownership for external APIs without minting arbitrary privileges.
- Bind consent, authorization codes, refresh grants and sessions to the correct context. Recheck
  reduced grants and lifecycle state; prevent cross-client or cross-product grant reuse.
- Define dedicated policies for platform management, user self-service, API keys and business APIs.
  Existing user API keys must not become a route around product/client restrictions. Choose explicit
  key context and allowed endpoint behavior, or exclude keys from product APIs until supported.
- Specify OAuth refusal behavior versus API 401/403/non-disclosing 404 responses; preserve protocol
  errors on OAuth endpoints. CORS and hidden UI actions are not authorization.

Acceptance: the complete user/client matrix passes using issued tokens, including refresh and
scope-reduction scenarios. APIs reject ID tokens, wrong audiences, forged context and expired grants.
Machine audit subjects never become the creator's UserId.

### P3. Enforce ownership across every module

Depends on: P0 inventory, P1 and P2 contracts.

Affected seams: Shared.Kernel MultiTenancy; Shared.Infrastructure.Core persistence/messaging;
each module's DbContext, repositories, commands/queries, event handlers and lifecycle cleanup.

| Existing module/capability        | Ownership decisions and required coverage                                                                                 |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| Identity                          | Global credentials versus tenant membership/product grants; settings, invitations, audit visibility and directory exports |
| Storage                           | Buckets, files and quotas; tenant-wide versus product-owned collections; upload/download/delete and signed links          |
| Notifications                     | Recipient plus tenant/product; preferences, push configuration/subscriptions, fan-out and delivery client                 |
| Branding                          | Instance fallback, tenant/product branding and client overrides; independent cleanup and cache invalidation               |
| ApiKeys                           | User authority plus explicit context; issuance, lookup, revocation and scope restrictions                                 |
| Inquiries                         | Submission ownership, anonymous intake context and authorized review/export                                               |
| Announcements, if still installed | Broadcast ownership, audience and downstream recipient scope; coordinate any independent removal work                     |
| Telemetry/observability           | Client credentials, provisioning, storage/query/export partitions and scope carried into external components              |

- Enforce tenant/product ownership on writes as well as reads; cover filter bypasses and stale
  pooled/ambient context. Use constraints within module stores and validated cross-module contracts.
- Carry scope/provenance through events, retries, jobs, cache, search, realtime and blob operations.
- Split product deletion from tenant purge; add idempotency/tombstone handling for delayed work.
- Keep authorization-sensitive projections fresh enough for the chosen revocation contract.

Acceptance: two sibling products remain isolated across every inventoried path, including deletion
and asynchronous work. Tenant-wide data is shared only through its explicit policy. No unscoped
business operation succeeds because its caller omitted context.

### P4. Expose management contracts and usable administration

Depends on: P1–P3 stable contracts.

Affected areas: Identity API contracts/controllers; `apps/wallow-web/src/features/organizations`;
`apps/wallow-auth`; `packages/auth`; `packages/sdk/openapi/v1.json`; SDK generated sources;
`packages/api-errors`; `apps/minimal-app`; setup and integration docs.

- Replace organization-facing management with tenant/product workflows and truthful labels.
- Expose product members, client kinds, allowed scopes/audiences, service grants and lifecycle.
- Scope product switching and sessions correctly; avoid a mutable selector changing another app's authority.
- Show public mobile registrations without a secret; show confidential credentials once and support rotation.
- Regenerate OpenAPI/SDK/error contracts only after backend behavior settles; migrate all callers and
  remove legacy organization APIs in the same implementation, not a long-lived compatibility layer.
- Update glossary/API guides and examples to describe actual implementation at completion.

Acceptance: an operator can bootstrap, create two products, register different clients and give
one user different roles without database edits. UI errors explain scope versus user access without
leaking another tenant's resources. Direct API requests enforce the same restrictions.

### P5. Prove independent integrations

Depends on: P2–P4.

- Demonstrate a confidential web BFF and native public-client authorization flow for one product.
- Demonstrate an external backend using Wallow identity without a private Identity-module dependency.
- Demonstrate a machine calling Wallow with its own grants and rotating credentials without changing identity.
- Define API resource registration/discovery and external validation examples, including audience
  separation, permission ownership and revocation limitations.
- Demonstrate two products invoking one installed module without duplicated implementation or data leakage.

Acceptance: real issued tokens work with an independently configured consumer. Wrong audience,
insufficient scope, wrong product and revoked authority fail under the selected contract. The mobile
flow verifies redirects and PKCE, not a mocked or secret-bearing pseudo-native registration.

### P6. Implement one bounded sharing and aggregation flow

Depends on: P3 and P5; approved dataset/copy policy from P0.

- Add module-owned read grants for a real source dataset with an explicit recipient product.
- Authorize source reads without changing the caller's acting product or bypassing tenant filtering.
- Build one live view or materialized Insights report; if both are included, verify their distinct
  user policies. Use a service account for scheduled imports with approved copying authority.
- Record source provenance and define invalidation, retained-copy handling and deletion behavior.

Acceptance: Product C can combine approved information from A and B in one tenant. It cannot read
ungranted fields/collections or another tenant. Revoking A stops further A imports/reads and applies
the selected existing-report policy without affecting B. Insights users cannot receive raw source
data merely because the importer is privileged.

### P7. Complete the implementation handoff

Depends on: all included packages.

- Remove obsolete organization-equals-tenant assumptions in seed, contracts, docs and tests.
- Publish the actual isolation guarantee, supported client types and external revocation behavior.
- Run repository gates once the implementation is complete and focused verification passes.
- Record unresolved exclusions honestly; do not describe interface placeholders as deployed isolation.

Acceptance: all scenarios below pass or an explicitly approved scope reduction changes the release
claim. Design documentation and generated contracts describe the same behavior. Source implementation
is a later authorization, not the completion criterion for this documentation task.

## 4. Behavioral acceptance matrix

Use fixtures with Tenant A containing Products Orders, Operations and Insights, and Tenant B
containing another Orders product. Include one user shared across tenants, one product-limited
customer, one administrator, a restricted customer client, an admin client and an Insights machine.

| ID  | Scenario                                                         | Required result                                                                    |
| --- | ---------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| I01 | Shared user switches product                                     | Same person identity; new validated product context; no union of roles             |
| I02 | Tenant admission without product grant                           | No product access unless an explicit all-members policy applies                    |
| I03 | Remove product access                                            | Other product and tenant memberships remain usable                                 |
| I04 | Tenant admin manages a member                                    | Cannot edit global credentials or inspect unrelated tenant membership              |
| A01 | Admin user through customer client calls admin endpoint          | Denied for absent scope                                                            |
| A02 | Customer through admin-capable client calls admin endpoint       | Denied for absent user authority                                                   |
| A03 | Valid user/client requests another person's private resource     | Denied by resource policy                                                          |
| A04 | Same product name in another tenant                              | No access or ID-based cross-reference                                              |
| A05 | Modify tenant/product header, route or query                     | Cannot change granted context                                                      |
| A06 | Request extra scopes/audience; refresh after reduction           | No privilege gain; reduced authority takes effect under revocation contract        |
| A07 | First-party provenance or global admin in restricted product app | Client ceiling still applies                                                       |
| A08 | API key calls product endpoint                                   | Explicit equivalent context policy or deliberate rejection; no alternate bypass    |
| C01 | Mobile public client redeems code without valid PKCE             | Rejected                                                                           |
| C02 | External API token is presented to Wallow                        | Wrong audience rejected                                                            |
| C03 | Service creator loses membership                                 | Machine authority remains explicit; audit stays under machine identity             |
| C04 | Service secret rotates; old secret is retried                    | Old credential fails after rotation policy; principal unchanged                    |
| D01 | Unresolved persistence scope or cross-product attached update    | Rejected; no silent unscoped write                                                 |
| D02 | Raw/bulk query, search, cache hit and signed file link           | Same ownership/expiry rules as ordinary API access                                 |
| D03 | Pooled context/job executes A then B                             | B never inherits A context or authorization                                        |
| D04 | Product deletion                                                 | Sibling and tenant-owned data survive; replay cannot recreate deleted product data |
| D05 | Tenant deletion                                                  | Other tenant and global user survive; descendant credentials cannot act            |
| S01 | Insights requests ungranted source dataset                       | Denied even when both products share TenantId                                      |
| S02 | Grant permits read but not export/write                          | Export/write rejected; query cannot broaden resource selector                      |
| S03 | Materialized report has broader audience                         | Requires explicit copying/report-audience authorization                            |
| S04 | Grant revoked while job/cache/report exists                      | New access blocked and defined retained-copy policy enforced                       |
| L01 | Membership/client/product suspension with outstanding token      | Hosted and external APIs meet their explicitly documented revocation bounds        |
| L02 | Event redelivery after purge                                     | Idempotent outcome; no resurrection or wrong-scope deletion                        |

These are future behavior/integration tests. Do not add source-text assertions. Current design
verification checks documentation consistency and referenced files only; it does not run these cases.

## 5. Verification commands for later implementation

Run focused tests for the affected package first, then repository gates once changes settle:

```bash
./scripts/run-tests.sh identity
./scripts/run-tests.sh api/tests/Modules/Identity/Wallow.Identity.IntegrationTests integration
./scripts/run-tests.sh
pnpm check
```

Add each affected module's integration tier as identified by P3. Runtime database, issued-token,
external consumer and browser/mobile evidence is required for the acceptance cases; a compile or
mock-only pass is insufficient. Record executed commands, real results and remaining limitations.
For documentation-only work now, verify links, status labels and consistency with the user's decisions.

## 6. Explicit exclusions and implementation risks

Excluded: Wallow-operated SaaS, billing, arbitrary cross-tenant sharing, independent identity realms,
database-per-tenant automation, per-tenant schema customization, an RLS rollout, untrusted runtime
plugins, module marketplace, automatic service extraction, generalized OAuth token exchange,
product/client reparenting, recursive organizations and production migration compatibility machinery.

Main risks are permission union, product-private data inheriting tenant-wide filters, global user
administration leaking into tenant administration, stale asynchronous authority, product deletion
using tenant purge, external JWTs retaining authority longer than advertised, and machine-powered
reports exposing source data to a broader audience. Each has a concrete package and negative test above.

The implementation should not begin by renaming Organization to Product. Today's Organization
combines the customer/tenant boundary with client ownership. Its responsibilities must be split
between Tenant and Product before callers or records are reassigned.
