# Self-hosted Faro and LGTM integration research

Research for [Establish the self-hosted Faro and LGTM integration options](https://github.com/bc-solutions-coder/wallow/issues/236), within [the Wallow observability map](https://github.com/bc-solutions-coder/wallow/issues/234).

Reviewed September 6, 2026. Wallow source baseline: `a704634f`. This is documentation and source inspection, not a test of the deployed system. The operator is the only viewer; self-hosting and Pangolin-protected Grafana are requirements. Device runtime scope remains open.

## Supported collection paths

Faro is an OSS browser SDK covering manual logs, unhandled exceptions/rejections, performance measurements, events and OpenTelemetry-based traces. Grafana documents collection into self-hosted Loki and Tempo. It does not require Grafana Cloud. [Faro overview](https://grafana.com/oss/faro/)

Alloy's GA `faro.receiver` is the documented self-hosted choice. It sends logs to `loki.*` receivers and traces to `otelcol.*` consumers; it does not provide a metrics output. Its listener defaults to loopback port 12347. CORS, optional `X-API-Key`, payload limits and rate limiting are configurable. Its source-map support includes downloads, local files, remote locations and release-templated paths. This permits private build artifacts to be mounted rather than publicly serving maps. Restrict or disable arbitrary map downloads; the documented default permits all origins. [Faro receiver](https://grafana.com/docs/alloy/latest/reference/components/faro/faro.receiver/)

The alternative `otelcol.receiver.faro` feeds OTel logs/traces directly, but remains **experimental** and requires the experimental stability flag. Sharing Wallow's existing OTLP pipeline is possible; it is not a reason to assume equivalent maturity. [OTel Faro receiver](https://grafana.com/docs/alloy/latest/reference/components/otelcol/otelcol.receiver.faro/)

Grafana explicitly recommends `faro.receiver` for self-hosting. Its data does not activate the Grafana Cloud Frontend Observability application, even if sent to Cloud. Plan to provision OSS dashboards and correlations; do not promise the Cloud frontend UI by installing Faro. [Component selection](https://grafana.com/docs/alloy/latest/collect/choose-component/)

**Integration inference:** browser → same-origin app ingestion → internal Faro receiver → Loki/Tempo is feasible. A proxy can retain collector credentials and stamp trusted application identity. Direct browser-to-Alloy access is an alternative, not a Faro requirement. The exact proxy envelope, authentication and release delivery contract still need design and runtime verification.

## Existing deployment gaps

The production Compose profile runs `grafana/alloy:latest` and `grafana/otel-lgtm:0.8.1`. The LGTM service has no persistent volumes; its only published port is Grafana on loopback. Alloy currently receives OTLP on 4317/4318 and exports all signals to the bundled backend; there is no Faro receiver. Node applications now use HTTP port 4318. [Production Compose](https://github.com/bc-solutions-coder/wallow/blob/a704634f/docker/docker-compose.production.yml), [Alloy configuration](https://github.com/bc-solutions-coder/wallow/blob/a704634f/docker/alloy/config.alloy)

Grafana describes `otel-lgtm` as intended for development, demonstrations and testing. It is a useful local verification environment, not evidence that the current profile is ready for persistent production telemetry. Recommendation for the deployment decision: separately managed, version-pinned Grafana, Loki, Tempo and a metrics backend, with explicit volumes/object storage, retention and recovery. This research does not choose capacity or retention. [Grafana's image announcement](https://grafana.com/blog/an-opentelemetry-backend-in-a-docker-image-introducing-grafana-otel-lgtm/)

API logging is disabled in base settings. API tracing registers ASP.NET Core and HttpClient instrumentation and a parent-based sampler; the repository guide identifies missing dedicated EF Core and Wolverine message-processing spans. Enabling the stack alone does not produce complete system coverage. [API settings](https://github.com/bc-solutions-coder/wallow/blob/a704634f/api/src/Wallow.Api/appsettings.json), [Service defaults](https://github.com/bc-solutions-coder/wallow/blob/a704634f/api/src/Wallow.ServiceDefaults/Extensions.cs), [Observability guide](https://github.com/bc-solutions-coder/wallow/blob/a704634f/docs/operations/observability.md)

## Browser-to-API correlation

Faro's tracing package instruments browser requests. Cross-origin propagation requires explicitly configured `propagateTraceHeaderCorsUrls`; cross-origin servers also need appropriate CORS behavior. Same-origin collection does not itself establish browser-to-API trace continuity. [Official browser quick start](https://github.com/grafana/faro-web-sdk/blob/main/docs/sources/tutorials/quick-start-browser.md)

Wallow's SDK BFF constructs outgoing headers from an allowlist containing content type, accept and forwarded network headers. **It drops `traceparent` and `tracestate`.** A request ID is set separately. Therefore adding Faro alone cannot connect a browser span through this proxy to the API. The design must preserve/validate W3C trace context and, if BFF execution itself should appear, add server instrumentation. Proof should follow one browser request through BFF and .NET into Tempo and associated logs, including failure and sampling behavior. [Proxy source](https://github.com/bc-solutions-coder/wallow/blob/a704634f/packages/sdk/src/server/proxy.ts)

## Revisiting the July rejection

The [July decision record](https://github.com/bc-solutions-coder/wallow/blob/a704634f/docs/plans/2026-07-31/2318-logging-telemetry-decision-record.md) explicitly permits revisiting Faro when linked distributed traces become a requirement. That requirement is now relevant. Its argument against exposing collector credentials remains useful, but rejects a particular direct transport rather than every Faro integration.

The blanket statement that traces bypass `beforeSend` is not supported by current upstream source: `pushTraces` calls `transports.execute`, and transport processing applies before-send hooks to items without excluding traces. This does **not** establish that a shallow context redactor sanitizes nested OTLP span attributes/events. The safe conclusion is to design and verify trace redaction for the chosen released SDK. Inspected upstream commit: `e96e684d86451bacddbd99916eed6fbf7d8d1700`; main is not a released-version guarantee. [Trace API](https://github.com/grafana/faro-web-sdk/blob/e96e684d86451bacddbd99916eed6fbf7d8d1700/packages/core/src/api/traces/initialize.ts), [Transport hooks](https://github.com/grafana/faro-web-sdk/blob/e96e684d86451bacddbd99916eed6fbf7d8d1700/packages/core/src/transports/initialize.ts)

Historical bundle estimates and router-wrapper behavior were not remeasured. Avoid carrying their exact numbers into a new package decision. The source record also marks the old endpoint and client-IP findings fixed; they should not be reopened solely from its historical diagnosis.

## Pangolin and Grafana identity

Pangolin SSO can send `Remote-User`, `Remote-Email`, `Remote-Name` and `Remote-Role`. Availability varies by authentication method; email OTP supplies only email. [Pangolin forwarded headers](https://docs.pangolin.net/manage/access-control/forwarded-headers)

A Pangolin resource gate controls reaching Grafana. Grafana still needs its own authentication configuration. OSS Grafana `auth.proxy` can consume a configured username/email header; it supports a trusted proxy IP allowlist to prevent spoofing. Its optional login-token setting controls issuing a Grafana session after proxy authentication. [Grafana auth proxy](https://grafana.com/docs/grafana/latest/setup-grafana/configure-access/configure-authentication/auth-proxy/)

**Deployment inference:** mapping `Remote-User` to Grafana's auth-proxy header is a viable candidate. Require identity-header replacement at the trusted edge and prevent direct access around it; an arbitrary trusted Docker subnet should not grant every app authority to impersonate the operator. Verify the actual Pangolin/Newt forwarding path and identity headers before selecting peer restrictions. Do not automatically translate Pangolin roles into Grafana administrative privileges.

Alternatively, Grafana Generic OAuth configures its own client against an identity provider, including authorization/token/user-info endpoints and a `/login/generic_oauth` callback. Using a common IdP may reuse a login, but is a separate integration from putting a Pangolin login page in front of Grafana. [Generic OAuth](https://grafana.com/docs/grafana/latest/setup-grafana/configure-access/configure-authentication/generic-oauth/)

## Remaining decisions and limits

Choose browser versus native device coverage, package API and publisher, trusted ingestion identity, permitted log fields, correlation schema, storage/retention budget, dashboard acceptance criteria and Grafana authentication mode in their decision tickets. No running deployment, throughput, delivery durability, SDK release compatibility, source-map pipeline or Pangolin end-to-end login was tested. No implementation or source tests were added.
