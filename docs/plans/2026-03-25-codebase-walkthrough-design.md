# Codebase Walkthrough & Audit Design

**Date:** 2026-03-25
**Goal:** Walk through every file in the Wallow codebase top-down, layer-by-layer, to build a complete mental model and identify issues (dead code, missing tests, inconsistencies).

## Approach

**Top-down, layer-by-layer, with inline tests.**

Each session covers one layer of one area. Every file is reviewed for its purpose and how it connects to the rest of the system. Corresponding tests are reviewed immediately after the source files.

## Walkthrough Order

1. Wallow.Api (entry point, middleware, DI, hubs)
2. Shared.Kernel (core abstractions)
3. Shared.Contracts (cross-module DTOs/events)
4. Shared Infrastructure (6 projects: Core, Plugins, Workflows, BackgroundJobs, Infrastructure, Api)
5. Identity (Domain, Application, Infrastructure, Api — each with tests)
6. Billing (same pattern)
7. Notifications (same pattern)
8. Storage (same pattern)
9. Inquiries (same pattern)
10. Messaging (same pattern)
11. Announcements (same pattern)
12. ApiKeys (same pattern)
13. Branding (same pattern)
14. UI Apps (Wallow.Auth, Wallow.Web)
15. Test Infrastructure (Tests.Common, Architecture.Tests)
16. Infrastructure Config (Docker, Deploy, CI/CD)
17. Synthesis — compile all notes into module summaries

## Per-Session Workflow

1. Open the task bead, mark in-progress
2. List all files in the layer
3. Walk through each file — what it does, why it exists
4. Review corresponding tests inline
5. Log issues found as new beads (dead code, missing tests, inconsistencies)
6. Capture observations in the bead's `--notes` field
7. Close the task bead

## Bead Structure

- One **epic** for the entire walkthrough
- One **feature** per major area (each module, shared infra, UI apps, test infra, deploy/CI)
- One **task** per layer within each feature
- One **task** at the end for synthesis

## Issue Tracking

Issues discovered during the walkthrough get their own bug/task beads, separate from the walkthrough tasks.

## Scope

- ~1,362 source .cs files
- ~699 test .cs files
- 9 modules, 8 shared projects, 3 top-level apps
- Docker, deploy, and CI/CD configuration
