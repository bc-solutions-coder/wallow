**status: active**

# Verified publication implementation plan

Issue: #283. Execute the approved layout and acceptance matrix on #212/#227, with the production-environment amendment. This plan sequences implementation; those tickets own the design.

## Outcome

Publish only exact validated main/release artifacts, with independent image/package/Pages/release opt-ins, isolated credentials, fresh security scans, durable provenance, safe retries and verified target readback. Shared validation and main-only signed Turbo remain #280. Application rollout is excluded.

## Implementation sequence

1. Create `.github/ci/publication.json` as the reviewed component/package/image/platform/variant catalog. Include root, api-errors, SDK and telemetry releases, all intended main image variants and the validated docs site. Validate catalog consistency against release configuration and packed package manifests in credential-free CI. Exercise valid catalogs and wrong/duplicate component/tag/scope/platform inputs with behavior fixtures.
2. Implement `scripts/ci/publication.py` authorization and identity helpers using the standard library. Require expected repository/default-main control definition, successful CI workflow ID/path/event/ref, exact source SHA/run/attempt, expected jobs and route, immutable artifact IDs/digests and mandatory non-expired artifacts. Validate actual downloaded bytes and safe archive members in dedicated directories. Reject PR/foreign/failed/cancelled/unexpected/replaced producer metadata through local HTTP fixtures before wiring jobs.
3. Add `.github/workflows/publish.yml` with completion and protected-main retry entry points. Its read-only controller resolves explicit producer/release identities and emits exact job plans. Use one repository-wide non-cancelling publication queue for every entry point. All write opt-ins default off, and enabled incomplete configuration fails preflight. Controller code/config comes from the protected-main workflow revision, distinct from artifact source.
4. Add read-only publication preparation and current-policy scanning. Reuse exact package tarballs and image exports; never build or pack again. Check every declared platform/config/version/build variant and dependencies. Prepare deterministic copy inputs with immutable digests, retain reports, and fail on scan/database errors. Current exceptions apply even to historical bytes. Test wrong/partial/malformed image/package inputs and stale-policy retries.
5. Implement narrow image and package publisher commands. Credentials are scoped to separate main-only jobs/environments and passed only to fixed registry operations. No project checkout execution, install, lifecycle hooks or Dockerfile runs. Publish immutable SHA/version identifiers first, read registry results back, recognize identical existing outputs and reject conflicts. Resolve dependency ordering from packed manifests and authorized releases; absent unauthorized dependencies stop publication.
6. Implement separate durable receipt writes and retry reconciliation. Release assets retain exact source/controller/producer/artifact/component/digest/integrity provenance beyond Actions retention. Verify existing receipts against registry reality before continuing. Partial publication resumes only missing authorized work. No mutable cache is authority.
7. Add reusable `.github/workflows/release-please.yml`. Run only after successful main validation when enabled, under production, with the approved repository-scoped automation identity and no bypass or project build. Verify exact merged release PR, bot identity, controlled component/version mapping and resolved tag SHA. Reconcile a newer release-please result against its own successful producer; leave unmatched releases pending. Include telemetry and independent package releases.
8. Add guarded alias and Pages publication. Compare source ancestry/component versions under the shared writer lock. Older or incomparable attempts cannot roll nightly/latest/major/minor/Pages backward. Reuse the validated docs site in `.github/workflows/docs.yml`; never rebuild it under deployment credentials. Record partial alias progress and queue cancellation/overflow recovery without claiming atomic cross-image updates.
9. Implement explicit historical recovery via protected-main workflow control and credential-free full validation of the approved release source. Bind the replacement run/attempt to the release and prior producer. Missing/expired artifacts stop ordinary retries. Already-published differing bytes stop recovery. Test actual missing GitHub artifacts and simulated retention metadata with clearly stated evidence limits.
10. Exercise real disposable event-driven acceptance with all capabilities off, each capability independently enabled, missing configuration, public/private profiles, exact registry bytes/platforms, Pages readback, release/tag races, producer rejection, identical/conflicting/interrupted publication, dependency readiness, fresh rescans, historical recovery and overlapping/stale writers. Keep actual targets disabled until applicable evidence passes.
11. Rebase onto merged shared-validation main, review spec compliance and code quality, merge through the required check, then enable each owner-requested target independently. Verify the first intended images/packages/docs/release operation using registry/Pages/release metadata and durable receipts. Document setup, retries and remaining owner configuration without secret values. Retire all obsolete writers; do not reactivate them as fallback.

## Verification

