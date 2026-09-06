# CI cache and artifact reuse evidence

Research for [Establish safe cache and artifact reuse across CI stages](https://github.com/bc-solutions-coder/wallow/issues/208), inspected September 6, 2026 at repository commit `21a0b5fc`. This report supplies evidence for later decisions; it does not select or implement the pipeline.

## What the repository currently does

- [JS CI](../../../.github/workflows/js.yml) runs Turbo build/typecheck and a separate serial test invocation. [Route-tree drift](../../../.github/workflows/route-tree-drift.yml) repeats app builds through Turbo. Both still pay checkout, install and network setup even when tasks hit remote cache.
- [Turbo configuration](../../../turbo.jsonc) records shared TypeScript configs, `.nvmrc`, `NODE_ENV`, dependency build edges, and package `dist/**`. Each [app configuration](../../../apps/wallow-auth/turbo.jsonc) adds `.output/**` and its generated route tree; the tree is excluded from inputs. Auth additionally hashes `AUTH_BASE_PATH`. These declarations make build-output reuse plausible; they do not prove every behavioral input is covered.
- [App Dockerfiles](../../../apps/wallow-web/Dockerfile) invoke the package build directly through pnpm, bypassing Turbo. [PR CI](../../../.github/workflows/ci.yml) builds AMD64 and ARM64 app images, including a separate test-only minimal app. [Deploy](../../../.github/workflows/deploy.yml) builds images again on main. Docker may reuse local layers within a runner; these app build commands declare no persistent external BuildKit cache. The [docs workflow](../../../.github/workflows/docs.yml) does use the GHA BuildKit backend.
- CI and deploy use GitHub caches for required job handoffs: `build-v3-<OS>-<SHA>` for .NET outputs and SHA-keyed image archives. Consumers set `fail-on-cache-miss`. The producer restores prior .NET outputs using a broad prefix and restores source mtimes; its own comment acknowledges false skips for deletions, renames and equal timestamps. A SHA key on the resulting upload does not repair that correctness risk.
- [Package publishing](../../../.github/workflows/package-publish.yml) consumes Turbo build/test results in a credentialed publishing workflow. [Cache compose](../../../docker/turbo-cache/docker-compose.yml) uses `ducktors/turborepo-remote-cache:latest`, static bearer authentication, a persistent local volume and loopback binding unless overridden. It requires a server signature key, but tracked client configuration does not enable `remoteCache.signature`; the three cache-enabled workflows provide no client signature key.

## Separate acceleration from promotion

GitHub cache lookup is scoped by key, version and branch, with default-branch fallback. Fork PRs can read eligible base caches; PR-created caches use the merge ref and cannot be restored by main or sibling PRs. Thus a same-looking key does not transfer a PR cache to main. Cache contents must contain no credentials. [GitHub cache reference](https://docs.github.com/en/actions/reference/workflows-and-actions/dependency-caching).

Artifacts have explicit producing-run identity and immutable artifact IDs. `download-artifact` supports repository, run ID and artifact ID; cross-run downloads require a token. Its current v8 default fails on digest mismatch. This verifies downloaded bytes against stored bytes, not whether the producer was trustworthy. [Download artifact action](https://github.com/actions/download-artifact).

Inference for the design: use disposable caches to accelerate recomputation, and explicitly identified artifacts or image digests for required handoffs. Before privileged promotion, verify the producer repository, workflow, successful run/attempt, event, protected ref and exact intended commit, together with artifact identity and build parameters. Selecting “latest successful” or trusting an attacker-selected name is insufficient. Bind reruns to their producing attempt to avoid stale outputs.

A PR workflow normally checks out GitHub's synthetic merge commit; that SHA need not equal the eventual main or release commit. `workflow_run` can receive secrets and write permissions even when the upstream workflow could not; GitHub explicitly warns about untrusted code and cache poisoning in this transition. Inference: promotion across this boundary needs a trust check, not merely a successful PR status. [GitHub event semantics](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows).

## Remote cache trust

Turborepo artifact signing requires both `signature: true` and `TURBO_REMOTE_CACHE_SIGNATURE_KEY` on the client. It uses HMAC-SHA256 and rejects unverifiable remote entries as misses. A server environment variable alone does not enable that client behavior. Because HMAC uses a shared secret, every holder can sign: signatures do not distinguish a developer laptop from a release builder sharing that key. [Turborepo remote caching](https://github.com/vercel/turborepo/blob/main/apps/docs/content/docs/core-concepts/remote-caching.mdx).

The cache server's current documentation offers JWT read/write scopes and a server-wide `READ_ONLY` mode. Static token configuration alone provides no declared per-client read/write split here. These capabilities must be checked against a pinned deployed image before relying on them. [Cache server settings](https://ducktors.github.io/turborepo-remote-cache/environment-variables).

Turbo supports `--cache=local:rw,remote:r`, but that only governs the CLI. Inference: code holding a write-capable bearer can call the HTTP API directly, so a PR-editable CLI flag cannot enforce a security boundary. Separate server authority or omit credentials. Tailnet membership restricts reachability; it does not establish output provenance. A cache shared with laptops inherits the trust of every writer. [Turbo run reference](https://github.com/vercel/turborepo/blob/main/apps/docs/content/docs/reference/run.mdx).

## Which repetition matters

| Work | Reuse condition or distinct purpose |
| --- | --- |
| JS build and route-tree generation | Same inputs and trusted cached outputs can satisfy both; retain the actual drift comparison. |
| Typecheck and unit tests | Separate checks; a build success cannot replace them. Cached success requires complete inputs and acceptable producer trust. Keep the repository's serial browser-test constraint. |
| Host JS output and container output | Different build environments today. Reuse needs proof of runtime compatibility, build arguments and dependency equivalence; Turbo currently does not participate in Docker. |
| AMD64 and ARM64 images | Runtime images differ even if the native-built JS bundle can be identical. The repository intentionally builds JS on BUILDPLATFORM to avoid emulation hangs. |
| Auth root path and `/auth` path | Distinct compiled outputs because `AUTH_BASE_PATH` changes emitted URLs. |
| .NET coverage | Unit/integration jobs use the coverage collector against restored builds; coverage reports and test execution are not replaced by a JS build cache. |
| Container and external-consumer tests | Exercise packaging/runtime contracts beyond compilation. Preserve the validation even if their inputs are built once. |

No JS coverage-instrumented build was established from the inspected workflow and script paths. Do not claim every apparent rebuild is redundant without tracing the complete consuming harness. If instrumentation changes output, model it as a distinct build input/task before reuse.

BuildKit's GHA backend requires explicit import/export configuration and image-specific scopes to avoid overwriting one another's cache. It remains a layer cache, not an artifact-promotion authority. [Docker GHA cache](https://docs.docker.com/build/cache/backends/gha/).

## Cold path and remaining evidence

A secret-free validation path can run with local caching only. For a cache audit, Turbo's `--force` reexecutes tasks but also overwrites caches; use disabled cache sources (for example `--cache=local:,remote:`) when the purpose is an isolated cold comparison. Preserve serial tests. Compare generated files, emitted bundle behavior and check outcomes, not just timing. [Turbo run reference](https://github.com/vercel/turborepo/blob/main/apps/docs/content/docs/reference/run.mdx).

Before design approval, measure cold/warm task hits, setup time, Docker time and archive transfer costs on representative JS-only, backend-only, shared-config, docs-only and dependency PRs. Validate a cache outage and a fresh fork without secrets. This research did not run builds, query cache contents, read secret values, inspect live tailnet ACLs, verify deployed image/auth settings, or measure hit rates. Tracked configuration establishes opportunities and risks; it does not establish that the deployed cache is secure or effective.
