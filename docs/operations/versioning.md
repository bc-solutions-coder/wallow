# Versioning Guide

Wallow uses automated semantic versioning driven by [Conventional Commits](https://www.conventionalcommits.org/) and [release-please](https://github.com/googleapis/release-please). Versions flow through assemblies, Docker images, and git tags with zero manual intervention.

## Commit Message Format

All commits must follow the Conventional Commits specification:

```
<type>[optional scope][!]: <description>

[optional body]

[optional footer(s)]
```

### Types and Version Impact

This table is the canonical list of accepted types. `CONTRIBUTING.md` and the root `CLAUDE.md`
point here rather than restating it.

| Type | Version Bump | Example |
|------|-------------|---------|
| `fix` | Patch (0.0.X) | `fix: resolve null reference in tenant resolver` |
| `feat` | Minor (0.X.0) | `feat: add file upload to Storage module` |
| `feat!` | **Major (X.0.0)** | `feat!: redesign authentication flow` |
| `chore` | *(no release)* | `chore: update NuGet packages` |
| `refactor` | *(no release)* | `refactor: extract base entity class` |
| `docs` | *(no release)* | `docs: add caching guide` |
| `test` | *(no release)* | `test: add identity integration tests` |
| `ci` | *(no release)* | `ci: add Docker build step` |
| `style` | *(no release)* | `style: reformat with oxfmt` |
| `perf` | *(no release)* | `perf: cache the permission lookup` |
| `build` | *(no release)* | `build: bump the pnpm version` |

> **Note:** release-please only creates releases for `fix:` (patch) and `feat:` (minor) commits. Other types appear in the changelog but don't trigger a version bump on their own.

A `BREAKING CHANGE` footer in any commit body also triggers a major bump:

```
refactor: change tenant ID from int to Guid

BREAKING CHANGE: TenantId is now a strongly-typed ID wrapping a Guid.
```

### Scope Examples

Scope is optional but useful for changelogs:

```
feat(identity): add TOTP authentication
fix(identity): correct token refresh logic
chore(deps): bump Wolverine to 3.x
```

## Version Flow

```
feature branch ──PR──► main branch ──release PR──► tag + GitHub Release
                        (accumulates)               (publishes)
```

1. **Feature branches** — Develop and PR into main. CI runs tests.
2. **Merge to main** — release-please analyzes commits and creates/updates a **Release PR** with changelog and version bump.
3. **Merge the Release PR** — release-please creates a git tag (`v4.1.0`) and GitHub Release.
4. **Tag triggers publish** — The publish workflow (`publish.yml`) promotes `:<short-sha>` images to `:latest` and semver tags, then scans with Trivy.

### Docker Image Tag Tiers

| Tag | Source | Stability |
|-----|--------|-----------|
| `:nightly` | Every merge to `main` | Bleeding edge — may be broken |
| `:latest` | Release publish | Current stable release |
| `:X.Y.Z` / `:X.Y` | Release publish | Pinned version |
| `:<short-sha>` | `main` branch push | Specific commit (internal) |

The commit tag is the **first seven characters** of the SHA, not the full 40 — `deploy.yml`
computes `SHORT_SHA="${SHA_TAG:0:7}"` and pushes `:nightly` alongside it. Both workflows' own
header comments say "`:sha`" as shorthand for the same thing.

### Example Sequence

Starting from the current root version, `4.0.0`:

```
1. feat: add payments               → merge to main → Release PR updated (4.0.0 → 4.1.0)
2. fix: tenant resolver edge case   → merge to main → Release PR updated (4.0.0 → 4.1.0)
3. merge Release PR                 →               → v4.1.0 tag + GitHub Release + Docker image
4. fix: tenant resolver             → merge to main → new Release PR (4.1.0 → 4.1.1)
```

## How to Trigger Version Bumps

**Patch** — Use `fix:` prefix.

**Minor** — Use `feat:` prefix.

**Major** — Use `feat!:` or `fix!:`, or include `BREAKING CHANGE` in the commit body.

> **Note:** The two components are at different stages. `.release-please-manifest.json` tracks the
> .NET backend at `4.0.0` — past 1.0, so a `feat!:` commit is a real major bump with the
> compatibility meaning semver gives it. `packages/sdk` is still pre-1.0 at `0.2.0`; moving it to
> `1.0.0` is an intentional decision, made by merging a `feat(sdk)!:` commit when the SDK's surface
> is ready to be held stable.

## release-please Configuration

Configuration lives in two files at the repository root:

- **`release-please-config.json`** — Per-component release type and extra files to version-bump
- **`.release-please-manifest.json`** — Tracks the current version of each component

### Monorepo (manifest) mode

release-please runs in **manifest mode**, versioning multiple components independently:

| Component | Path | Release type | Tag scheme | Published to |
|-----------|------|-------------|-----------|--------------|
| .NET backend | `.` | `simple` | `vX.Y.Z` | Docker images |
| SDK | `packages/sdk` | `node` | `sdk-vX.Y.Z` | GitHub Packages |

Each component gets its own changelog and its own Release PR: a `feat(sdk):` commit bumps only `packages/sdk`, while a commit scoped to the .NET backend bumps only the `.` component. The `.` component keeps its original `vX.Y.Z` tag scheme and behavior unchanged (an empty root component prepends nothing to the tag).

#### The SDK releases in two stages

Versioning the SDK and publishing it are separate steps, owned by different workflows. Neither
happens as a side effect of the other:

1. **release-please versions it.** The `packages/sdk` component is declared with `component: sdk`
   and `include-component-in-tag: true`, so merging its Release PR bumps
   `packages/sdk/package.json`, updates the SDK changelog, and creates an `sdk-vX.Y.Z` tag.
2. **`sdk-publish.yml` publishes it.** That workflow triggers **only** on a pushed `sdk-v*` tag or
   a manual dispatch with an explicit version — nothing else pushes the package to GitHub
   Packages.

So the tag created in step 1 is what starts step 2. See
[TypeScript SDK](../integrations/typescript-sdk.md) for what the published package contains.

Applications under `apps/*` are **private** and carry no semver — they are deliberately absent from the config and never receive version-bump PRs. They deploy by git SHA / CalVer instead.

release-please automatically updates `api/Directory.Build.props` with the new .NET version via the `extra-files` config.

### Trade-off: `workspace:*` bumps do not cascade

Manifest mode does **not** auto-cascade a `workspace:*` dependency bump. When a future published package (for example `packages/ui-*`) depends on `packages/sdk` via `workspace:*`, bumping the SDK will **not** automatically bump or release the dependent package.

To release the dependent after an SDK bump lands, bump its scope manually — for example commit `fix(ui-foo): bump sdk dependency` so release-please opens a Release PR for `ui-foo` as well. Apps under `apps/*` are unaffected because they carry no semver and are never released.

## What Gets Stamped

| Artifact | How | Example |
|----------|-----|---------|
| `api/Directory.Build.props` | Updated by release-please in the Release PR | `<Version>4.0.0</Version>` |
| Docker image tags | Deploy pushes `:nightly` and `:<short-sha>`; publish promotes to `:latest` and semver | `4.0.0`, `4.0`, `latest`, `nightly` |
| Git tags | Created by release-please on Release PR merge | `v4.0.0` |
| GitHub Releases | Created by release-please with auto-generated changelog | `v4.0.0` |

These examples track the **root** component, which is what `api/Directory.Build.props` and the
Docker tags carry. `packages/sdk` versions independently — it is at `0.2.0` and its tags are
`sdk-v0.2.0`.

## Local Development

Local builds use the version from `api/Directory.Build.props`. The publish workflow overrides this with the tag version via `/p:Version` build arg.

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Release PR not appearing | Ensure commits use conventional format (`feat:`, `fix:`). `chore:` alone won't trigger a release. |
| Want to force a specific version | Edit `.release-please-manifest.json` to the desired version and merge to main. |
| Release PR has wrong version | Check the manifest file matches the last released version. |
| Docker image not built | Verify the publish workflow triggers on `v*` tags and the Release PR was merged (not just closed). |
