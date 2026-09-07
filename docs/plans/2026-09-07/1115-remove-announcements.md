**status: active**

# Remove announcements and changelogs implementation plan

> Use superpowers:executing-plans to implement this plan task by task. This document authorizes planning only; implementation is a separate task.

**Goal:** Remove the unused Announcements module, including changelogs, and all supporting runtime code and generated contracts.

**Architecture:** Delete the module outright. Preserve Notifications and shared realtime delivery, removing their announcement-specific integration. Wallow is local-only and pre-release, so fresh databases and updated seeds replace compatibility migrations or rollout flags.

**Tech stack:** .NET 10, EF Core, Wolverine, PostgreSQL, OpenAPI, TypeScript, pnpm.

## Scope and evidence

- No announcement or changelog references were found in `apps/`.
- `api/src/Modules/Announcements/` owns both announcement and changelog controllers, persistence, migrations, and handlers.
- Notifications consumes `AnnouncementPublishedEvent`. Its `BroadcastToTenantAsync` method emits `AnnouncementPublished` but has no production callers anywhere in `api/`.
- `scripts/fork-smoke/src/routes/index.tsx` imports `changelogGetChangelogQueryKey` to verify packaged query exports. It makes no API request. Replace this dependency rather than remove the smoke coverage.
- Shared permissions/scopes, seeds, module registries, architecture tests, documentation, and generated clients also mention the module.
- Keep release changelog files and tooling, historical plans/archives, and unrelated scroll-area story content named “changelog.”
- Existing observability changes in the workspace are unrelated and must remain outside this work.

## Task 1: Remove the module and host wiring

1. Delete `api/src/Modules/Announcements/` and `api/tests/Modules/Announcements/`, including module-owned migrations and snapshots.
2. Remove Announcements project entries from `api/Wallow.slnx` and project references from:
   - `api/src/Wallow.Api/Wallow.Api.csproj`
   - `api/src/Wallow.Modules.Registry/Wallow.Modules.Registry.csproj`
   - `api/tests/Wallow.Architecture.Tests/Wallow.Architecture.Tests.csproj`
3. Remove its import/entry from `api/src/Wallow.Modules.Registry/WallowModuleRegistry.cs` and controller assembly mapping from `api/src/Wallow.Api/WallowModules.cs`.
4. Remove `Modules.Announcements` from `api/src/Wallow.Api/appsettings.json` and `appsettings.Production.json`.
5. Remove the announcements shorthand and update help/comments in `scripts/run-tests.sh`.

The shared registry drives migrations too. Do not add a drop-schema migration to another module. Existing disposable local databases may retain unused tables until recreated; fresh databases must never create the announcements schema.

## Task 2: Remove notification integration and authorization

1. Delete `api/src/Shared/Wallow.Shared.Contracts/Announcements/` and `api/src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/AnnouncementPublishedNotificationHandler.cs`.
2. Remove `NotificationType.Announcement` from `api/src/Modules/Notifications/Wallow.Notifications.Domain/Enums/NotificationType.cs`. Preserve the explicit values of other enum members.
3. Remove the unused `BroadcastToTenantAsync` declaration from `api/src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Interfaces/INotificationService.cs` and its implementation/logging from `api/src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/SseNotificationService.cs`. Preserve `SendToUserAsync` and shared `ISseDispatcher` tenant delivery.
4. Remove `AnnouncementRead`, `AnnouncementManage`, and `ChangelogManage` from these authorization files under `api/src/Shared/Wallow.Shared.Kernel/Identity/Authorization/`:
   - `PermissionType.cs`
   - `RolePermissionMapping.cs`
   - `ScopePermissionMapper.cs`
5. Remove `announcements.read`, `announcements.manage`, and `changelog.manage` from:
   - `api/src/Shared/Wallow.Shared.Contracts/Identity/ApiScopes.cs`
   - `api/src/Modules/Identity/Wallow.Identity.Infrastructure/Data/ApiScopeSeeder.cs`
   - `api/src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs`
   - `api/seed.json`
   - `docker/seed.production.json`
6. Inspect remaining migration references before editing. Remove only module-specific schema artifacts; do not rewrite unrelated Notifications migrations merely because a test used announcement sample data.

## Task 3: Keep surviving test coverage meaningful

