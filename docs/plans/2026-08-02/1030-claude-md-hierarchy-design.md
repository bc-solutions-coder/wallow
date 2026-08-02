# CLAUDE.md hierarchy + memory store redesign

**status: active**

## Problem

~155 KB (≈39k tokens) is injected into every session and every subagent before any work
starts. Measured:

| Source | Size | Share |
| --- | --- | --- |
| `bd prime` memories (178) | 107.7 KB | 69% |
| Root `CLAUDE.md` (350 ln) | 25.4 KB | 16% |
| `.claude/rules/*.md` ×4 (264 ln) | 22.4 KB | 15% |

The 20 nested `CLAUDE.md` files are **not** part of this — they load lazily when an agent
touches their subtree. The hierarchy already works; the always-on tier is what is oversized.

Measured accuracy, which inverts the obvious assumption:

- **CLAUDE.md + rules: 0 stale paths out of ~150 references.** These files are correct.
  Their problem is density and placement, not truth.
- **Memories: 15 stale paths out of 78 (19%)**, and they contradict CLAUDE.md — e.g. a memory
  points at `apps/wallow-auth/src/lib/api-passthrough.ts` while `apps/CLAUDE.md` gives
  `src/shared/lib/api-passthrough.server.ts`; another puts invalidation helpers in
  `apps/wallow-web/src/shared/testing/` while `TESTING.md` states helpers live in
  `@bc-solutions-coder/testing` and never in an app.

Root `CLAUDE.md` also carries package-specific depth — the oxlint rule-by-rule rationale
(`no-cycle`, `prefer-default-export`, `no-nodejs-modules`) — that `packages/lint/CLAUDE.md`
does not itself carry. That detail is paid for on every session but only matters when editing
`.oxlintrc.json`.

## Store contract

Each store gets exactly one job. This is the rule that decides where a new fact goes.

| Store | Loads | Holds |
| --- | --- | --- |
| Root `CLAUDE.md` | always | What the repo is, commands, the routing table, rules true of *every* file |
| `.claude/rules/` | always (native auto-load, no import needed) | Only cross-cutting rules with no natural directory home |
| Nested `CLAUDE.md` | when an agent touches that directory | Everything specific to that subtree |
| `docs/` | on explicit read | Rationale, history, the "why" |
| beads memories | always, via hook | Timeless repo-wide facts stated in none of the above |

## Writing rules for a CLAUDE.md entry

1. Directive first, imperative, one line.
2. Rationale only when omitting it would let an agent revert the rule — then one clause, or a
   `docs/` link.
3. No history verbs (`was`, `used to`, `no longer`, `replaces`). Mirrors the repo's existing
   test-comment rule.
4. No line-number citations into other files.
5. If a lint rule enforces it, write ``lint-enforced: `wallow/x` `` and stop.

Targets: root ≤120 lines, each nested ≤80, rules core ≤40.

## Memory policy

A memory survives only if **all four** hold: timeless; repo-wide; stated in no CLAUDE.md; not
rediscoverable by grep in under 30 seconds. Bead-scoped findings belong on the bead
(`bd note`), never in memory. Expect 178 → ~50.

## Phases

Each phase is independently verifiable and can be stopped after.

**Phase 1 — memory triage.** 178 memories, batched ~20 per agent (9 agents). Each agent
receives an explicit key list and returns, per key, `KEEP` / `DELETE` (with the file that
already states it) / `FIX` (with the corrected path), plus evidence. No writes.

**Phase 2 — CLAUDE.md condensation.** One agent per file. Each reads exactly one file,
verifies its claims against real code, and returns a condensed rewrite plus two lists: content
that belongs in a different CLAUDE.md, and rationale that belongs in `docs/`.

**Phase 3 — rules relocation.** `E2E.md` → `apps/*/e2e/CLAUDE.md`; `CONVENTIONS.md` →
`api/CLAUDE.md`; `TESTING.md` splits, with roughly 10 universal lines staying always-on and the
browser-mode gotchas moving to `packages/testing/CLAUDE.md`.

**Phase 4 — verification.** Independent agents confirm every path resolves, every command
exists, and no rule was lost between the old and new text.

## Constraints on the fleet

Agents are narrow by construction: one file or one batch of ~20 memory keys each, an explicit
input list, and a fixed output shape. No agent is asked to form a cross-file theory — that
synthesis stays with the coordinator.
