# Documentation Audit & Cleanup Design

**Date:** 2026-03-28
**Goal:** Bring all documentation to enterprise-release quality: accurate, current, non-duplicative, well-organized.

## Scope

### Deletions (No Audit Needed)

- `docs/plans/*` except `2026-03-25-codebase-walkthrough-design.md` and this file
- `.serena/memories/*` (all 8 files)
- `AGENTS.md` (redundant with CLAUDE.md)
- All module-level `CLAUDE.md` files (Billing, Identity, Storage, Shared/Contracts, Shared/Kernel, Api)

### Files to Audit & Update (~40 files)

| Tier | Files | Focus |
|------|-------|-------|
| T1: Root docs | README.md, CLAUDE.md, CONTRIBUTING.md, CODE_OF_CONDUCT.md, SECURITY.md, .github/PULL_REQUEST_TEMPLATE.md | Accuracy, consistency, enterprise polish |
| T2: Docs site | 25 files in docs/ + toc.yml + docs/CLAUDE.md | Verify against code, remove stale references, flag restructuring |
| T3: .claude/ config | 3 docs + 9 rules + 5 agent definitions | Accuracy, remove references to deleted files |
| T4: Module READMEs | 8 README.md in src/ | Verify current module state, consolidate from deleted CLAUDE.md |
| T5: Test docs | tests/CLAUDE.md, test READMEs | Verify test instructions match current setup |

### Consolidation Decisions

- `.claude/docs/module-creation.md` vs `docs/architecture/module-creation.md` — merge into one location
- `.claude/docs/module-simplification.md` — determine if docs site or agent-only
- `.claude/docs/communications-channels.md` — audit for relevance, consolidate or delete

## Agent Architecture

1 coordinator + ~18 parallel audit agents, each focused on 1-3 related files.

### Phase 0: Deletions
Direct file removal, no agents needed.

### Phase 1: Parallel Audit Agents

| Agent | Assigned Files |
|-------|---------------|
| 1 | README.md |
| 2 | CLAUDE.md (root) |
| 3 | CONTRIBUTING.md, CODE_OF_CONDUCT.md, SECURITY.md |
| 4 | docs/getting-started/* (4 files) |
| 5 | docs/architecture/assessment.md, authorization.md |
| 6 | docs/architecture/background-jobs.md, caching.md, file-storage.md |
| 7 | docs/architecture/messaging.md, realtime.md, workflows.md |
| 8 | docs/architecture/module-creation.md + .claude/docs/module-creation.md (consolidation) |
| 9 | docs/development/* (5 files) |
| 10 | docs/operations/* (4 files) |
| 11 | docs/integrations/* (3 files) |
| 12 | docs/api/service-accounts.md, docs/index.md, docs/CLAUDE.md, toc.yml |
| 13 | .claude/rules/* (9 files) |
| 14 | .claude/docs/communications-channels.md, module-simplification.md |
| 15 | .claude/agents/* (5 files) |
| 16 | Module READMEs (8 files in src/) |
| 17 | Test docs (tests/CLAUDE.md + test READMEs) |
| 18 | .github/PULL_REQUEST_TEMPLATE.md |

### Phase 2: Restructuring
Review all agent outputs, propose and execute file moves/consolidation.

### Phase 3: Cross-Reference Cleanup
Update toc.yml, fix broken links, verify all inter-doc references.

## Quality Standards

- No references to things that don't exist in the codebase
- No future tense plans ("we will add...") — only what exists now
- No stale configuration examples
- Consistent terminology across all docs
- Clear, concise prose
- Proper markdown formatting
