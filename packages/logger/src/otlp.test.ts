import { describe, expect, it, vi } from "vitest";

import { emitOtlp, otlpLogsUrl, toOtlpLogsPayload, type ServerLogRecord } from "./otlp";

/**
 * The OTLP/JSON encoding and the POST that ships it.
 *
 * Field names here are the collector's contract, not this package's: a renamed
 * key does not fail a build, it makes a Grafana query return nothing.
 */

const NOW_MS = 1_767_182_400_000;

function record(overrides: Partial<ServerLogRecord> = {}): ServerLogRecord {
  return {
    ts: "2026-07-31T12:00:01.000Z",
    clientTs: "2026-07-31T12:00:00.000Z",
    level: "info",
    event: "form.submitted",
    attrs: {},
    service: "wallow-web",
    ...overrides,
  };
}

function firstRecord(payload: ReturnType<typeof toOtlpLogsPayload>) {
  return payload.resourceLogs[0]!.scopeLogs[0]!.logRecords[0]!;
}

function attributeMap(attributes: readonly { key: string; value: unknown }[]) {
  return Object.fromEntries(attributes.map((entry) => [entry.key, entry.value]));
}

describe("toOtlpLogsPayload", () => {
  it("names the service as a resource attribute", () => {
    const payload = toOtlpLogsPayload([record()], NOW_MS);

    expect(payload.resourceLogs[0]?.resource.attributes).toEqual([
      { key: "service.name", value: { stringValue: "wallow-web" } },
    ]);
  });

  it("groups records by service", () => {
    // A resource carries ONE service name, so a mixed batch under a single
    // resource would mislabel every record but the first.
    const payload = toOtlpLogsPayload(
      [record(), record({ service: "wallow-auth" }), record()],
      NOW_MS,
    );

    expect(payload.resourceLogs.map((entry) => entry.resource.attributes[0]?.value)).toEqual([
      { stringValue: "wallow-web" },
      { stringValue: "wallow-auth" },
    ]);
    expect(payload.resourceLogs[0]?.scopeLogs[0]?.logRecords).toHaveLength(2);
  });

  it.each([
    ["debug", 5],
    ["info", 9],
    ["warn", 13],
    ["error", 17],
  ] as const)("maps %s to severity %i", (level, severityNumber) => {
    const encoded = firstRecord(toOtlpLogsPayload([record({ level })], NOW_MS));

    expect(encoded.severityNumber).toBe(severityNumber);
    expect(encoded.severityText).toBe(level.toUpperCase());
  });

  it("carries the event name as the body", () => {
    const payload = toOtlpLogsPayload([record()], NOW_MS);

    expect(firstRecord(payload).body).toEqual({ stringValue: "form.submitted" });
  });

  it("keeps the client and server times apart", () => {
    const payload = firstRecord(toOtlpLogsPayload([record()], NOW_MS));

    expect(payload.timeUnixNano).toBe("1785499200000000000");
    expect(payload.observedTimeUnixNano).toBe("1785499201000000000");
  });

  it("falls back to the passed clock for an unparseable time", () => {
    const encoded = toOtlpLogsPayload([record({ clientTs: "yesterday" })], NOW_MS);

    expect(firstRecord(encoded).timeUnixNano).toBe(String(BigInt(NOW_MS) * 1_000_000n));
  });

  it("promotes the server-stamped fields to semantic attributes", () => {
    const payload = firstRecord(
      toOtlpLogsPayload(
        [
          record({
            correlationId: "req-1",
            clientIp: "203.0.113.7",
            userId: "u1",
            tenantId: "t1",
            error: { name: "TypeError", message: "boom", stack: "at x" },
          }),
        ],
        NOW_MS,
      ),
    );

    expect(attributeMap(payload.attributes)).toMatchObject({
      "event.name": { stringValue: "form.submitted" },
      "wallow.correlation_id": { stringValue: "req-1" },
      "client.address": { stringValue: "203.0.113.7" },
      "enduser.id": { stringValue: "u1" },
      "wallow.tenant_id": { stringValue: "t1" },
      "exception.type": { stringValue: "TypeError" },
      "exception.message": { stringValue: "boom" },
      "exception.stacktrace": { stringValue: "at x" },
    });
  });

  it("omits the fields that were never stamped", () => {
    const payload = firstRecord(toOtlpLogsPayload([record()], NOW_MS));

    expect(Object.keys(attributeMap(payload.attributes))).not.toContain("enduser.id");
  });

  it.each([
    ["a string", "x", { stringValue: "x" }],
    ["a boolean", true, { boolValue: true }],
    ["an integer", 3, { intValue: "3" }],
    ["a float", 1.5, { doubleValue: 1.5 }],
    ["an object", { a: 1 }, { stringValue: '{"a":1}' }],
    ["null", null, { stringValue: "" }],
  ])("encodes %s attribute", (_name, value, expected) => {
    const encoded = toOtlpLogsPayload([record({ attrs: { field: value } })], NOW_MS);

    expect(attributeMap(firstRecord(encoded).attributes)["field"]).toEqual(expected);
  });

  it("keeps a cyclic attribute from costing the batch", () => {
    const cyclic: Record<string, unknown> = {};
    cyclic["self"] = cyclic;
    const encoded = toOtlpLogsPayload([record({ attrs: { cyclic } })], NOW_MS);

    expect(attributeMap(firstRecord(encoded).attributes)["cyclic"]).toEqual({
      stringValue: "[unserializable]",
    });
  });
});

describe("otlpLogsUrl", () => {
  it.each([
    ["http://localhost:4318", "http://localhost:4318/v1/logs"],
    ["http://localhost:4318/", "http://localhost:4318/v1/logs"],
    ["http://localhost:4318/v1/logs", "http://localhost:4318/v1/logs"],
  ])("resolves %s", (endpoint, expected) => {
    // OTEL_EXPORTER_OTLP_ENDPOINT is a base and OTEL_EXPORTER_OTLP_LOGS_ENDPOINT
    // is the full URL; a caller may hand in either.
    expect(otlpLogsUrl(endpoint)).toBe(expected);
  });
});

describe("emitOtlp", () => {
  it("posts JSON to the logs path", async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 200 }));

    const result = await emitOtlp("http://collector:4318", [record()], NOW_MS, fetchImpl);

    expect(result).toEqual({ ok: true, status: 200 });
    expect(fetchImpl).toHaveBeenCalledWith(
      "http://collector:4318/v1/logs",
      expect.objectContaining({ method: "POST", headers: { "content-type": "application/json" } }),
    );
  });

  it("sends nothing for an empty batch", async () => {
    const fetchImpl = vi.fn<typeof fetch>();

    await expect(emitOtlp("http://collector:4318", [], NOW_MS, fetchImpl)).resolves.toEqual({
      ok: true,
    });
    expect(fetchImpl).not.toHaveBeenCalled();
  });

  it("reports a rejected transport instead of throwing", async () => {
    const cause = new Error("ECONNREFUSED");
    const fetchImpl = vi.fn<typeof fetch>().mockRejectedValue(cause);

    await expect(emitOtlp("http://collector:4318", [record()], NOW_MS, fetchImpl)).resolves.toEqual(
      {
        ok: false,
        error: cause,
      },
    );
  });

  it("reports a refusing collector", async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 503 }));

    await expect(emitOtlp("http://collector:4318", [record()], NOW_MS, fetchImpl)).resolves.toEqual(
      {
        ok: false,
        status: 503,
      },
    );
  });
});
