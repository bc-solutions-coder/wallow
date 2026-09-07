**status: completed**

# Browser telemetry and relay evidence

The browser entry bundles Faro 2.11.0 and explicitly initializes structured logging,
unexpected errors, navigation/resource timings, web vitals and owned request tracing.
The server entry exposes a same-origin relay with validated-session attribution,
random temporary anonymous identifiers, context-generation handshakes and recursive
sanitization. Limits are checked again after trusted context is attached.

The browser preserves fetch redirect behavior. It propagates context only for owned
requests with explicit `redirect: "error"`; default-follow redirects carry no context.
Performance observers have document lifetime and are reused after disposal. Relay URLs
are excluded through Faro's transport API. Disposed or stale request/flush completions
cannot write into a replacement context.

## Verification

- Fifteen public Node, relay and Chromium tests passed. They cover bounded memory and
  export loss, oversized events, 600 compact log records split without truncation,
  forged span identity, login/logout/organization changes, duplicate handled errors,
  origin rejection, outage behavior and delayed request/flush/disposal races.
- `pnpm check` passed, including rendered tests, artifact export checks and the existing
  isolated SDK consumer. `dotnet format api/Wallow.slnx` completed.
- `./scripts/check-telemetry-consumer.sh` passed the packed Node 24.20.0 consumer after
  the browser entry changes. Registration `617e2bda-3bc8-4a9e-a228-da22ae32e60a`, trace
  `cf5ad65ea88b59b7724c6ddacf1a6065`; fatal child exited 1 and sanitized logs, traces and
  metrics were queried from the live private stack.
- `./scripts/check-browser-telemetry-consumer.sh` registered through Wallow's real HTTP
  checkbox workflow, waited for acknowledgement, then supplied the returned one-time
  configuration to an isolated packed Node/browser application. No per-application
  management operation was performed outside Wallow. Application selector
  `app-external-browser-proof-9d0ca9eea1774bb69e18f8ae32cc8238-external-browser-proof`, trace
  `7aab42787a7c8d41b74108dc935a8902` proved browser and Node spans, correlated logs,
  unexpected exception capture, automatic Faro performance, synthetic session transitions,
  forged identity rejection and sensitive-marker absence in actual Loki/Tempo responses.
  Seven relay batches were accepted, one foreign-origin request rejected, zero exports
  failed or dropped. An idle interval longer than the flush timer produced no recursive
  relay requests. Same-origin and cross-origin redirects completed without leaking context.

The proof uses test authentication and server-owned synthetic sessions, not deployed OIDC
or Pangolin. Its temporary control proxy and application listeners bind to loopback and
are removed afterward. The external application receives no management credential.
The actual deployed registration/operator journey and registry publication remain later
parent slices. The browser package now has a tested local registration-to-backend path.

Independent spec and standards reviews found lifecycle/context races, duplicate trusted
span attributes, post-stamping limits and Faro transport exclusions. These were corrected
and the reviewers cleared the final changes.
