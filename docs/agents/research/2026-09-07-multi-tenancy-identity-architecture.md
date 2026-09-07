# Multi-tenancy and identity architecture for fork-first Wallow

**Research completed:** 2026-09-07. **Status:** research and proposed design; no implementation decision or security certification.

**Subsequent design decision:** the discussion selected **Tenant → Product → Client**, with
application and service-account client kinds. The organization-based recommendation below is
historical research, not the selected target. See the
[agreed design and proposed policies](../../plans/2026-09-07/1234-tenant-product-client-design.md)
and [implementation scope](../../plans/2026-09-07/1234-tenant-product-client-scope.md).
No source implementation was authorized as part of that design work.

## Recommended direction

For the requirements described, use **Instance → Tenant → Organization**, with deployment-wide users connected through memberships. Treat **Product/Project** as software and **Client** as an OAuth/OIDC application registration. These are different concepts, not competing names for the same container.

A fork is a copy of the codebase. An instance is an operated installation. A tenant is a logical customer/data boundary _inside_ that installation. A fork can produce development and production instances; an instance can contain one tenant or several. Infrastructure isolation between independent operators does not provide isolation between customers hosted by one operator. Azure explicitly distinguishes logical tenants from deployments and describes dedicated and shared deployment models. [Microsoft tenancy models](https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/tenancy-models)

**Recommendation:** ship a normal installation with one default tenant, while retaining real tenant boundaries in the architecture. Hide tenant selection when there is only one tenant; do not maintain separate single-tenant and multi-tenant authorization implementations. This supports an internal company platform without requiring Wallow's authors to run a hosted service.

Adding Tenant is justified **if one customer/account really contains several separately governed organizations**. If the desired groups are only products, keep Organization as the tenant and introduce Product instead. A hierarchy should represent ownership and authorization requirements, not anticipated marketing terminology.

## Vocabulary and example

| Concept            | Proposed meaning                                                                       | Example                                    |
| ------------------ | -------------------------------------------------------------------------------------- | ------------------------------------------ |
| Fork               | Independently maintained source repository                                             | Acme's Wallow fork                         |
| Instance           | Installation operated under one platform administration authority; normally one issuer | Acme production platform                   |
| Tenant             | Independent account and outer application-data boundary                                | Acme Group; an unrelated customer, Contoso |
| Organization       | Membership and business-data owner within one tenant                                   | Acme Manufacturing; Acme Logistics         |
| Product or Project | Named software solution with related permissions and applications                      | Dispatch                                   |
| Client             | Registered OAuth/OIDC application                                                      | Dispatch web BFF; Dispatch mobile          |
| User               | Login identity within this instance's identity directory                               | Alice                                      |
| Membership         | User's relationship to one organization, including status and roles                    | Alice is Logistics dispatcher              |
| Module             | Code and data ownership boundary                                                       | Identity; Notifications; Dispatch          |

