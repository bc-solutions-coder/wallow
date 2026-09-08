# Configure CI and publication

Use this guide to configure a repository and retry publication from an exact validated build.
Application rollout is covered by the [deployment guide](deployment.md).

The publication migration remains in progress in [issue #283](https://github.com/bc-solutions-coder/wallow/issues/283).
Keep each production publisher disabled until its acceptance and first-target cutover checks pass.
Images currently publish immutable full-SHA tags and `nightly`. Release-version image tags remain under implementation.
Packages currently publish immutable versions with a `validated-<hash>` staging tag.
Stable release aliases and historical artifact recovery are not yet available.

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

| Secret | Purpose |
| --- | --- |
| `TURBO_API` | Complete private cache URL, including the correct scheme and port |
| `TURBO_TEAM` | Cache namespace |
| `TURBO_TOKEN` | Cache bearer credential used by CI |
| `TURBO_REMOTE_CACHE_SIGNATURE_KEY` | Independent client key for signed cache artifacts |
| `TS_OAUTH_CLIENT_ID` | Tailscale OAuth client ID |
| `TS_OAUTH_SECRET` | Tailscale OAuth client secret |

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

Each capability uses a repository Actions variable with the literal value `true`.
An absent variable or another value leaves that capability disabled.

| Variable | Required configuration |
| --- | --- |
| `ENABLE_IMAGE_PUBLISH` | `image-publish` environment allowing only the exact `main` branch; repository access to its GHCR destinations |
| `ENABLE_PACKAGE_PUBLISH` | `package-publish` environment allowing only the exact `main` branch; package ownership and repository linkage configured below |
| `ENABLE_DOCS_DEPLOY` | `github-pages` environment allowing only the exact `main` branch; Pages source set to GitHub Actions |
| `ENABLE_RELEASE_AUTOMATION` | `production` environment with `RELEASE_PLEASE_TOKEN` |

Image and package writers use their job-scoped GitHub token. They do not need a copy of the production token or a repository `NODE_AUTH_TOKEN`.
Pages uses its job token and GitHub OIDC. Keep publication environments separate from `production`.

For packages in a fork:

1. Update `.github/ci/publication.json` to use the fork owner's package scope across publishable and `validation_only_packages` entries.
2. Preserve the publishable basenames `api-errors`, `sdk`, and `telemetry`, which match their component IDs.
3. Update all workspace package manifests and internal dependencies to match the new scope.
4. Update `release-please-config.json` package names to match.
5. Set each publishable manifest's `repository.url` to `https://github.com/OWNER/REPOSITORY.git` and `repository.type` to `git`.
6. Retain `https://npm.pkg.github.com` as the package registry.
7. Update the lockfile and pass CI before enabling package publication.
8. Grant the repository Actions access to existing destination packages when needed.

The catalog includes api-errors, SDK, and telemetry. Package publication verifies required internal dependencies before uploading any candidate.
Existing versions must match the validated tarball bytes. A version conflict stops publication.

For an existing Pages site or legacy image tags, complete the reviewed migration recorded in issue #283 before enabling the writer.
Unknown existing content is not accepted as publication authority.

## Configure Release Please

1. Add the automation token as `production` secret `RELEASE_PLEASE_TOKEN`.
2. Verify that the token has the repository access needed to create release PRs, tags, and GitHub releases.
3. Review `release-please-config.json`, `.release-please-manifest.json`, and the publication catalog together.
4. Enable `ENABLE_RELEASE_AUTOMATION` after the protected controller has passed its acceptance checks.
5. Review and merge the generated release PR through the normal required checks.

Release Please runs from the protected main publication workflow. Its generated branch does not receive the production token.
The generated PR runs ordinary PR validation. The release commit needs its own successful registered main CI producer before its outputs can be published.

Release-origin and producer-selection receipts are retained on GitHub Releases. Do not delete or edit these receipts to force a retry.
Existing authenticated receipts can support publication while Release Please automation is disabled.

## Retry an exact publication

Use a new `Publish` dispatch on `main` when retrying with the current controller.
Select the successful **CI producer** run and attempt, not the failed Publish run ID.

```bash
gh workflow run publish.yml --repo OWNER/REPOSITORY --ref main \
  -f run_id=SUCCESSFUL_CI_RUN_ID \
  -f run_attempt=SUCCESSFUL_CI_ATTEMPT
```

For a particular release, add `-f release_id=GITHUB_RELEASE_ID`.
Once a release has a producer-selection receipt, supply that exact producer. Another successful build cannot silently replace it.

Publication jobs share a queue. Let the current invocation finish before interpreting a pending retry as a failure.
An identical retry verifies existing registry bytes and resumes missing work. Older main retries cannot move nightly or Pages backward.

Inspect the failed job and its retained plan, scan reports, and progress artifacts.
Progress can contain successful uploads even when a later operation failed.
The finalizer records durable release receipts separately from registry writes.

If required producer artifacts or incomplete-publication evidence have expired, ordinary retry stops.
Do not rebuild under publication credentials or substitute artifacts from another source.
Historical recovery remains an open migration task in issue #283.

## Verify enablement

Enable one capability at a time after its acceptance checks pass.
Record the source commit, producer run and attempt, Publish run, and destination readback for the first real operation.
For images, verify both platform manifests and the index digest. For packages, verify the downloaded tarball integrity.
For Pages, verify the served content and deployment progress.
For Release Please, verify the generated PR, its required checks, the merged release commit, and its release receipts.

Disabling a variable prevents later jobs for that capability. It does not remove published outputs or stop a job that has already started.
