**status: active**

# Investigation and operations evidence

Implements the remaining approved #251–#254 slices. Earlier completed slices and
proof commands remain in `1100-observability-implementation.md`.

## Investigation

The API exports authenticated OTLP JSON using `Telemetry:Export:Endpoint`,
`Credential`, `Environment`, and `Release`. Its buffer is 8 MiB including records
in flight, with 1 MiB requests and a shared five-second flush/shutdown budget.
Exports omit null identifiers, use no redirects or propagated headers, and never
log export failures back into themselves. Counters distinguish export failures
and dropped records. Logging captures stable events, omits exception messages,
redacts nested sensitive values, and retains bounded basename stack frames.
Handled validation/domain failures use warnings; an exception instance is captured
once. Framework HTTP spans provide server duration measurements.

`./scripts/check-browser-telemetry-consumer.sh` packs the package, installs it
outside the workspace, provisions browser/Node and API applications through the
actual registration checkbox HTTP workflow, and runs the real Wallow API on
loopback Kestrel. The failure controller exists only in the proof executable.
The browser's server-owned session transitions remain synthetic.

The complete Grafana and private-source proof passed with trace
`78ee4317bc49cb2b8be5c59fa468bb22` and application
`app-external-browser-proof-b2cd0150dafb46c08d28811d2d173ba7-external-browser-proof`.
Grafana returned the three-runtime trace and correlated API error. A separate
background ArgumentException was stored at ERROR; request validation remained
WARNING with HTTP 400. Classification correction applies only to Serilog request
logging, preserving unexpected background failures. The rendered dashboard queried without errors.
Browser records passed identity/context, redaction, redirect, and idle-relay checks.
The rendered Tempo view reported three services and a 500 response. The actual
`proof.js` browser crash resolved through the private map to `operation.ts`.

Private source maps use authenticated control PUT/GET at
`/control/v1/source-maps/{registrationId}/{release}/{file}`. Uploads accept flat v3
maps up to the 64 KiB control request limit, 1,024 sources, 4,096 names, and 16,384
mapping segments. The shared sanitized store is capped at 128 MiB. Uploads discard
source contents, source roots and URLs; lookup returns basename, function, and
one-based source coordinates. No public artifact route or URL fetch exists.
Namespaced hashed paths prevent traversal and cross-registration lookup collisions.
Index/section maps are deliberately rejected. Build pipelines must split oversized
maps. HTTP tests verify authentication, malformed inputs, persistence and absence
of private source contents in stored artifacts.

Provisioned Grafana views use application/environment/release/service/severity
filters, native Faro Loki performance, Prometheus request duration, and trace/log
links with a one-minute margin around span times. Operator-local proof uses a
short-lived loopback proxy holding the private admin credential, not Pangolin.

## Operator access

`compose.operator.yml` adds an observability-owned Newt and two explicit private
peer networks. No Docker socket is mounted. Newt's Grafana resource targets the
operator boundary on port 8082; machine ingestion is a separate resource targeting
gateway port 8080 and retains credential authentication. Never route control 8081,
metrics 8083, or storage services through Pangolin.

The boundary requires both the configured socket peer and an explicitly mapped
Pangolin identity, then replaces Grafana identity. It drops caller authorization,
cookies and roles. Grafana trusts only the boundary's stable address, creates
Viewer accounts, disables anonymous/basic/form login, and issues no login tokens.
Do not map an external identity to the private console administrator.

Required live inputs are absent: actual DNS, independent Newt site credentials,
non-overlapping peer networks, verified Pangolin identity header/values and operator
permissions. The example `Remote-User` header is not a claim about a deployed peer.
Local HTTP tests prove peer and identity rejection and header replacement.
`verify-operator.py` also passed against the real Grafana container: operator 200
with Viewer role, ordinary registrant 403, caller at a different explicit peer IP
403, direct Grafana request 401, and no published ports. The fixture never starts
Newt or connects to Pangolin and restores the local Grafana configuration. #252
must remain open until actual Pangolin checks succeed.

For VM-console recovery, stop the operator boundary and Newt, then recreate only
Grafana with a temporary console-only override enabling basic/form login, disabling
auth proxy and publishing `127.0.0.1:<temporary-port>:3000`. Read the existing admin
credential from the root-only `grafana.local`; use an SSH loopback tunnel if needed.
Remove the override, recreate Grafana with the normal operator configuration, and
verify direct/basic requests are rejected before restarting the tunnel. Do not
publish the recovery port on all interfaces or leave the override active.

## Capacity and outages

