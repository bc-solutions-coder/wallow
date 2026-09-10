# Configure CI and publication

PRs validate without production credentials. Successful main CI runs supply the exact packages, images and documentation used by publishing. Application rollout is covered by the [deployment guide](deployment.md).

## Configure validation

1. Enable GitHub Actions for the repository.
2. Require the GitHub Actions `CI / required` check before merging to `main`.
3. Require branches to be up to date before merging.
4. Keep pull requests and squash merges required, without bypass permissions.

PR validation uses local Turbo outputs and optional GitHub caches. PR jobs request no production environment or Tailscale credentials.
Deleting a cache causes work to run again. Cache availability does not replace validation.

Forks default to the `portable` security profile. Set repository variable `SECURITY_PROFILE=codeql` only when CodeQL is available for that repository.
The portable profile still runs its declared security checks. Review retained scanner reports when a policy gate fails.

## Configure main Turbo caching

1. Create the `production` environment.
2. Restrict its deployment branches to the exact `main` branch, without tag or wildcard rules.
3. Add the following environment secrets.

| Secret                             | Purpose                                                           |
| ---------------------------------- | ----------------------------------------------------------------- |
| `TURBO_API`                        | Complete private cache URL, including the correct scheme and port |
| `TURBO_TEAM`                       | Cache namespace                                                   |
| `TURBO_TOKEN`                      | Cache bearer credential used by CI                                |
| `TURBO_REMOTE_CACHE_SIGNATURE_KEY` | Independent client key for signed cache artifacts                 |
| `TS_OAUTH_CLIENT_ID`               | Tailscale OAuth client ID                                         |
| `TS_OAUTH_SECRET`                  | Tailscale OAuth client secret                                     |

4. Configure the OAuth client to allow the runner's `tag:ci` identity to reach the cache.
5. For a fork, set repository variable `ENABLE_MAIN_CACHE=true`.
6. Merge a validated change and inspect the main JS job's cache diagnostics.

The upstream repository selects main caching automatically. Forks must opt in.
The job reports remote hits, local hits, and misses separately. Restored GitHub outputs can produce local hits even when the remote cache is available.
If the private cache is unavailable or its configuration is incomplete, validation falls back to local execution.

Keep these secrets in `production`. Adding an environment does not make its secrets available to every job.
Only jobs that request that environment receive its secrets. The main JS job requests `production`; the PR JS job does not.
Local development can continue using local Turbo without the CI token.

## Configure publication destinations

Enable each repository variable with the literal value `true` after configuring its destination.

| Variable                    | Main-only environment and access                                     |
| --------------------------- | -------------------------------------------------------------------- |
| `ENABLE_RELEASE_AUTOMATION` | `production`, with `RELEASE_PLEASE_TOKEN`                            |
| `ENABLE_PACKAGE_PUBLISH`    | `package-publish`, with repository Actions access to GitHub Packages |
| `ENABLE_IMAGE_PUBLISH`      | `image-publish`, with repository Actions access to GHCR              |
| `ENABLE_DOCS_DEPLOY`        | `github-pages`, with Pages configured for GitHub Actions             |

Package and image writers use job-scoped GitHub tokens. Pages uses its job token and OIDC. They do not need copies of production secrets or a repository `NODE_AUTH_TOKEN`.

Publish verifies a successful main-push CI run from this repository, its exact attempt, required CI result and artifact identities. Downloads retain digest and safe-archive checks. Package lifecycle scripts do not run during publishing.

Release Please runs when the validated commit is still current main. Its token stays in `production`. Its release branch goes through normal secretless PR checks. After the release PR merges and main CI succeeds, Release Please creates releases, and publishers match the release tags to that validated commit. A release-event workflow also dispatches publication from the release commit’s successful CI run, so another merge advancing main cannot cause a release to be missed.

## Package and image tags

`.github/ci/publication.json` lists components and destinations. Packages publish only when their release tag matches the validated commit and packed version. Internal dependencies must exist before dependent packages publish. Registry checks reject conflicting immutable bytes.

Stable packages advance `latest`, `major-X` and `minor-X.Y`, in addition to retaining a content-derived staging tag. Images use immutable `sha-<full-commit>` tags; current main advances `nightly`. Platform releases also publish immutable version tags and stable `latest`, `X` and `X.Y` tags. Prereleases receive immutable versions without stable aliases.

Newer stable releases prevent older retries from moving stable tags backward. An older main build cannot advance `nightly` or deploy Pages. Existing mutable legacy tags can advance without a historical receipt migration. Conflicting immutable versions are never overwritten: investigate the conflict and publish a new version for changed content.

## Documentation deployment

Read-only preparation validates and repackages the exact CI site archive. The Pages job uses [actions/deploy-pages](https://github.com/actions/deploy-pages) to deploy that artifact, skipping it if the source is no longer current main. The workflow and Pages environment show deployment status.

## Retry publishing

Open **Actions → Publish → Run workflow**. Supply the successful main CI run ID and exact attempt. Optionally supply a release ID whose tag points to that CI commit.

A retry checks the registry, reuses identical immutable outputs and publishes missing outputs. Publishing is serialized. Separate outputs are not one transaction; after a partial failure, inspect the failed job and rerun the same validated inputs.

Logs and result artifacts provide diagnostics. Publication does not depend on custom receipts attached to Releases; existing receipt assets can remain as historical records.

Large CI publication candidates and prepared publication payloads are retained for three days. The .NET build handoff is retained for one day; rerun all CI jobs if that handoff has expired. Coverage and failure diagnostics remain available for seven days, and publication plans and scan reports for 30 days. Keeping a plan does not extend its payloads' lifetime.

Complete publication approvals and retries within three days of the original CI upload. Prepared copies expire three days after their own upload, but a fresh publication attempt still needs the original CI candidates. After expiry, the workflow cannot retry those original bytes. Validate a new main commit and publish a new release version as needed. Historical build reconstruction is outside this pipeline.

## Fork configuration

Before enabling package publication in a fork, update the package scope in the catalog, workspace manifests, Release Please configuration and lockfile. Publishable manifests must use `repository.type: git`, `repository.url: https://github.com/OWNER/REPOSITORY.git`, and `https://npm.pkg.github.com` as their registry. Preserve the catalog component IDs and package basenames. Grant Actions access to existing destination packages and configure the main-only environments.

Forks may leave publishing disabled while using PR validation. Main remote caching is separately enabled with `ENABLE_MAIN_CACHE=true` and the production secrets listed above.
