#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
consumer_dir="$(mktemp -d)"
trap 'rm -rf "$consumer_dir"' EXIT
cd "$repo_root"
if [[ -n "${CI_PACKAGE_DIR:-}" ]]; then
  archive="$CI_PACKAGE_DIR/telemetry.tgz"
else
  pnpm exec turbo run build --filter=@bc-solutions-coder/telemetry...
  mkdir "$consumer_dir/archives"
  archive="$(pnpm --dir packages/telemetry pack --pack-destination "$consumer_dir/archives" | tail -n 1)"
fi
compiler_version="$(node -p "require('./packages/telemetry/node_modules/typescript/package.json').version")"
cd "$consumer_dir"
printf '%s\n' '{"private":true,"type":"module"}' > package.json
npm install --ignore-scripts --no-audit --no-fund --registry=https://registry.npmjs.org "$archive" "typescript@$compiler_version" @types/node@24
cat > consumer.ts <<'TS'
import { initializeBrowserTelemetry } from '@bc-solutions-coder/telemetry';
import { initializeTelemetry, createBrowserRelay } from '@bc-solutions-coder/telemetry/server';
export const browser = () => initializeBrowserTelemetry({ relayPath: '/telemetry' });
export const server = () => initializeTelemetry({ endpoint: 'http://localhost:1', credential: 'test.credential' });
export const relay = () => createBrowserRelay({ endpoint: 'http://localhost:1', credential: 'test.credential', origin: 'http://localhost', resolveSession: async () => undefined });
TS
./node_modules/.bin/tsc --noEmit --module NodeNext --moduleResolution NodeNext --target ES2022 --skipLibCheck false consumer.ts
node --input-type=module <<'JS'
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { initializeTelemetry } from '@bc-solutions-coder/telemetry/server';
const manifest = JSON.parse(await readFile('node_modules/@bc-solutions-coder/telemetry/package.json', 'utf8'));
assert.ok(!JSON.stringify(manifest.dependencies ?? {}).match(/workspace:|catalog:/));
const records = [];
const server = createServer(async (req, res) => {
  const chunks=[]; for await (const chunk of req) chunks.push(chunk);
  records.push(JSON.parse(Buffer.concat(chunks).toString())); res.writeHead(200).end('{}');
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const telemetry = initializeTelemetry({ endpoint: `http://127.0.0.1:${server.address().port}`, credential: 'test.credential' });
try {
  telemetry.logger.info('artifact.verified', { email: 'PRIVATE@example.com' });
  await telemetry.flush();
  assert.ok(JSON.stringify(records).includes('artifact.verified'));
  assert.ok(!JSON.stringify(records).includes('PRIVATE'));
  console.log(JSON.stringify({ version: manifest.version, isolatedTarball: true, declarations: true, nodeRuntime: true }));
} finally { await telemetry.shutdown(); server.close(); }
JS
