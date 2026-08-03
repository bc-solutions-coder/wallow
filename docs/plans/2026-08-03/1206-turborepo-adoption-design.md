# Turborepo adoption

**status: active**

Put `turbo` in front of the three fan-out tasks — `build`, `typecheck`, `test` — so unchanged
work is replayed from a content-addressed cache instead of re-executed, and so the three stop
running as global phase barriers. Leave the five root-level tools (`oxlint`, `oxfmt`, `sherif`,
`knip`, `check-exports.sh`) exactly where they are. Then point the cache at a self-hosted
`turborepo-remote-cache` server reached over a Cloudflare Tunnel, so CI and every developer
machine share one cache.

## Why

`pnpm check` (`package.json:22`) is eight steps in strict sequence, and three of them redo the
entire workspace every time. Editing one file in `apps/wallow-web` still rebuilds all 13 buildable
packages, retypechecks all 16 members, and reruns every vitest suite — including the browser-mode
suites in `ui`, `forms`, `navigation`, and both apps, which drive real headless Chromium.

Two separate costs are hiding in there:

**Redundant execution.** `pnpm -r` has no memory. Nothing distinguishes "this package's inputs are
byte-identical to the last green run" from "this package just changed". Content hashing is the
whole answer, and it is the only reason to adopt turbo — the ordering `pnpm -r` already does
correctly.

**Phase barriers.** `check` is `build ALL → typecheck ALL → test ALL`. `packages/utils` could be
typechecking and testing while `packages/ui` is still building, but it waits for the slowest member
of each phase, three times. Modelled as one DAG, wall-clock becomes the longest single chain rather
than the sum of the slowest-per-phase.

The remote cache extends the first benefit across machines: a package built on CI for a PR is a
cache hit on the laptop that pulls the branch, and vice versa.

## 1. The partition — what turbo owns

Turbo owns exactly the tasks that are already per-package scripts:

| Task        | Members | Turbo? |
| ----------- | ------- | ------ |
| `build`     | 14      | yes    |
| `typecheck` | 16      | yes    |
| `test`      | 15      | yes    |
| `dev`       | 3       | yes (`persistent`, uncached) |

Everything else in `check` is a **single root invocation over the whole tree**, not a per-package
script: `oxlint apps packages`, `oxfmt`, `sherif`, `knip`, `scripts/check-exports.sh`. Turbo's own
guidance is to prefer package tasks, and the mechanical move would be to split oxlint per package —
but that directly contradicts `packages/lint/CLAUDE.md`, where the root `.oxlintrc.json` is
deliberately the single registration point for the `wallow/*` plugin and the source/test partition
is a repo-wide two-pass design. Splitting it to please a task runner would be the tail wagging the
dog.

So those five stay as plain root scripts, unwrapped. This is not a compromise worth agonising over:
oxc is a Rust toolchain and both lint passes finish in about a second. `knip` is the only slow one,
and it is a single process that reads the entire graph — caching it per-package is not even
meaningful.

**A root task cannot depend on "every package's build."** Inside a `//#task` definition, `dependsOn:
["build"]` means the root package's own `build` script, and `^build` reaches only the root's
workspace dependencies (just `@bc-solutions-coder/lint`, which has no build). `check:exports`
genuinely requires every `dist/` to exist, so its ordering stays where it already works — a shell
sequence in the root `check` script, after the turbo invocation.

## 2. The task graph

```jsonc
"build":     { "dependsOn": ["^build"], "outputs": ["dist/**"] }
"typecheck": { "dependsOn": ["^build"] }
"test":      { "dependsOn": ["^build"] }
"dev":       { "cache": false, "persistent": true }
```

`^build` on `typecheck` and `test` is not decoration. Every workspace member resolves
`@bc-solutions-coder/*` through `package.json` `exports` maps that point at `dist/`, which is why
`js.yml:86-87` has a hand-rolled "build packages first" step with a five-line comment explaining
it. That comment becomes a config line, and the CI step goes away.

It also has a second job: it makes a dependency's source change part of the dependent's cache key.
Without it, editing `packages/utils` would leave `apps/wallow-web`'s `test` hash untouched, and the
cache would confidently replay a stale pass. That is the single most dangerous failure mode in this
whole change, and `^build` is what prevents it.

