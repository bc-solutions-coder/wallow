# Compose ↔ `.env.example` drift check

**status: completed**

## Problem

`docker/*.yml` reference environment variables through `${VAR}` interpolation. `docker/.env.example`
and `docker/.env.production.example` are the only place a fork learns those knobs exist. Nothing
keeps the two in step, and they have already drifted: `docker-compose.yml` reads five `GARAGE_*`
variables that appear in no example file, so a fork tuning object storage has to read the compose
file to discover them.

The user's framing was "a library for manifest and env syncs". Manifests are already covered —
`sherif` (`pnpm lint:manifests`), `knip` (`lint:deps`), `publint` + `attw` (`check:exports`) and
pnpm catalogs together do what syncpack would. Only the env half is unguarded.

## What the check asserts

**Documentation completeness, not requiredness.** Every `${VAR}` a compose file references must
appear in its `.env.example` — commented or not.

That inversion is the whole design. It needs only the variable *name* — everything up to `:`, `-`,
`?`, or `}` — and never has to decide whether a variable is required. That matters because
requiredness is genuinely ambiguous here and getting it wrong produces a check that is worse than
nothing:

- A first pass that treated every `${VAR}` as required reported 11 drifted variables. Eight of
  those carry `:-` defaults (`${GARAGE_S3_PORT:-3900}`), so they are optional by construction.
- Of the three remaining bare `${VAR}` references — `OIDC_CLIENT_SECRET`, `BCORDES_CLIENT_SECRET`,
  `BCORDES_BFF_SECRET`, `BCORDES_BFF_AUTHCODE_SECRET` — the bare form is **deliberate fail-closed
  design**, called out in a comment in `docker-compose.production.yml`: an unset client secret must
  abort the seeder rather than register a public client. Three of them are documented as commented
  lines under an "Additional clients (optional)" heading in `.env.production.example`, exactly as
  intended.

A requiredness check would flag those three forever and still miss the five real gaps, because
`GARAGE_*` all have defaults and so never make Compose warn. Documentation completeness gets both
right.

Interpolation grammar (`${V}`, `${V:-d}`, `${V-d}`, `${V:?e}`, `${V?e}`) collapses to one regex
under this rule, and Docker itself is never invoked — no daemon, no socket, nothing to be flaky in
CI.

## Shape

`scripts/check-env.sh`, exposed as `pnpm lint:env` and added to the `pnpm check` chain beside
`lint:manifests` and `lint:deps`. A root script outside turbo, mirroring `scripts/check-exports.sh`.

```
docker-compose.yml            -> docker/.env.example
docker-compose.production.yml -> docker/.env.production.example
docker-compose.test.yml       -> docker/.env.example
```

Extract `${NAME` occurrences, take `NAME`, and require a `^#? *NAME=` line in the paired example
file. Anchoring on the trailing `=` is what keeps `GARAGE_S3_PORT` from being satisfied by a
hypothetical `GARAGE_S3_PORT_ALT`. Failures list the missing variables per pair; exit non-zero.

About 25 lines of bash, no new dependency.

## Current state

Against today's tree the check fails on exactly five variables, all from `docker-compose.yml`:

| Variable            | Compose default                     |
| ------------------- | ----------------------------------- |
| `GARAGE_ADMIN_PORT`  | `3903`                              |
| `GARAGE_ADMIN_TOKEN` | `wallow-admin-token`                |
| `GARAGE_REGION`      | `us-east-1`                         |
| `GARAGE_RPC_SECRET`  | `0123…cdef` (64 hex chars)          |
| `GARAGE_S3_PORT`     | `3900`                              |

Both production pairs pass clean, including all four client secrets.

## Plan

1. Write `scripts/check-env.sh`.
2. Add the five `GARAGE_*` knobs to `docker/.env.example` with their current compose defaults, so
   the check goes green. Leaves the `BCORDES_*` fail-closed design untouched.
3. Wire `lint:env` into `package.json`'s `check` chain, into `.github/workflows/js.yml`, and into
   the command table in `CLAUDE.md`.

## Deliberately not doing

- **Runtime validation** of a live container's environment. The failure it would catch is already
  caught by the service failing to start.
- **A canonical-source generator** emitting example files from compose. It would have to invent
  prose for every comment currently in the example files, and those comments are the documentation.
- **Scanning `process.env` in TypeScript or `ENV` in Dockerfiles.** A wider Node-based checker was
  considered and scoped out. It is not covered elsewhere: TS reads `process.env` directly in 20
  distinct variables across `apps/` and `packages/`, and `@bc-solutions-coder/env` does not type
  them — that package resolves base paths and origins, which is a different job. So this is a real
  gap, deliberately left open. Compose is the higher-value half (it is where the drift already
  exists) and it is the half a 25-line script can check without a parser. Worth revisiting as its
  own bead if the TS reads drift too.
