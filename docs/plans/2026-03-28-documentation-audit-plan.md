# Documentation Audit & Cleanup Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:dispatching-parallel-agents to execute Phase 1.

**Goal:** Bring all repo documentation to enterprise-release quality — accurate, current, non-duplicative, well-organized.

**Architecture:** Phase 0 deletes known-stale files. Phase 1 dispatches ~18 parallel agents to audit and fix files. Phase 2 consolidates restructuring recommendations. Phase 3 cleans up cross-references.

**Tech Stack:** Markdown, git, grep/glob for code verification

---

## Phase 0: Deletions

**Step 1: Delete plan files (keep walkthrough + this audit)**

```bash
# Delete all plans except walkthrough and audit docs
rm docs/plans/2026-03-25-audience-scoped-realtime-design.md
rm docs/plans/2026-03-25-audience-scoped-realtime.md
rm docs/plans/2026-03-25-blazor-testing-design.md
rm docs/plans/2026-03-25-blazor-testing-plan.md
rm docs/plans/2026-03-26-e2e-test-overhaul-design.md
rm docs/plans/2026-03-26-e2e-test-overhaul.md
rm docs/plans/2026-03-26-tenant-aware-dbcontext-fix-design.md
rm docs/plans/2026-03-27-mfa-overhaul-design.md
rm docs/plans/2026-03-27-mfa-overhaul.md
rm docs/plans/2026-03-27-optional-clamav-design.md
rm docs/plans/2026-03-27-optional-clamav-plan.md
```

**Step 2: Delete Serena memories**

```bash
rm .serena/memories/audit-workflow-structure.md
rm .serena/memories/code_style_conventions.md
rm .serena/memories/compiled-queries-research-findings.md
rm .serena/memories/module-developer-guide.md
rm .serena/memories/module_registration_flow.md
rm .serena/memories/project_overview.md
rm .serena/memories/suggested_commands.md
rm .serena/memories/task_completion_checklist.md
rm .serena/memories/test-architecture-audit-findings.md
```

**Step 3: Delete redundant files**

```bash
rm AGENTS.md
rm src/Modules/Billing/CLAUDE.md
rm src/Modules/Identity/CLAUDE.md
rm src/Modules/Storage/CLAUDE.md
rm src/Shared/Wallow.Shared.Contracts/CLAUDE.md
rm src/Shared/Wallow.Shared.Kernel/CLAUDE.md
rm src/Wallow.Api/CLAUDE.md
rm tests/CLAUDE.md
```

**Step 4: Commit deletions**

```bash
git add -A
git commit -m "docs: remove stale plans, serena memories, and redundant CLAUDE.md files"
```

---

## Phase 1: Parallel Audit Agents

Each agent receives these universal instructions:

