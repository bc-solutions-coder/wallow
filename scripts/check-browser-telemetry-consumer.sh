#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
consumer_dir="$(mktemp -d)"
container="wallow-browser-proof-$$"
cleanup() {
  if [[ -f "$consumer_dir/registration.log" ]]; then cp "$consumer_dir/registration.log" /tmp/wallow-proof-registration-last.log; chmod 600 /tmp/wallow-proof-registration-last.log; fi
  (cd "$repo_root/docker" && docker logs "$container-control" > /tmp/wallow-proof-control-last.log 2>&1) || true
  touch "$consumer_dir/stop"
  if [[ -n "${proof_pid:-}" ]]; then wait "$proof_pid" || true; fi
  (cd "$repo_root/docker" && docker rm -f "$container" "$container-control" "$container-grafana" >/dev/null 2>&1) || true
  rm -rf "$consumer_dir"
}
trap cleanup EXIT
cd "$repo_root"
pnpm exec turbo run build --filter=@bc-solutions-coder/telemetry...
mkdir "$consumer_dir/archives"
archive="$(pnpm --dir packages/telemetry pack --pack-destination "$consumer_dir/archives" | tail -n 1)"
cp scripts/browser-telemetry-consumer.mjs "$consumer_dir/consumer.mjs"
printf '%s\n' '{"private":true,"type":"module"}' > "$consumer_dir/package.json"
cd "$consumer_dir"
npm install --ignore-scripts --no-audit --no-fund --registry=https://registry.npmjs.org "$archive"
ports="$(python3 - <<'PY'
import socket
with socket.socket() as a, socket.socket() as b, socket.socket() as c, socket.socket() as d, socket.socket() as e:
    a.bind(('127.0.0.1', 0)); b.bind(('127.0.0.1', 0)); c.bind(('127.0.0.1', 0)); d.bind(('127.0.0.1', 0)); e.bind(('127.0.0.1', 0))
    print(a.getsockname()[1], b.getsockname()[1], c.getsockname()[1], d.getsockname()[1], e.getsockname()[1])
PY
)"
read -r app_port other_port control_port api_port grafana_port <<< "$ports"
export PUBLIC_ORIGIN="http://127.0.0.1:$app_port"
export GRAFANA_ORIGIN="http://127.0.0.1:$grafana_port"
export OTHER_ORIGIN="http://127.0.0.1:$other_port"
cd "$repo_root/docker"
docker create --name "$container-control" --network bridge -p "127.0.0.1:$control_port:8080" \
  node:24-alpine node --input-type=module -e '
  import { createServer } from "node:http";
  createServer(async (incoming, outgoing) => {
    try {
      const chunks = []; for await (const chunk of incoming) chunks.push(chunk);
      const response = await fetch(new URL(incoming.url, incoming.url.startsWith("/v1/") ? "http://gateway:8080" : "http://gateway:8081"), {
        method: incoming.method, headers: { authorization: incoming.headers.authorization, "content-type": "application/json", "x-wallow-environment": incoming.headers["x-wallow-environment"] ?? "test", "x-wallow-release": incoming.headers["x-wallow-release"] ?? "unknown" }, body: ["GET", "HEAD"].includes(incoming.method) ? undefined : Buffer.concat(chunks), signal: AbortSignal.timeout(5000), redirect: "error"
      });
      const reply = Buffer.from(await response.arrayBuffer());
      process.stderr.write(`control response ${incoming.url} ${response.status} ${response.status >= 400 ? reply.toString().slice(0, 600) : ""}\n`);
      outgoing.writeHead(response.status, { "content-type": "application/json" }); outgoing.end(reply);
    } catch (error) { process.stderr.write(`${error.name}: ${error.message}\n`); outgoing.writeHead(503).end(); }
  }).listen(8080, "0.0.0.0");' >/dev/null
docker network connect "${OBSERVABILITY_PROOF_NETWORK:-wallow-245-proof_storage}" "$container-control"
docker start "$container-control" >/dev/null
cd "$repo_root"
PROOF_CONTROL_URL="http://127.0.0.1:$control_port" PROOF_CONFIGURATION="$consumer_dir/configuration.json" \
  PROOF_API_PORT="$api_port" PROOF_STOP_FILE="$consumer_dir/stop" \
  PROOF_MANAGEMENT_FILE="$repo_root/docker/observability/gateway.local" \
  dotnet run --project api/tools/Wallow.ObservabilityProof/Wallow.ObservabilityProof.csproj > "$consumer_dir/registration.log" 2>&1 &
proof_pid=$!
for attempt in {1..120}; do
  if [[ -s "$consumer_dir/configuration.json" ]]; then break; fi
  if ! kill -0 "$proof_pid" 2>/dev/null; then tail -n 35 "$consumer_dir/registration.log"; exit 1; fi
  sleep 1
done
if [[ ! -s "$consumer_dir/configuration.json" ]]; then tail -n 35 "$consumer_dir/registration.log"; exit 1; fi
cd "$repo_root/docker"
docker create --name "$container" \
  -e PUBLIC_ORIGIN -e OTHER_ORIGIN \
  --network bridge \
  -p "127.0.0.1:$app_port:8080" -p "127.0.0.1:$other_port:8081" \
  --mount "type=bind,source=$consumer_dir,target=/consumer,readonly" \
  --workdir /consumer node:24-alpine node consumer.mjs >/dev/null
docker network connect "${OBSERVABILITY_PROOF_NETWORK:-wallow-245-proof_storage}" "$container"
docker start "$container" >/dev/null
for attempt in {1..30}; do
  if curl -fsS "$PUBLIC_ORIGIN" >/dev/null 2>&1; then break; fi
  sleep 1
done
if ! curl -fsS "$PUBLIC_ORIGIN" >/dev/null; then
  docker logs "$container"
  exit 1
fi
docker create --name "$container-grafana" --network bridge -p "127.0.0.1:$grafana_port:8080" \
  --env-file observability/grafana.local node:24-alpine node --input-type=module -e '
  import { createServer } from "node:http";
  const authorization = "Basic " + Buffer.from(process.env.GF_SECURITY_ADMIN_USER + ":" + process.env.GF_SECURITY_ADMIN_PASSWORD).toString("base64");
  createServer(async (incoming, outgoing) => {
    try {
      const chunks = []; for await (const chunk of incoming) chunks.push(chunk);
      const response = await fetch(new URL(incoming.url, "http://grafana:3000"), {
        method: incoming.method, headers: { authorization, "content-type": incoming.headers["content-type"] ?? "application/json" },
        body: ["GET", "HEAD"].includes(incoming.method) ? undefined : Buffer.concat(chunks), redirect: "manual"
      });
      outgoing.writeHead(response.status, { "content-type": response.headers.get("content-type") ?? "text/plain" });
      outgoing.end(Buffer.from(await response.arrayBuffer()));
    } catch { outgoing.writeHead(503).end(); }
  }).listen(8080, "0.0.0.0");' >/dev/null
docker network connect "${OBSERVABILITY_PROOF_NETWORK:-wallow-245-proof_storage}" "$container-grafana"
docker start "$container-grafana" >/dev/null
cd "$repo_root"
node packages/telemetry/scripts/browser-consumer.mjs

if [[ "${PROOF_STRESS:-0}" == "1" ]]; then
  node packages/telemetry/scripts/sustained-consumer.mjs
fi