1. Delete `api/tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/AnnouncementPublishedNotificationHandlerTests.cs` and the broadcast-specific cases in `Infrastructure/Services/SseNotificationServiceTests.cs` under that test project. Preserve user-delivery tests.
2. Replace announcement enum fixtures in that project's `Domain/Entities/NotificationCreateTests.cs`, `Application/Commands/InApp/ArchiveNotificationHandlerTests.cs`, and `Infrastructure/Persistence/EmailPreferenceRepositoryTests.cs` with an appropriate surviving notification type.
3. Update surviving-module expectations in `api/tests/Wallow.Architecture.Tests/`: `ModuleRegistrationTests.cs`, `MultiTenancyArchitectureTests.cs`, and `Modules/{ModuleRegistryTests,ModuleSchemaTests,ModuleToggleTests}.cs`. Keep optional-module toggle behavior covered using a surviving optional module.
4. Minimally remove the deleted path from the existing `MigrationRemovalTests.cs` source test. Add no source-text assertions or new source tests.
5. Update affected scope/permission expectations in `api/tests/Wallow.Shared.Kernel.Tests/Identity/ScopePermissionMapperTests.cs`, `api/tests/Modules/ApiKeys/Wallow.ApiKeys.Tests/{Authorization/ApiKeyPermissionExpansionTests,Controllers/ApiKeysControllerScopeValidationTests}.cs`, and Identity's `Infrastructure/{ApiScopeSeederGapTests,PermissionExpansionMiddlewareGapTests,PermissionExpansionMiddlewareTests}.cs` under `api/tests/Modules/Identity/Wallow.Identity.Tests/`.
6. Review `api/tests/Wallow.MigrationService.Tests/MigrationServiceTests.cs`, `api/tests/Wallow.Shared.Infrastructure.Tests/Contracts/ContractEventsTests.cs`, and `api/tests/Wallow.Api.Tests/{Logging/ModuleEnricherTests,Middleware/ApiVersionRewriteMiddlewareTests}.cs`. Remove deleted-contract cases and replace generic examples with surviving modules while preserving the behavior under test.
7. Run `dotnet build api/Wallow.slnx`, then `./scripts/run-tests.sh`. Resolve failures from this removal without weakening unrelated assertions.

## Task 4: Regenerate clients and repair fork smoke

1. Start the updated backend with the documented local setup, using a fresh disposable database and updated seeds. Verify migration and seeder completion.
2. Refresh the snapshot and SDK using the repository command:

   ```sh
   WALLOW_OPENAPI_URL=http://localhost:5001/openapi/v1.json pnpm --filter @bc-solutions-coder/sdk exec tsx scripts/generate.ts
   pnpm --filter @bc-solutions-coder/api-errors generate
   ```

3. Review changes to `packages/sdk/openapi/v1.json`, `packages/sdk/src/generated/`, and `packages/api-errors/src/generated/`. Do not hand-edit generated output. Announcement/changelog operations, DTOs, and error codes must be absent.
4. In `scripts/fork-smoke/src/routes/index.tsx`, replace `changelogGetChangelogQueryKey` with a surviving generated query-key function callable without required arguments. Keep the import from `@bc-solutions-coder/sdk/query`, rendered query key, and no-backend behavior.
5. Run `pnpm check` and `./scripts/fork-smoke.sh`. Both must pass, including packaged query exports.

## Task 5: Update current documentation

Use `rg -n -i 'announcement|changelog' README.md CONTRIBUTING.md CONTEXT.md api docs scripts docker packages` to find remaining references and classify each result.

Update current module lists, endpoint/scope examples, configuration tables, and migration/test instructions in `README.md`, `CONTRIBUTING.md`, `CONTEXT.md`, `api/README.md`, `api/src/Modules/Notifications/README.md`, `api/src/Shared/Wallow.Shared.Contracts/README.md`, and affected guides under `docs/{api,getting-started,architecture,development,operations}/`. Check test-project READMEs too. Replace generic examples with surviving endpoints. Preserve historical release records, agent archives, and this plan.

## Task 6: Verify the complete removal

1. Run `./scripts/run-tests.sh integration` with Docker available. This must exercise surviving handler code generation and database integration after registry removal.
2. Inspect the running API's OpenAPI document to confirm all four route families are absent: `/v1/announcements`, `/v1/admin/announcements`, `/v1/changelog`, and `/v1/admin/changelog`, including child operations.
3. Confirm fresh startup/migrations/seeding complete, no announcements schema is created, and surviving Notifications user delivery and module registration tests pass. If checking removed URLs directly, use an authenticated request so authorization middleware does not mask route absence.
4. Inspect remaining references with `rg`; every remaining match must be historical documentation, unrelated release/UI text, or a deliberate test of rejection. Do not create tests that inspect implementation source strings.
5. Run `dotnet format api/Wallow.slnx` and review its diff before staging. Rerun affected checks if formatting or fixes changed code after validation.
6. Review `git diff --check` and the staged diff. Stage only removal-related files. Never commit `issues.jsonl`; inspect/remove it only if status, staged output, or hooks reveal it.
7. Commit the validated implementation with a breaking-change conventional message, for example `feat(announcements)!: remove announcements and changelogs`. Mark this plan completed only after implementation and verification succeed. Report any unavailable verification explicitly.

## Completion criteria

The module and its runtime dependencies are gone; neither feature appears in OpenAPI or generated clients; frontend apps and fork smoke still build; fresh migrations and seeds succeed; backend fast/integration suites and `pnpm check` pass. General Notifications and realtime infrastructure continue to work. No feature flags, deprecated endpoints, or replacement changelog module are introduced.
