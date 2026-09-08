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
An isolated local registry accepted this exact index with PUT status 201 after both
prepared child manifests were uploaded with digest preservation. Reading the index by
tag and digest returned the exact bytes and digest header; each child manifest matched
its recorded digest and size. Evidence was captured at
`/tmp/wallow-publication-image-evidence/multi-platform-registry-proof.json`. The registry
used a loopback-only host port, and its container/network were removed afterward. This
proves local multi-platform registry transport, not hosted authentication or release
authorization.

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

## Fixed GHCR manifest transport

A standalone transport now targets only `https://ghcr.io` and one caller-authorized exact
repository. It exchanges GitHub credentials at the fixed `/token` endpoint for a bearer
token scoped to that repository's `pull,push` actions. No redirect is followed and no
GitHub credential is sent to manifest endpoints. Authentication JSON, manifest bytes,
error reads, and request timeouts are bounded; errors omit remote bodies and credentials.

[GitHub's Container registry documentation](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)
supports workflow `GITHUB_TOKEN` authentication and Docker schema-2 manifests.
The [Distribution token protocol](https://distribution.github.io/distribution/spec/auth/token/)
defines the repository-scoped token exchange, and the
[registry API specification](https://distribution.github.io/distribution/spec/api/)
defines manifest GET/PUT, digest headers, and `MANIFEST_UNKNOWN`.
An unauthenticated GET to GHCR independently returned the fixed realm
`https://ghcr.io/token`, service `ghcr.io`, and the requested repository's pull scope.
No hosted credential or write was used for that protocol check.

Only an explicit `MANIFEST_UNKNOWN` 404 is absent. Manifest reads verify the declared
media type, exact byte digest, registry digest header, and any requested digest reference.
Writes compare an existing manifest first: identical bytes succeed without PUT, differing
bytes fail. A new PUT must acknowledge the expected digest and pass exact-byte readback.
The caller still owns publication authorization and the global writer lock; this is not
registry compare-and-swap against external writers. No workflow uses this transport yet.

All 96 CI helper tests pass, including real HTTP fixtures for missing/identical/conflicting
manifests, scoped authentication, redirects, malformed and oversized responses, failed
writes, readback mismatch, and sanitized failures. Hosted GHCR acceptance remains open.

## Reviewed workflow trigger finding

The private CI security run `34170378504` reported Zizmor `dangerous-triggers` for the
publication workflow's intentional `workflow_run` trigger. The exception is restricted
to that audit, exact workflow path, and the SHA-256 of Zizmor's primary concrete trigger
feature. It expires after 30 days and tracks issue #283. The digest uses an explicit
`sha256:` prefix. A changed trigger block or absent/ambiguous primary feature cannot
inherit this exception; other scanner exception matching remains unchanged in this slice.

The rationale is the reviewed protected-controller checkout, API-bound successful main
producer and exact artifact identity, read-only permissions, and archive inspection
without producer execution or a production environment. The finding remains present with
its applied exception in the retained gate report. Replaying the actual private report
applies exactly one exception; adding another trigger to its feature makes it blocking
again. Behavior tests also cover changed path/audit, missing feature/selectors, expiry,
duplicate exceptions, and primary-location selection. All 98 helper tests pass.

## Read-only controller package inspection

The authorization command now inspects the API-authorized package artifact after image
inspection. Full routes require exactly the sealed `packages.tar.gz`/`pnpm` candidate
bundle; docs routes require no package artifact. The registered catalog and component
versions must match the trusted catalog identities. The controller verifies the immutable
artifact size/digest and producer seal, then extracts only the explicitly named regular
package tarballs into a temporary directory with compressed/decompressed limits.

The producer packs eleven packages. Three are publishable candidates; eight are explicitly
listed as validation-only packages in the catalog. All eleven must be present, and unknown,
duplicate, linked, or traversing outer members fail. Every companion receives bounded
package-manifest and safe-content inspection against its expected name, without requiring
publication eligibility. Companions never enter the candidate inventory. Publishable
packages retain exact hashes, integrity, registered versions, and runtime dependency
metadata, ordered by dependencies among the candidates. This does not authorize releases
or establish registry dependency readiness; no install, lifecycle script, or write runs.

Real nested ZIP/TAR fixtures cover valid candidates and private companions, dependency
ordering, altered seals, wrong versions/companion names, unsafe inner/outer archives,
missing/extra candidates, catalog/version drift, docs routing, and cleanup on failure.
All 105 helper tests, catalog validation, Actionlint, and diff checks pass. The new catalog
requires a fresh matching producer registration. This slice is awaiting review before
commit; hosted package-inspection acceptance remains open.

Candidate inspection is now explicitly separate from destination-bound inspection.
`inspect_candidate` checks registered name/version, safe contents, and packed publication
configuration, and records declared source repository metadata. It does not require the
candidate owner/repository to equal the current fork. `inspect_package` retains those
strict destination checks for enabled package publication. Disabling package publication
therefore does not prevent a fork from using independent image/docs/release capabilities.

Actual private run `34170378504`, attempt 1, source `57fca3f3cd5da04ba7d9b9ed081d5e07557ef94a`,
artifact `10035534673` passed ZIP size/digest, producer seal, and all eleven nested tarball
checks. Candidate versions were api-errors 1.0.0, sdk 2.0.0, and telemetry 0.1.0; all declared
the canonical Wallow repository. Source candidate inspection accepts and records those
values, while destination-bound inspection correctly rejects the unconfigured private
repository. No fixture URL rewrite or identity-check relaxation was used. The old catalog
is intentionally rejected against the newly expanded controller catalog. Evidence:
`/tmp/wallow-private-package-replay-evidence.json`. That producer failed its security job;
this is archive/source-candidate compatibility evidence, not publication authorization.

## Default-off Release Please automation

The Publish controller now calls a separate reusable Release Please workflow only after
successful authorization, on main, with the literal `ENABLE_RELEASE_AUTOMATION=true`.
The called workflow repeats these checks and API producer/controller authorization before
using the existing production `RELEASE_PLEASE_TOKEN`. A missing token fails the enabled
job. No repository variable or secret was changed. The built-in GitHub token remains
read-only; only the owner-selected credential is passed to the official action and its
fixed GitHub actor lookup. The token's broader capabilities are not claimed to be limited.

The pinned official action is release-please-action v5.0.0 at
`45996ed1f6d02564a971a2fa1b5860e934307cf7`. The official tag resolves directly to that commit
and GitHub reports its commit signature verified. Its
[action definition](https://github.com/googleapis/release-please-action/blob/45996ed1f6d02564a971a2fa1b5860e934307cf7/action.yml)
uses the packaged Node 24 entry point. Repository, main branch, config/manifest paths,
GitHub API endpoints, and non-fork operation are fixed by the workflow. No project build,
install, or producer checkout runs. The independent release queue does not cancel active
runs and permits queued runs; the existing exact Actionlint compatibility rule is extended
to this workflow while action_policy.py validates GitHub's supported queue settings.

Sealed evidence records the API run/attempt/job ID, controller SHA, producer identity,
invocation actor and authenticated credential actor numeric IDs, and observed main before
and after. Bounded action outputs supply identifiers for fixed-repository API lookups:
PR IDs/head SHAs/base, release IDs/component versions/tags, and resolved tag commits are
recorded. Partial PR observations survive later release reconciliation failure. No record
authorizes package/image publication; different or newer release commits require their
own successful registered producer and future release authorization. Main observations
and author names are not substitutes for that check.

The Zizmor exception rationale now describes the read-only authorization job and isolated
production release writer separately. Its exact trigger feature remains unchanged; the
privileged job and updated rationale require independent review before this slice commits.

Hosted transport evidence also advanced independently: private run `34172968013`, attempt
1, source `2ab5a0a165be615552d3db5ce2fee44e6a1cadc8`, passed GHCR new-write/readback,
identical retry, and conflict rejection using its package-write GitHub token. Empty AMD64
and ARM64 fixture manifests produced index
`sha256:aebee3ee73c56dfaf2dbf57cb9cdd5f923b4273f29db44f1c488b4762c440e02`.
This proves hosted transport only, not application release acceptance; see
[issue #283 evidence](https://github.com/bc-solutions-coder/wallow/issues/283#issuecomment-5577161988).

This slice passes all 112 helper tests, Actionlint, immutable action/queue policy, and
format/diff checks. A fresh pinned Zizmor 1.30.0 strict scan collected all seven workflow
and action documents, including the new reusable release workflow: seven findings and
zero unexcepted blockers. Independent privilege-boundary review passed; no release
variable was enabled and no production Release Please invocation was run locally.

## Fresh dependency inspection

The read-only controller now consumes the exact API-authorized `dependency-inputs`
artifact. ZIP identity and the producer seal are verified before bounded extraction.
Only the registered pnpm lockfile and NuGet lockfiles derived from registered API project
paths are accepted; the inventory must match both sets exactly. The pnpm bytes must match
the registered source hash. Generated NuGet locks are bound through the registered
artifact and seal, rather than an invented source hash.

A Trivy-only installation path reuses the pinned 0.74.0 binary checksum and downloads the
current vulnerability database. The scan disables another database update so it uses
the captured database snapshot. The controller scans verified files without a restore,
package installation, or project execution. Every expected target must have the correct
package ecosystem and nonempty package coverage. Findings are evaluated against the
protected controller's current exception policy. A blocking finding fails inspection;
raw scan/database metadata, input hashes, and gate findings are retained even on failure.
Diagnostics use a separate artifact so the successful publication plan keeps its exact
two-file payload/seal layout for downstream verification.
Temporary archives and extracted lockfiles are removed on success and failure. This is
fresh dependency inspection, not destination or release authorization.

The 117 helper tests pass, including real ZIP/TAR fixtures for altered seals, lockfile
hashes, unsafe members, duplicate/missing/extra inventory, scanner coverage, new fixable
HIGH findings, and cleanup. Actionlint and immutable action policy pass. A prior real
private scanner report contains exactly 57 expected targets (56 NuGet projects plus
pnpm). The producer now explicitly writes USTAR headers to match the strict reader.
Repacking the real local export proof with this format passes archive and pnpm identity
checks, then correctly fails inventory: it contains only 55 NuGet locks. This is format
and rejection evidence, not complete producer acceptance. Replay of the complete Linux
producer artifact and hosted fresh-scan acceptance remain pending.

## Complete image preparation wiring

The read-only controller now prepares every publishable catalog image/platform inside
its existing verified bundle loop. It downloads each source bundle once, checks the
validation-only companion, and excludes the companion from preparation. Full routes
prepare application, infrastructure and documentation images; docs routes prepare docs.
No application image is loaded or executed, and no registry write credentials are used.

Skopeo 1.22.2 uses the previously proved immutable
`quay.io/skopeo/stable@sha256:e5d9c4af8ec327785c7ca938d1e4f8452c6a05014850e58e2ff9456899ebd97c`.
The fixed conversion runs with no network, a read-only container filesystem, no Linux
capabilities and only dedicated input/output/scratch mounts. It writes Docker v2
compressed directories using the official
[copy format and compression options](https://github.com/podman-container-tools/skopeo/blob/v1.22.2/docs/skopeo-copy.1.md).
Configuration digests and every compressed/decompressed layer are checked before and
after scanning. The regular-file scan archive serializer binds Trivy's input to those
exact source bytes. Image reports must match ImageID, DiffIDs, configuration platform
and the exact catalog tag; package coverage and the current protected policy gate remain
mandatory. Finding scopes use the producer's `tag::target::package@version` convention,
normalizing the temporary scan archive path to the original tag. Image scans use `--skip-db-update` against the dependency scan's freshly
captured database; docs routes initialize that database once themselves.

Each bundle produces a sealed USTAR artifact containing only referenced blobs, exact
manifests, both-platform indexes and inventory. The inventory binds the source artifact,
producer, authorized controller, current preparation run/attempt, catalog digest, source
and prepared identities, index digests and report hashes. The seal's revision must equal
the authorized controller. Prepared artifacts and diagnostic reports are uploaded
separately; failed preparation leaves no successful partial bundle. Artifact names include
the preparation run and attempt. These records explicitly do not authorize publication.
The authorize timeout is 90 minutes for sequential preparation of all sixteen variants.

Local actual Garage and Postgres Replica exports passed all four infrastructure variants
through the new conversion, scanner and serializer. The resulting 304,076,800-byte TAR
has digest `sha256:4b8e8c77fa03e3d0837151456333860a742a03425a0a30711edd5ae853dc6719`.
Evidence is `/tmp/wallow-real-image-preparation-evidence.json`. This replay used the pinned
Trivy 0.74.0 container as a macOS runner adapter and the prior proof's captured database;
its invocation seal is synthetic. It proves local conversion/scanning/serialization, not
API authorization or hosted acceptance of all sixteen variants. The real CLI also caught
and removed an unsupported filesystem-only scanner option before handoff.

Hosted dependency acceptance now passes independently: private Publish run `34175645116`
consumed exact producer run `34173777794`, attempt 1, with all 56 NuGet locks and pnpm.
The refreshed Trivy database yielded one MEDIUM finding and zero blockers; see
[dependency evidence](https://github.com/bc-solutions-coder/wallow/issues/283#issuecomment-5577557590).
The disabled Release Please job skipped, and an independently enabled fixture without a
token failed before the official action, with its opt-in removed afterward; see
[release boundary evidence](https://github.com/bc-solutions-coder/wallow/issues/283#issuecomment-5577570079).

Independent review approved this preparation slice. All 131 helper tests pass; the four
focused preparation tests also pass after the final scope-alignment adjustment.
Actionlint, immutable action policy and formatting checks pass. Hosted complete image
preparation remains the next acceptance step.

## Main image publication and nightly ordering

The controller now has a real image writer behind literal `ENABLE_IMAGE_PUBLISH=true`.
It uses the separate `image-publish` environment with no production secrets. Enabled
configuration requires an existing custom deployment policy permitting exactly the `main`
branch; the read-only controller checks that policy before starting the privileged job.
The writer repeats the environment, protected controller, successful registered producer,
current preparation run/attempt/job, sealed plan and immutable artifact API checks. Its
GitHub token has package-write plus repository/Actions/checks read permissions only.
No project build, dependency installation, Dockerfile or application execution occurs.

Prepared archives are extracted into dedicated temporary directories only after their
GitHub ZIP identity, producer seal, exact plan digest, USTAR members, catalog/platform
coverage, configuration/layer digests, prepared manifests and indexes match. Skopeo uses
the pinned image and a temporary mode-0600 auth file for fixed GHCR digest-addressed copies.
Each child is read back by digest, including all blobs, and rechecked against its original
configuration and decompressed layers. Credentials and extracted data are removed after
success or failure. Completed and partial progress are retained independently of CI.

All immutable full-SHA indexes publish before any nightly update. The writer uses OCI
indexes with standard
[annotations](https://github.com/opencontainers/image-spec/blob/v1.1.1/image-index.md)
and unchanged Docker-v2 children. The canonical index binds source repository, full SHA,
catalog image ID, both child digests and the exact prepared Docker index digest. It omits
controller, preparation run and artifact IDs so a new preparation of identical source
bytes remains an identical retry. Those invocation identities stay in the sealed plan
and progress record. Differing immutable bytes fail; identical results are reverified.

Before advancing `nightly`, the writer checks strict index/provenance structure, exact
byte equality with the recorded source's full-SHA immutable reference, known catalog
identity and both platform descriptors, and GitHub main ancestry. Unknown legacy targets,
missing immutable evidence and incomparable history fail. An older retry may finish its
missing immutable output but leaves nightly unchanged. The alias transport immediately
rechecks the observed previous value before PUT and verifies exact readback. The shared
non-cancelling workflow queue serializes these writers; this does not claim registry CAS
against external writers. Repository package-write authorities can alter registry state.
Annotations alone are not authority, and this main-image progress mechanism does not
replace release authorization or durable release receipts.

Independent hosted proofs now cover OCI indexes with Docker children, exact tag/digest
readback, Skopeo selected-platform pulls, Docker Engine pulls on both architectures, and
digest-addressed Skopeo pushes. They used synthetic images; application publication is
still a separate acceptance step. The full hosted preparation run `34176316160` passed
all sixteen real variants, and docs-only run `34176637603` passed exactly two docs variants.
Evidence includes `/tmp/wallow-hosted-image-preparation/evidence.json` and
`/tmp/wallow-hosted-docs-preparation/evidence.json`. Job-token reads of environment/policy
metadata passed with Actions read access, and a non-main job was rejected by the actual
`image-publish` environment even with `deployment: false`.

All 145 helper tests pass, including real sealed archive writer fixtures, exact retry and
conflict handling, older/unknown alias cases, partial cleanup, current invocation API
checks, and HTTP alias state/readback tests. Independent review approved the code and
updated exact Zizmor privilege rationale. The real repository remains opted out; hosted
image writer acceptance will start in the disposable private repository.
A fresh pinned Zizmor scan reports eight findings and zero unexcepted blockers; Actionlint,
immutable action policy, formatting and diff checks also pass.

### Durable release origin and producer selection

The default-off `ENABLE_RELEASE_AUTOMATION` path now records each exact Release Please PR revision before Actions artifact expiry. A separate main-only `pull-requests: write` job appends a bounded, machine-readable provenance comment using only its job token. It verifies the protected action job and exact sealed action evidence first. Numeric credential actor identity is correlation; PR author or labels alone never establish automation origin. The comment binds the PR API ID/head SHA, protected invocation, and exact artifact digest. Readers require the comment to remain unedited and its recorded job to have succeeded.

A separate `contents: write` job rechecks releases on each enabled successful main invocation. It binds the live release ID/tag, resolved commit on main, source manifest component/version, merged PR revision, durable PR origin and protected release-creation action evidence. It preserves an immutable `wallow-release-origin-v1.json` on that actual Release even when the release commit still lacks its own successful producer. There is no synthetic release bucket. `wallow-release-selection-v1.json` then pins the earliest successful registered producer by run ID/attempt, or an explicitly named producer on manual retry with `release_id`. A complete job inventory can establish that a historical run had no successful registration; missing or expired artifacts from a successful registration fail closed. Existing selection always wins, and ordinary retry never rebuilds or substitutes newer artifacts.

Receipt asset IDs, exact bytes/digests, numeric GitHub Actions uploader, and upload timestamps are checked against their protected originating job. New writes receive exact API metadata and byte readback. A failed recorder's partial asset is not authority. Retry must revalidate the original origin/action evidence and selected producer, preserve original bytes, and add an immutable endorsement bound to the original asset ID/digest. Only a successful endorsement job makes that partial receipt usable. Failed PR recorders can append a new origin after fresh action-evidence verification. Expired evidence requiring new production remains pending explicit recovery; no blanket recovery override is introduced.

These receipts record origin and selection only (`publication_authorized: false`). Package/image release writers must still enforce current security preparation, destination capability, complete component coverage, and release progression before writes; those integrations remain subsequent work. No project build/install, registry release write, or production credential is added to either recorder. Only the official Release Please job uses the existing production token. The workflow trigger exception requires independent review of these added narrow writers.

Hosted transport evidence: disposable Release asset probe `34178655149`, source `cace5462052ebf9b6c2eab8d039fba22601960f9`, used a job token with contents write. Upload returned asset `549704331` on synthetic prerelease `384401213`, exact SHA-256/size and numeric uploader `41898282`; API octet-stream retrieval redirected to signed storage without forwarding Authorization, returned exact bytes, and duplicate-name upload was rejected with 422. This proves transport behavior only, not application release authority.

## Pages artifact preparation

The Pages API protocol probe on disposable public repository `wallow-pages-acceptance-20260908`, run `34179198582`, accepted an older producer SHA as `pages_build_version` under current protected controller OIDC and served the exact uploaded marker. This proves transport only; authorization, durable freshness, and real Pages cutover remain pending.

The site preparation helper inspects the bounded compressed archive without extracting producer files. It rejects links, special files, unsafe or duplicate names, file/directory collisions and unsupported PAX metadata. It emits a deterministic regular-file Pages TAR, preserves long DocFX names and every file byte, and records per-file hashes for readback. Producer metadata is not executed. The real site artifact from docs producer `34176345104/1` has 1,170 files. Independent comparison matched every original file hash after preparation; resulting TAR is 47,964,160 bytes with SHA-256 `d6022b665c3547f2ecbb6d8e916cfa3879493a93e10d324f1e7266d684e851b9`. This helper is not yet wired to a deployment job.

Read-only authorization now downloads and verifies the exact registered DocFX artifact, prepares it without credentials, records the source artifact and all content hashes in the sealed plan, and uploads a single-file prepared-site artifact only on success. Three additional archive/identity behavior checks cover exact bytes, wrong seals, unsafe site content, and missing/duplicate candidates. The 186-test helper suite and Actionlint pass. Pages writer credentials and deployment remain separate work.

Hosted preparation run `34180670994/1` verified source artifact `10037423739`, sealed plan `10038806550`, and prepared-site artifact `10038807815`; all 1,170 original file bytes match the prepared TAR and plan inventory.

Pages history now authenticates durable deployment intents against exact protected jobs and creation windows, including failed jobs. Prior newer intents prevent older retries, same-source content conflicts fail, and every incomparable intent remains an error. Transport operations use fixed GitHub API paths, constrained Actions OIDC requests, bounded status polling, and credential-free exact public index readback. They are not yet wired to the final writer job or historical bootstrap.

Actual helper transport passed on disposable public Pages run `34181004688`, controller `dc5055a23238a2e7c66941f858ea47c543222f50`, historical source `6db46dafcebba8ce87fe2f87ec9116f6748b40f3`, artifact `10038897926`, intent `6319601110`, success status `17965453487`. The unchanged transport helper authenticated OIDC, retained/read back intent payload, deployed the exact artifact, observed service success and verified the public index hash `3dc86de7168779e39fc81ca087de06529e7bda1fc723437beae078bd30e93a3d`. This is transport acceptance with synthetic content, not final producer authorization or real target cutover. The 197-test helper suite passes; independent history/transport review and changed-file DevSkim checks pass.

GitHub protocol references: https://docs.github.com/en/rest/deployments/deployments and https://docs.github.com/en/rest/pages/pages. Polling follows the success/failure distinction in https://github.com/actions/deploy-pages/blob/main/src/internal/deployment.js while retaining bounded requests and sanitized errors.

## Protected Pages writer

The reusable `docs.yml` writer is now wired behind the approved literal `ENABLE_DOCS_DEPLOY` flag. It requests only the exact main-only github-pages environment, repository/Actions/check read access, and Pages/OIDC/deployment write permissions. A shared preparation authorizer preserves the existing image boundary and separately binds the Pages writer to the exact successful protected preparation, sealed source plan, and single-file prepared TAR digest. It does not rebuild or execute site content.

Before deployment, the writer authenticates the newest durable Pages intent as the source frontier. Protected writer code only records this intent after authorization and forward-only checks, including when later deployment fails; older retries never create an intent. The recorded prior deployment ID and rechecked deployment lists detect changes around intent creation. This preserves rollback protection while avoiding a workflow API lookup for every historical deployment. Same-source retries require identical TAR, inventory and index identities. It retains progress after each mutation and verifies service success, exact public index bytes and final deployment status readback.

The first real cutover is bounded by `.github/ci/pages-bootstrap.json`: exact retired Docs run/attempt/workflow/job, deployment ID and success status, current-main source ancestry, and the observed index hash. Read-only replay verified all 163 existing deployment records and the reviewed baseline without writes. This proves the baseline's source and index, not full historical site byte equivalence. After a verified new intent establishes the frontier, the old bootstrap is no longer used; remove its configuration and legacy verification path after the real cutover. Forks without prior content start from a confirmed missing public index; existing unknown content or deployment records fail.

Independent review approved the writer, new privileges, exact reusable job name and frontier induction. Twenty-five Pages behavior tests cover preparation identity, environment policy, malformed history, failed intent ordering, concurrent insertion, same-source conflicts, exact content readback and partial progress. Existing image authorization tests still pass after the common-authorizer extraction. Hosted full writer acceptance and real cutover remain required.