`packages/config` is a declared dependency of 15 of the 16 members, so a change to a shared Vite
preset invalidates essentially everything through the normal dependency hash. No special handling
needed — and specifically not a stub `build` script. `packages/config` has no `build` task at all,
but turbo keeps it in the task graph as a **Transit Node**: a package without the task is still
visited, still contributes its own files to the hash, and still carries its dependencies through.
Nothing executes for it; invalidation happens anyway. Task 5 verifies this rather than trusting it.

## 3. Cache-correctness hazards

A slow build is an annoyance. A false cache hit is a green CI run over broken code. Five specific
things in this repo can produce one.

**Route-tree codegen writes into tracked source.** Per `route-tree-drift.yml`, each app's
`routeTree.gen.ts` is emitted as a side effect of `vite build` — there is no standalone codegen
script, because the TanStack Start plugin owns it. The file is therefore both an input and an
output of the same task: a cold run mutates its own input so the next run misses, and a warm run
skips the build so the tree is never regenerated. Fix in each app's package configuration — declare
the file in `outputs` (so a hit restores it) and exclude it from `inputs` (so it stops
self-invalidating). This is correct on the merits, not just convenient: the tree is a pure function
of `routes/**` plus the plugin config, both of which remain inputs.

**`inputs` silently opts out of `.gitignore`.** The moment a task declares an `inputs` array, turbo
stops honouring `.gitignore` for that task and hashes whatever the globs match — including
`node_modules`, `.output`, and `test-results` if the globs are careless. Every `inputs` array in
this repo must start with `$TURBO_DEFAULT$` and only subtract from there.

**Strict environment mode is the default.** Turbo 2.x runs tasks with only the variables listed in
`globalEnv`/`env` plus a built-in passthrough set. Most of the audit is reassuring — the
`process.env` reads in `apps/` and `packages/` are runtime server reads (`OIDC_*`,
`COOKIE_PASSWORD`, `REDIS_URL`, `PORT`) or Playwright `E2E_*` reads in specs that are not turbo
tasks, and `wallow-web`'s build reads `import.meta.env.*`, which Vite synthesises rather than
inheriting. `NODE_ENV` goes in `globalEnv`.

**There is exactly one real build-time variable, and it is `AUTH_BASE_PATH`.**
`apps/wallow-auth/vite.config.ts` reads it through `process.env` at config-evaluation time and bakes
the URL prefix into every emitted asset path — `docker-compose.production.yml` passes it as a
`--build-arg` and `docker/.env.production.example` documents it as a fork knob. It fails twice if
left undeclared: strict mode filters it out, so `AUTH_BASE_PATH=/auth pnpm build` silently emits a
root-based bundle; and because it is not in the hash, a `/auth` build and a `/` build share a cache
key, so one is replayed for the other. It goes in `wallow-auth`'s `build.env`, not `globalEnv` — the
other two apps do not read it and should not miss cache when it changes.

`packages/config`'s `process.env.PORT` read is dev-server configuration only, and `dev` is uncached,
so it needs no declaration. Build-output parity gets verified explicitly rather than assumed
(Task 4).

**`CI` must stay out of `globalEnv`.** It is tempting and it is wrong: hashing `CI` gives CI and
laptops different cache keys for identical inputs, which defeats the entire point of a shared remote
cache. `CI` is in turbo's built-in passthrough set, so tasks still see it; it just doesn't
contribute to the hash.

One thing that is *not* a hazard, having checked: `packages/testing`'s `browser-styles-wiring`
helper reads files off disk, but only ones resolved relative to the consuming app's own directory,
so they fall inside that package's default input set.

## 4. CI shape

PR CI keeps running **every** task, accelerated by cache hits rather than narrowed by `--affected`.
An unchanged package restores in milliseconds, so a full run costs little more than an affected one
once the cache is warm — and the merge commit retains the guarantee that every task actually
passed. Under `--affected`, a single wrong `inputs` glob converts a real failure into a skipped
task, and the class of bug you get is "main broke and the PR was green". Full runs degrade to
slowness instead. `--affected` stays available for local use and can be revisited once the globs
have earned trust.

