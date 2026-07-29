/**
 * The request-correlation runbook is a deliverable, not a nicety
 * (Wallow-pu6a.6.7).
 *
 * `requestId` and `traceId` on a `WallowError` are only worth carrying if
 * someone knows what to do with them, and that knowledge has no compile-time
 * home — so the workflow (frontend error -> id -> backend OTel trace in Grafana)
 * is pinned here as a documentation contract, the same way
 * `bff-pattern-docs.test.ts` pins the session-store guidance.
 */
import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { REQUEST_ID_HEADER } from "./request-id";

// packages/sdk/src -> repo root
const repoRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
const docPath: string = resolve(repoRoot, "docs/operations/request-correlation.md");
const tocPath: string = resolve(repoRoot, "docs/toc.yml");

function readDoc(): string {
  return readFileSync(docPath, "utf8");
}

describe("docs/operations/request-correlation.md", () => {
  it("exists", () => {
    expect(existsSync(docPath)).toBe(true);
  });

  it("is reachable from the docs site navigation", () => {
    // A page missing from toc.yml is a page nobody on call will find.
    expect(readFileSync(tocPath, "utf8")).toContain("operations/request-correlation.md");
  });

  it("names the correlation header the SDK actually sends", () => {
    expect(readDoc()).toContain(REQUEST_ID_HEADER);
  });

  it("names both members a caught WallowError exposes", () => {
    const doc: string = readDoc();

    expect(doc).toContain("WallowError");
    expect(doc).toContain("requestId");
    expect(doc).toContain("traceId");
  });

  it("states that the BFF generates a request id when the caller sends none", () => {
    expect(readDoc()).toMatch(/generat/iu);
  });

  it("walks the reader to a trace in the Grafana stack", () => {
    const doc: string = readDoc();

    // The otel-lgtm stack this repo runs: Grafana is the UI, Tempo holds traces.
    expect(doc).toMatch(/grafana/iu);
    expect(doc).toMatch(/tempo|trace/iu);
    // The local Grafana the compose stack publishes, so the reader has somewhere
    // to click rather than a vendor-neutral description of a search box.
    expect(doc).toContain("localhost:3001");
  });
});