On the disposable project `wallow-245-proof`, 256 concurrent 8,441-byte log requests
completed in 0.162 seconds: 19 HTTP 200, 237 HTTP 429. Maximum measured request
latency was 44.2 ms; eight control replays stayed below 11.8 ms. An authenticated
1 MiB-plus body returned 413 and compression returned 415. Private metrics counted
refusals. With Alloy stopped, ingestion returned 503 in 43 ms while a management
replay completed in 2.5 ms. The script restored Alloy afterward.

An isolated 8 MiB tmpfs returned ENOSPC after exactly 8,388,608 bytes. This proves
that disposable kernel limit, not the Debian data-volume allocation. A post-load
memory snapshot was gateway 75.19/384 MiB, Alloy 66.9/1024, Loki 111.2/1280,
Tempo 498.1/1024, Prometheus 122.2/1024, Grafana 342.5/512, Garage 8.72/512.
These are snapshots, not sustained-workload peaks.

`storage-readiness.py` is a read-only mount/quota evidence collector. On Darwin it
returns unverified/exit 2 even when all directories exist. On Linux it records
findmnt and available XFS quota reports for comparison with prepared allocations.
It does not provision disks or infer quotas from Garage capacity weights.

The health dashboard has missing-data states and 80/90 percent storage thresholds.
The filesystem metric describes the gateway's mounted filesystem, not a verified
project quota. Configured retention remains 14/7/30 days. `verify-retention.py` passed against a separate Tempo 2.10.8 container using
a 64 MiB tmpfs and 512 MiB memory cap. A trace reached backend storage in 3.06
seconds and disappeared in 47.77 seconds with 45-second retention. Trace
`e5a79a3550ec47a4aacbd3cc02120ddf` was queried using `mode=blocks`, excluding
ingester memory. This demonstrates accelerated Tempo retention only.
Effective runtime configuration confirmed Loki 14 days, Tempo 7 days and Prometheus
30 days with an 8 GiB size limit. Gateway scrape health returned 1.

The sustained browser/Node/API workload completed 240 requests over 65.6 seconds
at 250 ms intervals while forcing Tempo block flushes. Eighteen concurrent Grafana
queries succeeded, and the compaction counter increased from 56 to 68. Sampled
peak memory was gateway 101.8 MiB, Alloy 95.25 MiB, Loki 125.7 MiB, Tempo 509 MiB,
Prometheus 143.3 MiB, Grafana 512 MiB and Garage 10.13 MiB. Grafana reached its
configured cap. The relay recorded 70 accepted, one deliberate rejection, two
dropped records and two failed exports. All application responses retained their
expected behavior. This demonstrates bounded loss under this workload, not
lossless delivery or spare Grafana capacity.

A repeat measured allocated disk usage with `du -sk /tmp/wallow-245-data` around
the entire external proof: 80,192 KiB before and 84,100 KiB after, a net
change of 3,908 KiB. This includes setup, background storage work and
cleanup; it is not a per-record storage estimate. That repeat completed 240
requests in 65.4 seconds, 18 concurrent queries and compaction counter 84→86,
with two dropped records and two failed exports. Trace:
`9c9ff1782d9c2a36950f20e8b4cade38`. Sampled peaks were gateway 102.8 MiB,
Alloy 96.48 MiB, Loki 121.4 MiB, Tempo 550.7 MiB, Prometheus 152.6 MiB,
Grafana 512 MiB and Garage 11.11 MiB.

Actual Debian quota mappings and storage sub-allocation exhaustion remain required
for #253.

## Package release

Release-please now owns `packages/telemetry` and `telemetry-v*` independently.
The publishing workflow uses pnpm, installs Chromium for package tests, separates
build/typecheck from tests, and verifies an isolated telemetry tarball.
`./scripts/check-telemetry-artifact.sh` passed for 0.1.0: isolated install, public
NodeNext declarations, real HTTP log export and sensitive-value removal.

No registry version has been published or installed with consumer credentials.
The full deployment/consumer contract remains outstanding, so #254 stays open.
Consumer CI needs explicit read access to the restricted GitHub package and a
read:packages token supplied through a build secret or temporary npm configuration,
never a Docker ARG, image layer, committed file, or browser environment variable.

## Repository validation

`pnpm check` passed. `./scripts/run-tests.sh all` passed 5,348 tests including
integration, with zero failures or skips. Standards and spec review found no
remaining blockers in the implemented behavior. Expected-failure classification
received an additional real API-to-Grafana check and 422 passing API tests after
that full run. `pnpm lint:actions` and `pnpm lint:env` also passed.