`js.yml` changes in four ways: `turbo.jsonc` joins the `paths:` trigger lists (the root task graph
can change every task's behaviour and must not be able to land without CI), the standalone "Build
packages" step is deleted (`^build` covers it), `.turbo` gets an `actions/cache` entry as the
phase-3 fallback, and the phase-4 remote cache supersedes that fallback.

One operational note that is not a correctness issue but will look like one: `pnpm -r` runs at
pnpm's default `workspace-concurrency` of 4, while turbo defaults to `concurrency: "10"`. Ten
browser-mode Vitest suites driving real headless Chromium on a two-core runner is a plausible source
of contention flake, so the first cold CI run gets read as a concurrency measurement, not just a
timing one, and `"concurrency": "50%"` is the dial if it misbehaves.

## 5. Self-hosted remote cache

**Server:** `ducktors/turborepo-remote-cache` — a Fastify implementation of the Remote Cache API,
the most maintained of the community options, with a published Docker image and local-filesystem
storage. Storage stays on a plain volume; the S3 provider is available later if the cache outgrows
the disk, and Wallow's existing GarageHQ would serve, but a directory is the right starting point.

**Exposure:** Cloudflare Tunnel. `cloudflared` runs beside the cache container and dials out, so no
inbound port is opened on the home network and the runner just sees an HTTPS URL. Two things about
this deserve to be stated plainly rather than discovered later:

- **Cloudflare Access service tokens cannot protect this endpoint.** Turbo sends exactly one
  credential — `Authorization: Bearer $TURBO_TOKEN` — and has no mechanism for the
  `CF-Access-Client-Id` / `CF-Access-Client-Secret` header pair that Access requires. The hostname
  is therefore publicly reachable and authentication rests entirely on `TURBO_TOKEN`. It must be a
  long random value, held as a GitHub Actions secret and in each developer's shell, and rotated by
  editing one env var on the server.
- **A Cloudflare WAF rule is the second layer.** Restrict the hostname to GitHub Actions' published
  egress ranges plus the tunnel operator's own network, and rate-limit it. This is defence in depth,
  not a substitute for the token — Actions ranges are shared by every GitHub customer.

**Integrity:** enable `remoteCache.signature` with a ≥32-byte `TURBO_REMOTE_CACHE_SIGNATURE_KEY`
shared by client and server, plus `futureFlags.longerSignatureKey` so a too-short key fails the run
immediately instead of silently weakening the HMAC. This guards against truncated or corrupted
artifacts, not against an attacker who already holds the token.

**Blast radius:** a poisoned or corrupt remote cache is recoverable — `turbo run … --force` bypasses
it, `remoteCache.enabled: false` disables it, and deleting the server's storage directory resets it.
Nothing about the cache is load-bearing for correctness of a from-scratch build.

## 6. Rejected alternatives

**Nx.** More capable, considerably more opinionated, and its value is concentrated in code
generation and project inference this repo does not want — `packages/config` and the Vite presets
already are the project inference. Turbo does one thing here and does not ask for the build to be
restructured around it.

**Bare `actions/cache` over `node_modules` and `dist/`, no task runner.** Cheaper to add, but it
caches directories rather than task results: no notion of "these inputs produced this output", so
it cannot skip a task, only reuse its scratch. It also does nothing for local development.

**Splitting oxlint into per-package tasks to widen turbo's coverage.** Covered in §1 — it would
break a deliberate design for a saving measured in seconds.

**Vercel Remote Cache.** Works out of the box, but adds a vendor account to a self-hostable stack
for a benefit the user's own server already provides.

## 7. Rollback

Delete `turbo.jsonc`, the three app package configurations, and the root devDependency; restore the
four root scripts to their `pnpm -r` forms and the deleted `js.yml` build step. Nothing else in the
repo will have changed shape, because turbo is only ever invoking scripts that already exist. The
cache server is independent infrastructure and can be left running or torn down separately.

## Implementation

`docs/plans/2026-08-03/1206-turborepo-implementation.md`