OAuth defines a client as software making protected-resource requests; it separately defines the authorization server and resource server. Accordingly, a mobile client, API audience, product and organization should not be collapsed into one entity. [RFC 6749 §1.1](https://www.rfc-editor.org/rfc/rfc6749.html#section-1.1)

```text
Acme production instance — shared identity directory: Alice, Bob
├── Tenant: Acme Group
│   ├── Organization: Manufacturing ← Alice's membership
│   │   └── Product: Factory Console
│   │       ├── Web client
│   │       └── Mobile client
│   └── Organization: Logistics ← Alice's and Bob's memberships
│       └── Product: Dispatch
│           └── Web client
└── Tenant: Contoso
    └── Organization: Contoso Operations

Manufacturing → explicit inventory-read grant → Logistics
Acme Group ↛ Contoso by default
```

This is an ownership diagram. It does not imply that every descendant has identical data access or that clients must be single-organization forever.

## What established identity platforms actually model

These vendors use different meanings for “tenant.” Their models are useful precedents, not interchangeable schemas or evidence that an application automatically has safe data isolation.

| Platform | Documented structure                                                                                                                                                                                                                                        | Lesson for Wallow                                                                                                                                            |
| -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Keycloak | Realms contain identity resources. Organizations operate within a realm; an organization member is a realm user linked to one or more organizations. Organization context can be carried in tokens.                                                         | Users outside organizations plus memberships is an established B2B identity model. A realm is closer to an identity boundary than an ordinary company row.   |
| ZITADEL  | An instance contains organizations and normally represents one issuer. Organizations contain projects and users. Projects group applications, roles and assignments; grants let another organization use a project and manage assignments to granted roles. | Separate software ownership from the organizations allowed to use that software. ZITADEL's user ownership differs from a deployment-global membership model. |
| Auth0    | Organizations represent business customers/partners within an Auth0 tenant, support membership and business-specific login, and can support delegated administration. Organization authentication adds organization context to tokens.                      | An identity provider's “tenant” is not necessarily your application's customer tenant; organization context still requires resource-server validation.       |

Sources: [Keycloak server administration, organizations](https://www.keycloak.org/docs/latest/server_admin/#_organizations), [ZITADEL instances](https://zitadel.com/docs/concepts/structure/instance), [ZITADEL organizations](https://zitadel.com/docs/guides/manage/console/organizations-overview), [ZITADEL projects and grants](https://zitadel.com/docs/guides/manage/console/projects-overview), [Auth0 Organizations](https://auth0.com/docs/manage-users/organizations), [Auth0 organization tokens](https://auth0.com/docs/manage-users/organizations/using-tokens).

**Recommendation:** borrow the shared-directory membership idea from Keycloak/Auth0 and the product-versus-consumer distinction from ZITADEL. Do not reproduce their entire administrative hierarchies.

## Global users do not invalidate multi-tenancy

Authentication answers who the caller is. Membership and authorization decide where that caller can act. Microsoft explicitly describes a single identity accessing multiple tenants and cautions about leaking tenant-enriched profile data. [Microsoft multitenant identity considerations](https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/identity) The Keycloak example above demonstrates that a user can belong to multiple organizations within one identity realm. Application-data isolation does not require duplicating that person's credentials in every organization.

For Wallow, “global” should mean **this instance only**, never all independent forks. Keep credentials, login methods, MFA and account recovery in Identity. Keep organization-specific display information, employee identifiers, titles and permissions on membership/profile records owned by the relevant scope.

Choose and document the remaining identity boundary:

| Model                                 | Suitable when                                                                                       | Consequence                                                                                            |
| ------------------------------------- | --------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| Instance-wide User + memberships      | One trusted platform operator; users commonly collaborate across organizations or customer accounts | Simple SSO and account recovery; account disablement and credential compromise affect all memberships  |
| Tenant-owned identity directory/realm | Customers require independent account lifecycle, login policies or identity namespaces              | Memberships and credentials are tenant-local; cross-tenant use requires guest identities or federation |
| Separate instance                     | Customers require independent operator, issuer, infrastructure or upgrade control                   | Stronger operational separation; collaboration becomes an explicit integration                         |

These are Wallow design tradeoffs. Logical tenant separation cannot isolate customers from the operator who controls the application, database and signing keys.

**Recommended default:** instance-wide users, with tenant administrators unable to browse or mutate arbitrary global users. They may manage their own memberships and tenant-scoped configuration. Removing a membership must not delete the shared account. Make global account suspension a distinct platform operation. Tenant directory exports contain only permitted member views. Do not silently link identities by matching email: OIDC's stable identity key is the issuer/subject pair, while email is not guaranteed unique or stable. [OpenID Connect Core §5.7](https://openid.net/specs/openid-connect-core-1_0.html#ClaimStability)

If independent credentials, recovery or subject namespaces are mandatory, choose tenant-owned identities before expanding the schema. A shared user table is then a deliberate tradeoff, not equivalent to isolated identity realms.

## Proposed domain model

The following is a **candidate design**, not a required list of tables to add immediately:

| Entity                   | Ownership and constraints                                                                                           |
| ------------------------ | ------------------------------------------------------------------------------------------------------------------- |
| Tenant                   | Independent ID, lifecycle state, name; one default record in an internal installation                               |
| Organization             | Required TenantId; unique `(TenantId, OrganizationId)` relationship target                                          |
| User / ExternalIdentity  | Instance-owned; external identity keyed by issuer and subject                                                       |
| Membership               | `(TenantId, OrganizationId, UserId)` unique; status and organization roles; organization must belong to that tenant |
| TenantRoleAssignment     | Optional explicit tenant administration grant; do not manufacture tenant-wide data access from ordinary membership  |
| Product                  | Owned by an organization; add only when related clients need a common product/permission lifecycle                  |
| RegisteredClient         | Owned by an organization or its product; registration metadata and credentials, not a human membership              |
| ProductOrganizationGrant | Optional organization access to another organization's product; permissions delegated explicitly                    |
| Module-owned resource    | Required TenantId; OrganizationId when organization-owned; module-specific access policy                            |
| ResourceShare            | Optional owning/recipient organization, resource or collection, permitted action, lifecycle and revocation          |

Use tenant-qualified foreign keys within module-owned persistence to prevent a row in Tenant A referencing an organization in Tenant B. For organization-owned children, use an organization-qualified relationship as well. Scope names and uniqueness to their intended owner. IDs being difficult to guess is not authorization.

Do not put nullable OrganizationId on every table with an undocumented meaning of “null means shared.” Separate tenant-wide resources from organization-owned resources in the domain, or use an explicit ownership type with constraints. A tenant catalog, organization document and personal preference have different ownership even if they occupy the same database.

One company operating several products usually needs **one organization with several products/clients**, not a separate organization for each product. An in-process module does not need its own OAuth client merely because it is a module: register consuming applications and define API resources/audiences according to the actual protocol boundaries.

“Everyone in an organization has one set of information” means one organization-owned dataset, with individual permissions. It does not mean all users can edit every field. Likewise, a client registration does not own copies of all business data: multiple apps can access the same module APIs and organization-owned records.

For a shared app, distinguish **owner organization** from **allowed consuming organizations**. Keeping today's one-organization client is a reasonable initial product constraint; add product/client grants when a concrete app must serve more than one organization. Do not infer consumption rights from registration ownership.

## Request authorization and organization switching

Recommended flow:

1. Validate the access token's signature, issuer, audience and lifetime using the configured trust boundary.
2. Resolve the selected tenant and organization from server-validated context. Verify the organization's actual parent tenant. A URL, header or login hint is a selector, not proof of permission.
3. Verify active membership or an explicit service-principal grant, and verify the client may operate in that context.
4. Authorize the action against the target resource's tenant, owner organization and applicable grants.
5. Execute with immutable request scope; bind persistence and downstream work to that scope.
6. Record actor, client, tenant, organization, operation and relevant sharing decision in the audit record.

Auth0 explicitly requires checking the organization claim against the expected organization when organization authentication is used. The broader flow above is a Wallow recommendation. [Auth0 organization-token validation](https://auth0.com/docs/manage-users/organizations/using-tokens)

Organization switching should obtain a newly validated context, and new organization-bound tokens where those tokens carry the selected organization. Never union roles from all memberships into a token and apply them to whichever organization the caller later names. Refresh must recheck applicable membership/client access; define revocation latency for already issued tokens. Use bounded token lifetimes and live checks for sensitive operations according to that requirement.

Client-credentials callers need explicit service grants. They have no human membership to inherit. A permitted OAuth scope is still insufficient if the resource belongs to a different tenant or ungranted organization.

## Sharing within a tenant

**Same tenant is necessary for default intra-tenant sharing, but it is not an access grant.** A TenantId filter alone would allow sibling-organization reads unless another boundary is enforced.

Begin with organization-private data. Add either:

- A tenant-owned resource deliberately available through a tenant-level permission, such as an approved common reference catalog.
- An explicit organization-to-organization grant for a resource, collection or product, such as Manufacturing sharing inventory availability read-only with Logistics.

For a shared document, require matching tenant, a valid recipient grant, and the caller's permission in the recipient organization. Distinguish read, edit, export and delegation. A product grant permits use of a software solution; it does not automatically share every record owned by its publisher.

The owning module should expose an authorized sharing/query API. Cross-organization reporting uses an explicitly authorized projection or query over a defined organization set; it must not turn off tenant filters. Grant removal must affect future reads, cached results and pending work under a documented policy. Separate copied/exported data from live access: revocation cannot retrieve an already delivered copy.

## Data isolation choices

AWS describes silo, pooled and mixed isolation patterns. Microsoft treats isolation as a spectrum across compute, data and other infrastructure. No source establishes one universal best model for a fork-first product. [AWS isolation concepts](https://docs.aws.amazon.com/whitepapers/latest/saas-tenant-isolation-strategies/core-isolation-concepts.html), [Microsoft tenancy models](https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/tenancy-models)

| Storage/deployment choice       | Strength                                                          | Cost or limitation                                                                  | Wallow position                                                            |
| ------------------------------- | ----------------------------------------------------------------- | ----------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| Shared database, tenant columns | One operational database and straightforward intra-tenant queries | Isolation must cover every access path; shared resource contention                  | Recommended initial implementation if logical isolation meets requirements |
| Database per tenant             | Clear database boundary; per-tenant backup/restore placement      | Routing, provisioning and migrations across databases; shared process still trusted | Later option driven by actual isolation/restore needs                      |
| Schema per tenant               | Namespace separation within a database                            | Tenant schema proliferation and migration complexity                                | Avoid as default; EF Core does not directly support this model             |
| Separate deployment per tenant  | Independent compute/database lifecycle                            | More infrastructure and cross-installation integration                              | Natural choice for independent fork operators or stronger isolation needs  |

EF Core documents tenant-column query filters and database selection by connection string; it explicitly says its examples are working practices, not a complete best-practice architecture. [EF Core multi-tenancy](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy)

**Recommendation for the shared database:** use application authorization, database constraints, centrally applied query scope and explicit write validation. EF global filters can be disabled with `IgnoreQueryFilters`; they are query conveniences, not a complete write authorization mechanism. Reject unresolved business scope and cross-scope mutations, including attached entities and bulk/raw paths. Keep privileged platform operations separate and audited. [EF Core global query filters](https://learn.microsoft.com/en-us/ef/core/querying/filters)

PostgreSQL RLS can add a database enforcement layer. Enabled tables default-deny without policies; `USING` governs visible rows and `WITH CHECK` governs accepted new row values. Superusers and `BYPASSRLS` bypass it; owners normally bypass unless forced. Whole-table operations and referential checks have exceptions. [PostgreSQL row-security policies](https://www.postgresql.org/docs/current/ddl-rowsecurity.html)

**Recommendation:** when adding RLS, test using the actual non-owner runtime role. Carry verified scope transaction-locally; ensure pooled connections never inherit another request's context. A tenant-only policy still needs organization authorization. RLS using an application-controlled tenant setting protects against omitted predicates, not a fully compromised application able to choose that setting. Budget this work as a coherent enforcement feature rather than claiming isolation from merely enabling RLS.

## Modular monolith boundaries and non-HTTP work

The following are proposed engineering invariants. OWASP's multi-tenant guidance independently covers tenant context, storage/cache separation and tenant-aware audit controls. [OWASP multi-tenant security](https://cheatsheetseries.owasp.org/cheatsheets/Multi_Tenant_Security_Cheat_Sheet.html)

| Surface           | Required invariant                                                                                                                             |
| ----------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| Module API        | Accept validated execution scope; authorize its own resources; do not expose another module's DbContext                                        |
| Database          | Owning module controls writes; scope applies to reads, writes, bulk operations and administrative paths                                        |
| Events/outbox     | Envelope includes tenant, owning organization when applicable, actor/service identity and event ID; persisted atomically with business changes |
| Jobs/retries      | Restore fresh scope for each job; distinguish user-delegated work from authorized system work; tenant-qualified idempotency keys               |
| Cache/search      | Tenant and relevant organization/access scope in keys or partitions; permission-aware results and invalidation                                 |
| Files/blobs       | Ownership metadata plus checked upload/download/delete; scoped object names organize storage but do not authorize access                       |
| Realtime/webhooks | Validate subscription and destination scope; authorize payloads before delivery                                                                |
| Audit/operations  | Tenant-scoped audit visibility and exports; explicit platform privileges, quotas, restore and deletion procedures                              |

A job must not inherit the previous job's ambient tenant. A trusted business event is not a reusable end-user bearer token: consumers apply their defined service authority, and user-delegated delayed actions recheck permissions when required. Dead-letter replay must preserve scope and idempotency.

Modules can share one process and database while owning their tables and publishing contracts. Preserve Wallow's existing integration-events-only cross-module rule: use durable integration events and module-owned projections, with explicit delayed-consistency behavior. Keep event payloads independent of ORM entities. Do not introduce direct cross-module table writes. Synchronous cross-module contracts are an alternative architecture requiring an explicit decision, not a prerequisite for multi-tenancy. [Wallow domain and integration rules](../../../CONTEXT.md)

This makes later extraction easier; it does not make extraction free. Moving a module introduces network failures, independent credentials, contract versions and consistency decisions. Add tenant/org context to contracts now, while deferring service discovery and distributed infrastructure until extraction is needed.

## What this changes in today's Wallow

Repository evidence inspected during this research:

- [CONTEXT.md](../../../CONTEXT.md) describes organization and tenant as 1:1, with global users, organization memberships and organization-bound clients.
- [Organization](../../../api/src/Modules/Identity/Wallow.Identity.Domain/Entities/Organization.cs) currently forces `TenantId = Id`; its factory ignores the supplied tenant ID.
- [Tenant resolution](../../../api/src/Modules/Identity/Wallow.Identity.Infrastructure/MultiTenancy/TenantResolutionMiddleware.cs) currently treats organization context as tenant context, with a privileged header override.
- [TenantAwareDbContext](../../../api/src/Shared/Wallow.Shared.Infrastructure.Core/Persistence/TenantAwareDbContext.cs) filters tenant-scoped rows by TenantId. [TenantSaveChangesInterceptor](../../../api/src/Shared/Wallow.Shared.Kernel/MultiTenancy/TenantSaveChangesInterceptor.cs) stamps added entities and suppresses tenant-ID changes, but returns when scope is unresolved. This is not proof of complete write isolation.
- [DefaultTenantConnectionResolver](../../../api/src/Shared/Wallow.Shared.Kernel/MultiTenancy/DefaultTenantConnectionResolver.cs) returns the default connection regardless of tenant, so the abstraction does not itself demonstrate database-per-tenant routing.
- [Membership](../../../api/src/Modules/Identity/Wallow.Identity.Domain/Entities/Membership.cs) is organization-scoped without an ambient tenant filter; [RegisteredClient](../../../api/src/Modules/Identity/Wallow.Identity.Domain/Entities/RegisteredClient.cs) has OrganizationId.
- [Message stamping](../../../api/src/Shared/Wallow.Shared.Infrastructure.Core/Middleware/TenantStampingMiddleware.cs) and [restoring](../../../api/src/Shared/Wallow.Shared.Infrastructure.Core/Middleware/TenantRestoringMiddleware.cs) carry tenant scope, not a distinct organization authorization context.

Deletion is another coupled assumption: the [Branding organization-deletion handler](../../../api/src/Modules/Branding/Wallow.Branding.Infrastructure/Handlers/OrganizationDeletedHandler.cs) derives tenant scope from OrganizationId and removes branding rows in that scope. If scope were changed to the shared parent tenant without redesigning ownership and purge predicates, deleting one organization could purge sibling data. This is a future-change hazard, not evidence of current sibling leakage. Storage files/buckets also currently have TenantId without a separate OrganizationId, so their intended ownership must be decided explicitly.

**Consequence:** changing only Organization.TenantId would collapse existing sibling-organization protection wherever TenantId is the sole predicate. Before allowing two organizations in one tenant, classify every existing resource as instance-, tenant-, organization-, or user-owned, and enforce that ownership. Token claims, tenant resolution, memberships, client access, query/write boundaries and messaging must change coherently. This is more than introducing a parent table.

## Suggested sequence, without a hosted-platform commitment

1. **Decide the domain boundary.** Record whether tenant means an independent customer account containing organizations, and whether identities remain instance-wide. Distinguish Product from Organization. Update domain documentation only after that decision.
2. **Establish one coherent model.** If the nested requirement is accepted, add a real tenant identity, bootstrap one tenant, reclassify resource ownership, and change request/message scope plus authorization together. Use the repository's pre-release schema replacement policy rather than inventing production migration machinery.
3. **Prove sibling and tenant isolation.** With two tenants and two organizations in one tenant, exercise public APIs and real persistence: reads, writes, membership removal, client grants, forged scope, organization switching, jobs, cache, files and privileged operations. Include missing-context and runtime-database-role cases. These are behavioral/integration scenarios, not source-string tests.
4. **Add one concrete sharing capability.** Choose a real dataset and explicitly model read sharing; prove grant revocation and recipient boundaries before generalizing.
5. **Expand only for an actual need.** Add Product when several clients share its lifecycle; database placement when restore/isolation requirements demand it; separate identity realms when shared account lifecycle is unacceptable; extract a module when operational needs justify distribution.

The immediate decision is therefore not “is Wallow a SaaS?” It is whether one installation must represent independent customer accounts containing multiple organizations. The requested model can do that while remaining fork-first and internally deployed. It should promise a clearly defined logical isolation model, with stronger identity and infrastructure separation offered only where implemented and verified.
