**status: completed**

# Research: publishing `@bc-solutions-coder/sdk` to GitHub Packages for external consumers

_Resolves wayfinder research ticket #117 (map #112). Extends
`1254-external-idp-research.md` § 3 ("The RP side") and its open item 5 ("SDK distribution:
`workspace:*`-only today; external clients can't `npm install` it") — that item is **stale**, see
§ 0. Every claim cites the repo file, registry response, or first-party doc that owns it._

---

## 0. Headline: the SDK is already published, publicly, by an existing pipeline

The ticket asks "what publish step/workflow is missing". Nothing is missing on the producer
side — the pipeline exists, has run green twice, and the package is public:

| Fact | Evidence |
| --- | --- |
| A dedicated publish workflow exists, triggered by `sdk-v*` tags or manual dispatch, publishing with `pnpm publish` and `NODE_AUTH_TOKEN: ${{ secrets.GITHUB_TOKEN }}` (`permissions: packages: write`). | `.github/workflows/sdk-publish.yml` |
| release-please versions the SDK as its own component (`component: sdk`, `include-component-in-tag: true`, `release-type: node`) and the manifest records `packages/sdk: 0.2.0`. | `release-please-config.json`, `.release-please-manifest.json` |
| Two releases have shipped through that path: `sdk-v0.1.0` (2026-07-05) and `sdk-v0.2.0` (2026-07-26), both `SDK Publish` runs `completed / success` (run ids 28744860166, 30225531126). The 0.2.0 run was triggered by the release-please "chore: release main" merge, proving the tag → publish hand-off works. | `gh run list --workflow sdk-publish.yml`; `gh release list` |
| The package landing page `https://github.com/bc-solutions-coder/wallow/pkgs/npm/sdk` answers HTTP 200 **anonymously** and carries the `Public` label. | `curl` from this session (no credentials) |
| The two-stage release design (release-please versions, `sdk-publish.yml` publishes) is already documented. | `docs/operations/versioning.md` § "The SDK releases in two stages"; `docs/integrations/typescript-sdk.md` § "Publishing the SDK" |
| Consumer-side install instructions (scope line in project `.npmrc`, token in user-level config, `read:packages`) are already documented. | `docs/integrations/typescript-sdk.md` § "Installation"; `docs/integrations/integration-cookbook.md` § 1; `packages/sdk/README.md` |

So the real deliverable is (a) a short list of **hardening gaps** in what is published, and (b)
the **consumer-side** recipe for `bc-solutions-coder/bcordes`, which is where the actual
unknowns were.

---

## 1. What the published artifact actually looks like (verified by packing it)

`pnpm --filter @bc-solutions-coder/sdk build && pnpm --dir packages/sdk pack` in this worktree
produced `bc-solutions-coder-sdk-0.2.0.tgz` (272 KB, 68 files: `dist/**` JS + `.d.ts` + source
maps, plus `package.json`, `README.md`, and the workspace `LICENSE`, which pnpm packs into a
workspace member automatically — [pnpm publish docs](https://pnpm.io/cli/publish)).

The packed manifest (what a consumer receives):

- `exports` is the **`publishConfig.exports` dist map** (`./dist/index.js`, `./dist/server/index.js`,
  `./dist/server/passthrough.js`, `./dist/query/index.js`, each with a `types` condition).
  pnpm applies `publishConfig` overrides for `exports`/`main`/`types` at pack time
  ([pnpm package.json § publishConfig](https://pnpm.io/package_json#publishconfig)); `npm publish`
  would not — the comment in `sdk-publish.yml` and `scripts/check-exports.sh` both record this.
- `dependencies` are exactly `cookie-es ^3.1.1`, `iron-webcrypto ^1.2.1`, `openid-client ^6.1.7`
  — **no `workspace:*` runtime deps at all**, so the ticket's `workspace:*` concern does not
  arise for `dependencies`.
- `peerDependencies.@tanstack/react-query` came out as `^5.101.2` — the `catalog:react` protocol
  was replaced by the resolved range, as documented ([pnpm catalogs § Publishing](https://pnpm.io/catalogs#publishing):
  "The `catalog:` protocol is removed when running `pnpm publish` or `pnpm pack`"). It stays
  optional via `peerDependenciesMeta`.
- `devDependencies.@bc-solutions-coder/config` came out as `"0.0.0"` — pnpm rewrote
  `workspace:*` to the sibling's version, and `packages/config` is `0.0.0`/private. Harmless
  (consumers never install a dependency's devDependencies) but it is a published reference to a
  package that does not exist on any registry. See § 3.
- `devDependencies.typescript` came out as `6.0.3` (the `catalog:tooling-tsc6` pin).
- **Missing fields:** no `repository`, no `license`, no `engines`. See § 3.

Type resolution outside the workspace: `@arethetypeswrong/cli` against that tarball
(`--profile esm-only --ignore-rules internal-resolution-error`, the exact invocation
`scripts/check-exports.sh` runs in `pnpm check`) reports every entrypoint green for
`node16 (from ESM)` and `bundler`. The emitted `.d.ts` files import only
`iron-webcrypto`, `openid-client`, `@tanstack/react-query` (query entry only), and a self-reference
to `@bc-solutions-coder/sdk/server` (resolves through the package's own `exports`). The `redis`
mentions in `dist/server/store/redis-adapter.d.ts` are JSDoc prose; `NodeRedisClient` is declared
structurally so there is no `redis` import (`packages/sdk/src/server/store/redis-adapter.ts`).
`@tanstack/react-router` is likewise not imported — `packages/sdk/src/route-context.ts` states the
SDK "gains no dependency on `@tanstack/react-router`".

Node-only imports in the runtime bundle are `node:crypto` (server entry). Browser entries are
`node:`-free. The `@types/node ^24` devDependency does not leak into consumers.

---

## 2. Registry semantics that constrain the consumer (first-party GitHub docs)

- **Authentication is required to install even a public npm package from GitHub Packages.**
  "In most registries, to pull a package, you must authenticate with a personal access token or
  `GITHUB_TOKEN`, regardless of whether the package is public or private. However, in the
  Container registry, public packages allow anonymous access" —
  [About permissions for GitHub Packages](https://docs.github.com/en/packages/learn-github-packages/about-permissions-for-github-packages#visibility-and-access-permissions-for-packages).
  Confirmed empirically: `pnpm view @bc-solutions-coder/sdk` from this machine (scope line
  present, no token) returns `ERR_PNPM_FETCH_401`.
- **Only classic PATs work.** "GitHub Packages only supports authentication using a personal
  access token (classic)" — [Working with the npm registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-npm-registry#authenticating-to-github-packages).
  Fine-grained PATs are not an option for `read:packages`.
- **A public package is readable by any repository's `GITHUB_TOKEN`.** "If the package is public,
  any workflow running in any repository can download the package" —
  [Publishing and installing a package with GitHub Actions](https://docs.github.com/en/packages/managing-github-packages-using-github-actions-workflows/publishing-and-installing-a-package-with-github-actions#default-permissions-and-access-settings-for-packages-modified-through-workflows).
  So `bcordes`'s CI needs **no PAT**: `NODE_AUTH_TOKEN: ${{ secrets.GITHUB_TOKEN }}` with
  `permissions: packages: read` is enough.
- **Why the package is public without anyone toggling it:** a package first created by a
  workflow using `GITHUB_TOKEN` "inherits the visibility and permissions model of the repository
  where the workflow is run" (same page). `bc-solutions-coder/wallow` is `PUBLIC`
  (`gh repo view --json visibility`). Note this differs from a direct `npm publish` from a
  laptop, where "the default visibility is private".
- **The `repository` field links package ↔ repo.** "You can connect a package to a repository as
  soon as the package is published by including a `repository` field in the `package.json`"
  ([npm registry docs](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-npm-registry#publishing-a-package)).
  The current manifest lacks it; the link exists today only because the workflow ran in the repo.
- **npm provenance is not a GitHub Packages feature.** Provenance attestations are generated by
  the **npm registry** on publish and logged to Sigstore; the docs describe it exclusively for
  `registry.npmjs.org` ([npm: Generating provenance statements](https://docs.npmjs.com/generating-provenance-statements)).
  `pnpm publish --provenance` exists ([pnpm publish](https://pnpm.io/cli/publish#--provenance))
  but has no consumer on `npm.pkg.github.com`. **Do not add `id-token: write` or `--provenance`
  to `sdk-publish.yml`** unless the package moves to npmjs.

---

## 3. Producer-side checklist (Wallow repo) — hardening, not enabling

Everything below is optional polish; publishing already works. Ordered by value.

1. **Add `repository`, `license`, and `engines` to `packages/sdk/package.json`.**
   - `"repository": { "type": "git", "url": "https://github.com/bc-solutions-coder/wallow.git", "directory": "packages/sdk" }`
     — makes the package ↔ repo link explicit instead of incidental, and is what GitHub's docs
     say to set for publishing.
   - `"license"` matching the root `LICENSE` (the file is already packed; the SPDX field is not).
   - `"engines": { "node": ">=24" }` — declares the only runtime the workspace tests on
     (`.nvmrc` = 24, both app Dockerfiles `FROM node:24-slim`, `sdk-publish.yml`
     `node-version: "24"`). With pnpm this is advisory unless the consumer sets `engineStrict`
     ([pnpm package.json § engines](https://pnpm.io/package_json#engines)). Do **not** add
     `engines.pnpm`: bcordes runs pnpm 10.28.2 and this is a library, not a workspace.
   - Run `pnpm check:exports` after — `publint --strict` is part of it.
2. **The `@bc-solutions-coder/config: "0.0.0"` devDependency in the published manifest.**
   Harmless (consumers never install a dependency's devDependencies) and there is no pnpm knob
   to strip devDependencies on publish; the pragmatic fix is a one-line note in
   `packages/sdk/CLAUDE.md` that this rewrite is expected. Low priority.
3. **Keep `sdk-publish.yml` as is.** Reviewed line-by-line against the setup-node docs
   ([advanced usage § Publish to GPR](https://github.com/actions/setup-node/blob/main/docs/advanced-usage.md#publish-to-npmjs-and-gpr-with-npm)):
   `registry-url` + `scope` + `NODE_AUTH_TOKEN` is the documented shape; `pnpm publish` is
   correctly chosen over `npm publish` (§ 1); the `pnpm version` sync step is belt-and-braces
   because release-please already bumped the manifest, but it is what makes manual dispatch
   work. One consideration: the docs recommend `package-manager-cache: false` in publish jobs
   ("a poisoned cache may expose credentials"); the workflow uses `cache: "pnpm"`. The exposure
   is `GITHUB_TOKEN` scoped to `packages: write` on this repo — accept or drop the cache; not
   blocking.
4. **`publishConfig.access: "restricted"`** is irrelevant on GitHub Packages (visibility is a
   package setting, inherited from the repo — § 2). Leave it; it only matters if the package ever
   moves to npmjs, where it would then need `public`.
5. **Do not add provenance** (§ 2).
6. **Docs drift to fix while touching this:** `docs/integrations/typescript-sdk.md` § Installation
   says the token "needs `read:packages` on that organization" — the scope owner is a **user**
   (`gh api /users/bc-solutions-coder` → `type: User`), and the package is public, so any
   classic PAT with `read:packages` works regardless of org membership. Also worth stating
   explicitly that a public GitHub Packages npm package still needs a token (readers assume
   "public" means anonymous, as it does for ghcr.io).

---

## 4. Consumer-side recipe for `bc-solutions-coder/bcordes`

Facts about the consumer (read via `gh api repos/bc-solutions-coder/bcordes/contents/…`):
public repo; single-package (no workspace); `packageManager: pnpm@10.28.2`, installed in the
Dockerfile with `npm install -g pnpm@10.28.2` on `node:24-alpine`; TanStack Start `~1.167.5`,
`@tanstack/react-router 1.168.3`, `@tanstack/react-query ^5.95.2`; **no `.npmrc` today**; image
built by `docker/build-push-action@v5` in `.github/workflows/docker-publish.yml` and deployed by
Portainer pulling from GHCR (`DEPLOYMENT.md`), so the runtime host never runs `pnpm install`.

### 4.1 Local developer

```ini
# .npmrc  (committed)
@bc-solutions-coder:registry=https://npm.pkg.github.com
```

```bash
# once per machine; classic PAT with read:packages only — fine-grained PATs are not accepted
pnpm config set "//npm.pkg.github.com/:_authToken" "$GITHUB_TOKEN"
pnpm add @bc-solutions-coder/sdk
```

The token must **not** go in the committed `.npmrc` — pnpm refuses to expand env vars in
registry credentials from a project file (recorded in Wallow's root `.npmrc` comment and
`docs/integrations/typescript-sdk.md`). Is a read token acceptable for a public-repo consumer?
Yes, and unavoidable (§ 2): the registry requires auth for public packages. The token is
read-only and per-developer; no secret is committed.

### 4.2 GitHub Actions (CI + image build)

```yaml
permissions:
  contents: read
  packages: write   # already needed for the GHCR push; read is sufficient for the install

steps:
  - uses: actions/setup-node@v7
    with:
      node-version: "24"
      registry-url: "https://npm.pkg.github.com"
      scope: "@bc-solutions-coder"
  - run: pnpm install --frozen-lockfile
    env:
      NODE_AUTH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

`setup-node` writes the credential to a user-level `.npmrc` and points `NPM_CONFIG_USERCONFIG`
at it, so pnpm expands `NODE_AUTH_TOKEN` there (setup-node advanced usage, linked in § 3). No
PAT secret is required because the package is public (§ 2).

### 4.3 Dockerfile — BuildKit secret, never `ARG`/`ENV`

bcordes' current `base` stage does `COPY package.json pnpm-lock.yaml ./` then
`pnpm install --frozen-lockfile`. Two changes:

```dockerfile
# base stage
COPY package.json pnpm-lock.yaml .npmrc ./          # .npmrc carries only the scope line
RUN --mount=type=cache,id=pnpm,target=/root/.local/share/pnpm/store \
    --mount=type=secret,id=NODE_AUTH_TOKEN,env=NODE_AUTH_TOKEN \
    pnpm config set "//npm.pkg.github.com/:_authToken" "$NODE_AUTH_TOKEN" --location=global \
 && pnpm install --frozen-lockfile \
 && pnpm config delete "//npm.pkg.github.com/:_authToken" --location=global
```

Secret mounts exist for exactly this: "Build arguments and environment variables are
inappropriate for passing secrets to your build, because they persist in the final image" and
`--mount=type=secret,id=…,env=VAR` exposes it as an env var for that `RUN` only
([Docker: Build secrets](https://docs.docker.com/build/building/secrets/)). The `config delete`
keeps the token out of the layer's `~/.npmrc` (the `base` stage is only used for
`COPY --from=base /app/node_modules`, so the runtime image never contains it either way).

Pass it in:

```yaml
# docker-publish.yml
- uses: docker/build-push-action@v5
  with:
    context: .
    file: ./Dockerfile
    secrets: |
      NODE_AUTH_TOKEN=${{ secrets.GITHUB_TOKEN }}
```

```bash
# local build
NODE_AUTH_TOKEN=$(pnpm config get "//npm.pkg.github.com/:_authToken") \
  docker build --secret id=NODE_AUTH_TOKEN .
```

Add `.npmrc` to nothing in `.dockerignore` (it must be copied) — check the existing
`.dockerignore` does not exclude dotfiles wholesale.

### 4.4 Version and peer pins to check on the consumer

| Requirement | Source | bcordes today | Action |
| --- | --- | --- | --- |
| Node 24 | Wallow's tested runtime (`.nvmrc`, Dockerfiles); the SDK declares nothing until § 3.1 lands | `node:24-alpine` | none |
| `@tanstack/react-query ^5.101.2` (optional peer, only for the `./query` entry) | packed manifest (§ 1) | `^5.95.2` in `package.json` — the lockfile may pin below 5.101.2 | `pnpm update @tanstack/react-query` if `pnpm install` warns about an unmet peer; or skip the `./query` entry entirely |
| TanStack Start version | **no pin required** — the SDK imports neither `@tanstack/react-start` nor `@tanstack/react-router` (§ 1); server handlers are `(Request) => Promise<Response>` | `~1.167.5` | none |
| ESM only | `"type": "module"`, no CJS build (attw `node16 (from CJS)` = dynamic import only) | Vite/Nitro app, ESM | none |
| `redis` | not a dependency; `createRedisAdapter` takes a structurally-typed client | n/a | install `redis` only if using `ValkeySessionStore` |

---

## 5. Is `/server` the right surface for an external RP?

Yes, and it is already the whole surface `bff-example`/`minimal-app` use. The four entries and
their roles are documented in `docs/integrations/typescript-sdk.md` § Overview and
`packages/sdk/CLAUDE.md`. For an own-domain RP the minimum is:

- `@bc-solutions-coder/sdk/server` → `createWallowBffServer()` / `loadBffConfigFromEnv()` plus
  a session store (`CookieSessionStore` needs nothing extra; `ValkeySessionStore` needs a
  `RedisLike`).
- `@bc-solutions-coder/sdk` (browser) → `createWallowSdk()`, `loginRedirect()`, `logout()`,
  `getCurrentUser()`, the generated operations.
- `@bc-solutions-coder/sdk/query` only if the app wants the generated TanStack Query layer
  (this is what pulls in the `@tanstack/react-query` peer).

What the surface does **not** yet cover for an external RP is protocol, not packaging — the
back-channel logout handler and the M2M client-credentials source, both already listed as open
items in `1254-external-idp-research.md` § 6 and scoped to #127 (which this ticket blocks).

---

## 6. Recommendation

- **Producer:** treat #117 as "verified working; polish only". Land § 3.1 (`repository`,
  `license`, `engines`) and the § 3.6 doc fix as one small `build(sdk):` + `docs:` change. No
  workflow changes; no provenance.
- **Consumer (bcordes):** § 4.1–4.3 is the complete recipe — a one-line `.npmrc`, `GITHUB_TOKEN`
  in Actions, and a BuildKit secret mount in the Dockerfile. It needs no PAT in CI because the
  package is public, and a classic `read:packages` PAT per developer locally, which is the
  ceiling GitHub Packages allows.
- **#127** can proceed on the published package shape today: `pnpm add @bc-solutions-coder/sdk@0.2.0`.

---

## Sources

Repo: `.npmrc`; `package.json`; `pnpm-workspace.yaml`; `packages/sdk/package.json`;
`packages/sdk/vite.config.ts`; `packages/sdk/tsconfig.build.json`; `tsconfig.build.base.json`;
`packages/sdk/src/server/index.ts`; `packages/sdk/src/server/store/redis-adapter.ts`;
`packages/sdk/src/route-context.ts`; `packages/sdk/CLAUDE.md`; `packages/sdk/README.md`;
`.github/workflows/sdk-publish.yml`; `.github/workflows/release-please.yml`;
`release-please-config.json`; `.release-please-manifest.json`; `scripts/check-exports.sh`;
`apps/wallow-auth/Dockerfile`; `docs/operations/versioning.md`;
`docs/integrations/typescript-sdk.md`; `docs/integrations/integration-cookbook.md`;
`docs/plans/2026-08-29/1254-external-idp-research.md`.

Live: `gh run list --workflow sdk-publish.yml`; `gh release list`; `gh repo view --json visibility`;
`gh api /users/bc-solutions-coder`; anonymous `curl` of the package page; `pnpm view` 401;
`gh api repos/bc-solutions-coder/bcordes/contents/{package.json,Dockerfile,.github/workflows,DEPLOYMENT.md}`;
local `pnpm pack` + `attw` of the resulting tarball.

First-party docs: GitHub Packages — [npm registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-npm-registry),
[permissions](https://docs.github.com/en/packages/learn-github-packages/about-permissions-for-github-packages),
[access control and visibility](https://docs.github.com/en/packages/learn-github-packages/configuring-a-packages-access-control-and-visibility),
[Actions publishing/installing](https://docs.github.com/en/packages/managing-github-packages-using-github-actions-workflows/publishing-and-installing-a-package-with-github-actions);
pnpm — [catalogs](https://pnpm.io/catalogs), [package.json](https://pnpm.io/package_json),
[publish](https://pnpm.io/cli/publish); npm — [provenance](https://docs.npmjs.com/generating-provenance-statements);
[actions/setup-node advanced usage](https://github.com/actions/setup-node/blob/main/docs/advanced-usage.md);
[Docker build secrets](https://docs.docker.com/build/building/secrets/).
