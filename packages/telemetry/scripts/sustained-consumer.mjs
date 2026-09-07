import assert from "node:assert/strict";
import { execFile } from "node:child_process";
import { setTimeout as delay } from "node:timers/promises";
import { promisify } from "node:util";
import { chromium } from "playwright";

const run = promisify(execFile);
const REQUESTS = 240;
const INTERVAL_MS = 250;
const SAMPLE_MS = 1000;
const origin = process.env.PUBLIC_ORIGIN;
const grafana = process.env.GRAFANA_ORIGIN;
const project = process.env.OBSERVABILITY_PROOF_PROJECT ?? "wallow-245-proof";
const services = ["gateway", "alloy", "loki", "tempo", "prometheus", "grafana", "garage"];
const peaks = {};
const browser = await chromium.launch();
let active = true;
let queries = 0;
const query = async (expression) => {
  const response = await fetch(
    `${grafana}/api/datasources/proxy/uid/prometheus/api/v1/query?query=${encodeURIComponent(expression)}`,
  );
  assert.ok(response.ok);
  return response.json();
};
const started = performance.now();
const before = await query("sum(tempodb_compaction_blocks_total)");
const sample = async () => {
  if (!active) {
    return;
  }
  const result = await run("docker", [
    "stats",
    "--no-stream",
    "--format",
    "{{.Name}} {{.MemUsage}}",
    ...services.map((service) => `${project}-${service}-1`),
  ]);
  for (const line of result.stdout.trim().split("\n")) {
    const [name, usage] = line.split(" ");
    const match = /^(?<value>[\d.]+)(?<unit>MiB|GiB|KiB)$/u.exec(usage);
    if (match) {
      const multipliers = { KiB: 1024, MiB: 1_048_576, GiB: 1_073_741_824 };
      const bytes = Number(match.groups.value) * multipliers[match.groups.unit];
      peaks[name] = Math.max(peaks[name] ?? Number.MIN_VALUE, bytes);
    }
  }
  const flush = await fetch(`${grafana}/api/datasources/proxy/uid/tempo/flush`, { method: "POST" });
  assert.ok(flush.ok);
  await query("up");
  queries++;
  await delay(SAMPLE_MS);
  return sample();
};
const samples = sample();
try {
  const page = await browser.newPage();
  await page.goto(origin);
  await page.waitForFunction(() => globalThis.consumerReady);
  await page.evaluate(
    async ({ count, interval }) => {
      const FAILURE = 500;
      const BATCH = 5;
      const FIRST = 0;
      const STEP = 1;
      const send = async (index) => {
        if (index >= count) {
          return;
        }
        const response = await globalThis.telemetry.fetch("/operate", { redirect: "error" });
        if (response.status !== FAILURE) {
          throw new Error("Application operation changed during telemetry load");
        }
        if (index % BATCH === FIRST) {
          await globalThis.telemetry.flush();
        }
        await new Promise((resolve) => {
          setTimeout(resolve, interval);
        });
        return send(index + STEP);
      };
      await send(FIRST);
      await globalThis.telemetry.flush();
    },
    { count: REQUESTS, interval: INTERVAL_MS },
  );
  const response = await fetch(`${origin}/proof`);
  const proof = await response.json();
  const flush = await fetch(`${grafana}/api/datasources/proxy/uid/tempo/flush`, { method: "POST" });
  assert.ok(flush.ok);
  const after = await query("sum(tempodb_compaction_blocks_total)");
  const elapsedMs = Math.round(performance.now() - started);
  process.stdout.write(
    `${JSON.stringify({ requests: REQUESTS, elapsedMs, forcedBlockFlushes: true, intervalMs: INTERVAL_MS, fullTracing: true, concurrentQueries: queries, sampledPeakBytes: peaks, compactionBefore: before.data.result, compactionAfter: after.data.result, relay: proof.relay })}\n`,
  );
} finally {
  active = false;
  await samples;
  await browser.close();
}
