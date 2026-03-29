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

| Type | Version Bump | Example |
|------|-------------|---------|
| `fix` | Patch (0.0.X) | `fix: resolve null reference in tenant resolver` |
| `feat` | Minor (0.X.0) | `feat: add file upload to Storage module` |
| `feat!` | **Major (X.0.0)** | `feat!: redesign authentication flow` |
| `chore` | *(no release)* | `chore: update NuGet packages` |
| `refactor` | *(no release)* | `refactor: extract base entity class` |
| `docs` | *(no release)* | `docs: add caching guide` |
| `test` | *(no release)* | `test: add billing integration tests` |
| `ci` | *(no release)* | `ci: add Docker build step` |

> **Note:** release-please only creates releases for `fix:` (patch) and `feat:` (minor) commits. Other types appear in the changelog but don't trigger a version bump on their own.

A `BREAKING CHANGE` footer in any commit body also triggers a major bump:

```
refactor: change tenant ID from int to Guid

BREAKING CHANGE: TenantId is now a strongly-typed ID wrapping a Guid.
```

### Scope Examples

Scope is optional but useful for changelogs:

```
feat(billing): add Stripe webhook handler
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
3. **Merge the Release PR** — release-please creates a git tag (`v0.2.0`) and GitHub Release.
4. **Tag triggers publish** — The publish workflow (`publish.yml`) retags the `:latest` images with semver versions and scans with Trivy. Images are already built and pushed by CI on merge to main.

### Example Sequence

```
1. feat: add payments       → merge to main → Release PR updated (0.1.0 → 0.2.0)
2. fix: billing edge case   → merge to main → Release PR updated (0.1.0 → 0.2.0)
3. merge Release PR         →               → v0.2.0 tag + GitHub Release + Docker image
4. fix: tenant resolver     → merge to main → new Release PR (0.2.0 → 0.2.1)
```

## How to Trigger Version Bumps

**Patch** — Use `fix:` prefix.

**Minor** — Use `feat:` prefix.

**Major** — Use `feat!:` or `fix!:`, or include `BREAKING CHANGE` in the commit body.

> **Note:** The project starts at `0.x.y`. Moving to `1.0.0` is an intentional decision — merge a commit with `feat!: release v1.0.0` when ready.

## release-please Configuration

Configuration lives in two files at the repository root:

- **`release-please-config.json`** — Release type, extra files to version-bump
- **`.release-please-manifest.json`** — Tracks the current version

release-please automatically updates `Directory.Build.props` with the new version via the `extra-files` config.

## What Gets Stamped

| Artifact | How | Example |
|----------|-----|---------|
| `Directory.Build.props` | Updated by release-please in the Release PR | `<Version>0.2.0</Version>` |
| Docker image tags | CI pushes `:latest` and `:sha`; publish adds semver tags | `0.2.0`, `0.2`, `latest` |
| Git tags | Created by release-please on Release PR merge | `v0.2.0` |
| GitHub Releases | Created by release-please with auto-generated changelog | `v0.2.0` |

## Local Development

Local builds use the version from `Directory.Build.props`. The publish workflow overrides this with the tag version via `/p:Version` build arg.

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Release PR not appearing | Ensure commits use conventional format (`feat:`, `fix:`). `chore:` alone won't trigger a release. |
| Want to force a specific version | Edit `.release-please-manifest.json` to the desired version and merge to main. |
| Release PR has wrong version | Check the manifest file matches the last released version. |
| Docker image not built | Verify the publish workflow triggers on `v*` tags and the Release PR was merged (not just closed). |