Use behavior-focused Python tests and actual temporary archives/HTTP responses, not source-text assertions. Run `python3 -m unittest discover -s scripts/ci -p 'test_*.py'`, Actionlint, the affected repository quality checks and actual GitHub/registry/Pages runs. Every acceptance item needs exact source/controller/producer identities, run/attempt URLs, expected and observed results, and registry/report/receipt evidence. A green disabled workflow is not publication acceptance.

## Initial state

The worktree starts at the shared-validation branch rebased onto main `72203c76`. Its 22 Python behavior tests and Actionlint pass, and dependency installation completed with the frozen lockfile. #280 is still open: private browser diagnosis, restricted tailnet grants, hosted-main signed cache evidence and protection/merge cutover remain. No publication capability has been enabled.

## Image preparation progress

The image inspector verifies exact local tags/platforms, content-addressed configurations and every uncompressed layer against configuration diff IDs without loading containers. It accepts the plain regular-file/directory Docker-save format produced by CI and rejects extended TAR headers before metadata parsing. Shared archive handling bounds decompressed bytes; image inspection also bounds member count and JSON metadata. Behavior checks cover altered layers, missing images, wrong platforms/tags, malformed configuration addresses, unsafe members and extended headers.

The actual private acceptance infrastructure export from run `34167381437`, attempt 1, artifact `10034755096` passed inspection for Garage and Postgres Replica on both AMD64 and ARM64. This proves archive inspection only; registry copies, fresh scanning, release authorization and publication readback remain open. All publication capabilities remain disabled.

Preparation experiment: Skopeo 1.22.2 at container index digest
`sha256:e5d9c4af8ec327785c7ca938d1e4f8452c6a05014850e58e2ff9456899ebd97c`
converted the actual Garage AMD64 export without network access or application execution.
OCI conversion preserved JSON values but changed configuration bytes; Docker v2 output
(`copy --format v2s2 --dest-compress` to a directory) preserved the exact configuration
digest. The prepared-image verifier checks that digest, each compressed blob digest/size,
and each bounded decompressed layer against the original inspected export. The actual
prepared manifest digest is `sha256:79612b00f51c982577cd7e1ddeca491198cd22f7ebbdc74c339a3c208271d9a4`.
Registry copy/readback and fresh scanning remain separate acceptance requirements.

An isolated local registry round trip now passes for the actual Garage AMD64 and ARM64
variants. `skopeo copy --preserve-digests` uploaded prepared directories, then downloaded
them into fresh directories. The verifier matched the exact manifest, configuration,
compressed layer digests/sizes and decompressed layer identities on readback. AMD64 used
the manifest above; ARM64 used
`sha256:960955c7e194fa5970ef8d23666bb46f322ee3653055b091f873d0d72adf916c`.
The registry was isolated on an internal Docker network without host ports and removed
afterward. This proves byte-preserving transport, not hosted authentication, multi-platform
index publication, authorization, conflict/retry handling or fresh scanning.

The manifest-list builder now requires exactly one verified AMD64 manifest and one
verified ARM64 manifest, preserving their digest and byte size in deterministic output.
The actual Garage variants produce index digest
`sha256:d14e9e43f268e7db861aec70e5e17cf363b06d8465b7cbb9149cef8a14c41410`.
This index has not yet been published or read back from a registry.

## Read-only controller image inspection

The authorization command now compares the registered catalog with the validated catalog
from the protected controller checkout, then downloads each authorized image artifact by
immutable ID. Full routes require app, infrastructure, and docs bundles; docs routes
require only the docs bundle. Each ZIP is checked against the GitHub size/digest and its
producer seal before Docker-save inspection verifies every exact catalog tag/platform.
Downloads and inspection use sequential temporary directories, cleaned on success and
failure before the next bundle. The sealed plan retains only inspection metadata.

The authorize job retains read-only GitHub permissions and has no production environment,
image execution, rebuild, or registry write. Its timeout is now 30 minutes to accommodate
large bundles. All 87 helper tests pass, including real sealed ZIP/TAR fixtures with fake
transport, catalog drift, missing/extra/duplicate bundles, altered seals, unexpected tags,
download tampering, transport failure, and cleanup. Actionlint passes. Hosted execution
and registry publication remain separate acceptance work.

The app producer also exports `wallow-bff-example:test` as an AMD64 validation companion.
The catalog now lists it explicitly under `validation_only_images`, separate from images
with publication destinations. Inspection requires and verifies this companion alongside
publishable images, then excludes it from the returned publication inventory. Unknown
extra tags still fail. Catalog validation rejects duplicate tags, overlap with publishable
images, unsupported platforms/bundles, and publication fields on companion entries.
Real archive tests cover accepted/excluded companions, unexpected companion tags, and
incorrect companion architecture. All 88 helper tests pass.