> **Audit instructions for all agents:**
> 1. Read your assigned file(s) completely
> 2. For every claim (file paths, class names, config keys, commands, URLs), verify it exists in the codebase using grep/glob
> 3. Remove or fix anything that references things that don't exist
> 4. Remove future-tense plans ("we will add...", "planned for...", "TODO") — only document what exists NOW
> 5. Remove AI filler and verbose explanations. Write clear, concise, professional prose
> 6. Ensure consistent terminology (e.g., always "Wolverine" not "MassTransit", always "module" not "service")
> 7. If you find content that belongs in a different file, note it as a comment at the bottom: `<!-- RESTRUCTURE: move X to Y -->`
> 8. If the file should be deleted entirely (no useful content), replace contents with: `<!-- DELETE: reason -->`
> 9. Ensure proper markdown formatting, no broken links
> 10. **Minimize code snippets** — only include code when prose alone cannot explain the concept. Remove excessive examples, boilerplate dumps, and "here's what it looks like" snippets. A single focused example beats three redundant ones. Configuration keys and CLI commands are fine; full class implementations are not (unless it's a pattern guide)
> 11. Do NOT add new content or features — only fix, remove, or clarify existing content

### Agent 1: README.md

**Files:** `/README.md`

**Extra context:** This is the first thing developers and enterprise evaluators see. It must:
- Accurately describe what Wallow is
- List actual modules that exist in `src/Modules/`
- Show correct setup commands (verify docker compose commands, dotnet run commands)
- Have correct URLs for local services (cross-check with `docker/.env` and `docker/docker-compose.yml`)
- Not reference features that don't exist yet
- Look polished and professional — this is the storefront

### Agent 2: Root CLAUDE.md

**Files:** `/CLAUDE.md`

**Extra context:** This is loaded into every AI conversation. It must be:
- 100% accurate for every command, path, and module name listed
- Verify all module names match `src/Modules/` directories
- Verify all commands actually work (check scripts exist, project paths are correct)
- Verify local development URLs and credentials against `docker/docker-compose.yml` and `docker/.env`
- Verify documentation file paths listed actually exist (especially after Phase 0 deletions)
- Check the "Fork-First Architecture" section — verify `branding.json` exists, `BrandingOptions` class exists where stated
- Keep it concise — agents read this every conversation

### Agent 3: CONTRIBUTING.md + CODE_OF_CONDUCT.md + SECURITY.md

**Files:** `/CONTRIBUTING.md`, `/CODE_OF_CONDUCT.md`, `/SECURITY.md`

**Extra context:** Standard open-source files. Verify:
- CONTRIBUTING.md references correct branch names, PR process, test commands
- CODE_OF_CONDUCT.md is appropriate and complete
- SECURITY.md has correct contact info and reporting process
- All three should reference the project name consistently

### Agent 4: Getting Started docs

**Files:**
- `docs/getting-started/configuration.md`
- `docs/getting-started/developer-guide.md`
- `docs/getting-started/fork-guide.md`
- `docs/getting-started/onboarding.md`

**Extra context:** These are the onboarding path. Verify:
- All config keys mentioned exist in `appsettings.json` or `appsettings.Development.json`
- Docker compose commands are correct
- Prerequisites (SDK versions, tools) are current — check `global.json` and `Directory.Build.props`
- Step-by-step instructions actually work in order
- No duplicate content between developer-guide and onboarding (consolidate if needed)

### Agent 5: Architecture — assessment + authorization

**Files:**
- `docs/architecture/assessment.md`
- `docs/architecture/authorization.md`

**Extra context:** Verify:
- Assessment references actual current architecture, not planned/aspirational
- Authorization doc matches actual auth implementation — check `src/Modules/Identity/` and `src/Shared/Wallow.Shared.Kernel/Extensions/ClaimsPrincipalExtensions.cs`
- Policy names, claim types, role names match code

### Agent 6: Architecture — background-jobs, caching, file-storage

**Files:**
- `docs/architecture/background-jobs.md`
- `docs/architecture/caching.md`
- `docs/architecture/file-storage.md`

**Extra context:** Verify:
- Background jobs doc matches Wolverine scheduling/hosted services in code
- Caching doc references Valkey correctly (not Redis), check actual cache usage patterns
- File storage doc matches GarageHQ/S3 setup, verify ClamAV is documented as optional (recent change)

### Agent 7: Architecture — messaging, realtime, workflows

**Files:**
- `docs/architecture/messaging.md`
- `docs/architecture/realtime.md`
- `docs/architecture/workflows.md`

**Extra context:** CRITICAL:
- Messaging MUST say Wolverine in-memory bus, NOT RabbitMQ. Verify no RabbitMQ references remain
- Realtime doc should match SignalR hub implementations in code
- Workflows doc should match actual Wolverine saga/workflow implementations

### Agent 8: Module creation consolidation

**Files:**
- `docs/architecture/module-creation.md`
- `.claude/docs/module-creation.md`

**Extra context:** These likely overlap. Your job:
1. Read both files completely
2. Determine which content is unique to each
3. Merge into ONE file at `docs/architecture/module-creation.md` (the docs site location)
4. The `.claude/docs/` version should be replaced with a one-liner pointing to the docs site version, OR deleted if fully redundant
5. Verify all referenced file paths, class names, and patterns match actual module structure

### Agent 9: Development docs

**Files:**
- `docs/development/api-development.md`
- `docs/development/database-development.md`
- `docs/development/database-migrations.md`
- `docs/development/frontend-setup.md`
- `docs/development/testing.md`

**Extra context:** Verify:
- API development patterns match actual controllers/endpoints
- Database docs reference correct EF Core patterns, check migration command matches CLAUDE.md
- Frontend setup matches actual Blazor project structure (`Wallow.Auth`, `Wallow.Web`)
- Testing doc matches `./scripts/run-tests.sh` usage and test project structure

### Agent 10: Operations docs

**Files:**
- `docs/operations/deployment.md`
- `docs/operations/observability.md`
- `docs/operations/troubleshooting.md`
- `docs/operations/versioning.md`

**Extra context:** Verify:
- Deployment doc matches actual Docker/CI/CD setup — check `.github/workflows/`, `docker/`
- Observability matches Grafana setup, check if OpenTelemetry config is accurate
- Versioning matches release-please config — check `.release-please-manifest.json` and `release-please-config.json`
- ClamAV is documented as optional in deployment (recent change)

### Agent 11: Integration docs

**Files:**
- `docs/integrations/asyncapi.md`
- `docs/integrations/dcr-integration.md`
- `docs/integrations/external-auth.md`

**Extra context:** Verify:
- AsyncAPI doc matches actual event contracts in `src/Shared/Wallow.Shared.Contracts/`
- DCR integration is actually implemented (check for Dynamic Client Registration code)
- External auth doc matches actual OIDC/OAuth implementation

### Agent 12: Docs site meta files

**Files:**
- `docs/api/service-accounts.md`
- `docs/index.md`
- `docs/CLAUDE.md`
- `docs/toc.yml`

**Extra context:**
- `docs/CLAUDE.md` — agent instructions for docs work, verify still accurate
- `docs/index.md` — landing page, must be accurate and professional
- `toc.yml` — must reference all files that exist and none that don't (especially after Phase 0 deletions)
- `service-accounts.md` — verify API key/service account feature exists in code

### Agent 13: .claude/rules/*

**Files:** All 9 files in `.claude/rules/`
- COMMITS.md, CRITICAL.md, DOCUMENTATION.md, E2E.md, FAILSAFES.md, GENERAL.md, LOGGING.md, TEAMS.md, TESTING.md

**Extra context:** These are loaded into every AI conversation. They must be:
- 100% accurate — every rule must match actual codebase conventions
- Verify E2E rules match actual test base classes (check `tests/Wallow.E2E.Tests/`)
- Verify TESTING.md module shorthands match `./scripts/run-tests.sh`
- Verify LOGGING.md pattern matches actual usage in codebase
- Verify GENERAL.md ClaimsPrincipalExtensions methods match actual code
- Remove any rules that reference deleted files or defunct patterns

### Agent 14: .claude/docs — communications + simplification

**Files:**
- `.claude/docs/communications-channels.md`
- `.claude/docs/module-simplification.md`

**Extra context:**
- `communications-channels.md` is 24KB — likely contains a lot. Determine: is this still relevant? Does it describe actual notification/messaging channels? If outdated, pare down drastically or delete
- `module-simplification.md` — verify the simplification patterns it describes match current module structure. If the simplifications have already been applied, this doc may be stale

### Agent 15: .claude/agents/*

**Files:** All 5 agent definitions:
- `codebase-explorer.md`
- `csharp-bead-implementer.md`
- `devops-engineer.md`
- `enterprise-architect.md`
- `test-automator.md`

**Extra context:** Verify:
- Agent instructions reference correct tools, paths, and patterns
- No references to deleted files (AGENTS.md, .serena/memories, module CLAUDE.md files)
- Instructions match current project conventions

### Agent 16: Module READMEs

**Files:**
- `src/Modules/Billing/README.md`
- `src/Modules/Identity/README.md`
- `src/Modules/Storage/README.md`
- `src/Shared/README.md`
- `src/Shared/Wallow.Shared.Contracts/README.md`
- `src/Shared/Wallow.Shared.Infrastructure/README.md`
- `src/Shared/Wallow.Shared.Kernel/README.md`
- `src/Wallow.Api/README.md`

**Extra context:**
- Each README should accurately describe its module/project
- Verify listed features, classes, and patterns actually exist
- Consolidate any valuable info from the deleted CLAUDE.md files into these READMEs
- Check if there are modules without READMEs that should have them (Notifications, Messaging, Announcements, Inquiries)

### Agent 17: Test docs

**Files:**
- `tests/Wallow.Tests.Common/README.md`
- `tests/Modules/Identity/Wallow.Identity.IntegrationTests/README.md`

**Extra context:**
- Verify test utilities described actually exist
- Verify integration test setup instructions are current
- Note: `tests/CLAUDE.md` was deleted in Phase 0 — check if anything valuable needs to move to these READMEs

### Agent 18: PR template

**Files:** `.github/PULL_REQUEST_TEMPLATE.md`

**Extra context:**
- Verify checklist items match actual project requirements
- Ensure it references correct test commands and review process

---

## Phase 2: Restructuring

After all Phase 1 agents complete, collect all `<!-- RESTRUCTURE: ... -->` and `<!-- DELETE: ... -->` comments.

**Step 1:** Review restructuring recommendations across all agents
**Step 2:** Execute file moves, merges, and deletions
**Step 3:** Update `docs/toc.yml` to reflect final structure
**Step 4:** Commit: `docs: restructure documentation based on audit findings`

---

## Phase 3: Cross-Reference Cleanup

**Step 1:** Grep all .md files for internal links and verify targets exist
**Step 2:** Grep for references to deleted files and fix them
**Step 3:** Verify `docs/toc.yml` matches actual file structure
**Step 4:** Final commit: `docs: fix cross-references and update toc`

---

## Phase 4: Final Review & Push

**Step 1:** `git status` — review all changes
**Step 2:** `git diff --stat` — sanity check scope
**Step 3:** `git push`
