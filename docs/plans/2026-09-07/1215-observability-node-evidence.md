**status: completed**

# Installable Node telemetry verification for #247

The package bundles `@bc-solutions-coder/logger/server` into built ESM and emits standalone
declarations with explicit relative `.js` imports. An isolated consumer installs the tarball
without workspace resolution and typechecks using NodeNext module resolution.

`./scripts/check-telemetry-consumer.sh` ran on the independent `wallow-245-proof_storage`
network. The consumer runtime was Node v24.20.0. It provisioned through the private gateway
control API, initialized the packed server entry, and queried actual logs, correlated trace,
duration metric and fatal-error log from Loki, Tempo and Prometheus. Credentials travelled
only through private environment/stdin paths; printed evidence contains no secrets.

Latest successful packed proof:

```json
{
  "node": "v24.20.0",
  "registration": "6b947c08-03c4-4036-be7e-419ed28df20e",
  "traceId": "9f6820fb11d58858a7256c9511a23c75",
  "packed": true,
  "correlated": true,
  "sanitized": true,
  "crashExitCode": 1
}
```

The fatal child uses an unhandled rejected `telemetry.trace`, exercising the case where
an error was queued before becoming fatal. The monitor checks confirmed export delivery;
queued records do not suppress its final attempt. Queries found exactly the expected caught
and fatal errors, without seeded nested-email, query, exception-message or credential values.

Six focused tests exercise public APIs and actual local HTTP servers: correlated sanitized
failure export; overload and a rejecting collector; handled-versus-unexpected deduplication;
returned HTTP 500 span status and IPv6 removal; redirects across owned/unowned origins; and streamed uploads with bounded redirect replay.
The initial overflow test exposed costly regex work, corrected with bounded string work and
an aggregate recursion budget. The HTTP outage test leaves application responses successful
and checks byte limits and loss counters. The first public API test failed before implementation.

The package uses explicit Request-handler wrapping and an owned-origin fetch method; it does
not patch global HTTP APIs. Browser capture, Wallow registration, registry publication and
Debian deployment are separate remaining slices. This is packed-artifact proof, not a claim
that a published registry version has been verified.

`pnpm check` passed after the final changes. Independent standards and spec reviewers
confirmed their findings resolved. The backend remains covered by the preceding 5,610-test
full integration run; this slice changes no backend code.
