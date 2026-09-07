#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
consumer_dir="$(mktemp -d)"
container="wallow-browser-proof-$$"
cleanup() {
  (cd "$repo_root/docker" && docker rm -f "$container" "$container-control" >/dev/null 2>&1) || true
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
with socket.socket() as a, socket.socket() as b:
    a.bind(('127.0.0.1', 0)); b.bind(('127.0.0.1', 0))
    print(a.getsockname()[1], b.getsockname()[1])
PY
)"
read -r app_port other_port <<< "$ports"
export PUBLIC_ORIGIN="http://127.0.0.1:$app_port"
export OTHER_ORIGIN="http://127.0.0.1:$other_port"
cd "$repo_root/docker"
docker create --name "$container-control" --network bridge -p "127.0.0.1:$app_port:8080" \
  node:24-alpine node --input-type=module -e '
  import { createServer } from "node:http";
  createServer(async (incoming, outgoing) => {
    try {
      const chunks = []; for await (const chunk of incoming) chunks.push(chunk);
      const response = await fetch(new URL(incoming.url, "http://gateway:8081"), {
        method: incoming.method, headers: { authorization: incoming.headers.authorization, "content-type": "application/json" }, body: Buffer.concat(chunks), signal: AbortSignal.timeout(5000), redirect: "error"
      });
      process.stderr.write(`control response ${response.status}\n`);
      outgoing.writeHead(response.status, { "content-type": "application/json" }); outgoing.end(Buffer.from(await response.arrayBuffer()));
    } catch (error) { process.stderr.write(`${error.name}: ${error.message}\n`); outgoing.writeHead(503).end(); }
  }).listen(8080, "0.0.0.0");' >/dev/null
docker network connect "${OBSERVABILITY_PROOF_NETWORK:-wallow-245-proof_storage}" "$container-control"
docker start "$container-control" >/dev/null
cd "$repo_root"
PROOF_CONTROL_URL="$PUBLIC_ORIGIN" PROOF_CONFIGURATION="$consumer_dir/configuration.json" \
  PROOF_MANAGEMENT_FILE="$repo_root/docker/observability/gateway.local" \
  dotnet run --project api/tools/Wallow.ObservabilityProof/Wallow.ObservabilityProof.csproj > "$consumer_dir/registration.log" 2>&1 || {
    tail -n 35 "$consumer_dir/registration.log"; (cd "$repo_root/docker" && docker logs "$container-control"); exit 1;
  }
cd "$repo_root/docker"
docker rm -f "$container-control" >/dev/null
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
cd "$repo_root"
node packages/telemetry/scripts/browser-consumer.mjs
