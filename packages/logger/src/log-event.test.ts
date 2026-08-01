import { describe, expect, it } from "vitest";

import {
  DEFAULT_INGEST_LIMITS,
  isAtLeast,
  isValidEventName,
  LOG_LEVELS,
  parseLogBatch,
  REDACTED,
  redactAttrs,
  type IngestLimits,
  type LogEvent,
} from "./log-event";

/**
 * The wire contract: level ordering, the event-name grammar, redaction, and the
 * batch validator the ingest route runs against unauthenticated input.
 *
 * `parseLogBatch` is total — it never throws — so every case here asserts the
 * returned reason rather than a rejection.
 */

function event(overrides: Partial<LogEvent> = {}): LogEvent {
  return {
    ts: "2026-07-31T12:00:00.000Z",
    level: "info",
    event: "form.submitted",
    attrs: {},
    ...overrides,
  };
}

describe("level ordering", () => {
  it("orders the levels ascending", () => {
    expect(LOG_LEVELS).toEqual(["debug", "info", "warn", "error"]);
  });

  it.each([
    ["debug", "info", false],
    ["info", "info", true],
    ["error", "debug", true],
    ["warn", "error", false],
  ] as const)("isAtLeast(%s, %s) is %s", (level, minimum, expected) => {
    expect(isAtLeast(level, minimum)).toBe(expected);
  });
});

describe("event names", () => {
  it.each(["form.submitted", "bff.logout.failed", "logger.dropped", "route_change", "a1.b2"])(
    "accepts %s",
    (name) => {
      expect(isValidEventName(name, DEFAULT_INGEST_LIMITS.maxEventNameLength)).toBe(true);
    },
  );

  it.each([
    "Form.Submitted",
    "the user submitted the form",
    ".leading",
    "trailing.",
    "double..dot",
    "1starts-with-digit",
  ])("rejects %s", (name) => {
    expect(isValidEventName(name, DEFAULT_INGEST_LIMITS.maxEventNameLength)).toBe(false);
  });

  it("rejects a name past the cap", () => {
    expect(isValidEventName("a".repeat(200), DEFAULT_INGEST_LIMITS.maxEventNameLength)).toBe(false);
  });
});

describe("redaction", () => {
  it("matches keys case-insensitively as a substring", () => {
    expect(redactAttrs({ newPassword: "hunter2", Authorization: "Bearer x", count: 3 })).toEqual({
      newPassword: REDACTED,
      Authorization: REDACTED,
      count: 3,
    });
  });

  it("walks into nested objects", () => {
    expect(redactAttrs({ user: { email: "a@b.dev", id: "u1" } })).toEqual({
      user: { email: REDACTED, id: "u1" },
    });
  });

  it("stops descending past the depth limit", () => {
    // A bag deep enough to hit the limit is a payload, not a log record; a cyclic
    // one would otherwise hang the request carrying it.
    const deep = { a: { b: { c: { d: { e: { password: "hunter2" } } } } } };

    expect(redactAttrs(deep)).toEqual(deep);
  });

  it("survives a cycle", () => {
    const cyclic: Record<string, unknown> = { name: "loop" };
    cyclic["self"] = cyclic;

    expect(() => redactAttrs(cyclic)).not.toThrow();
  });

  it("honours a caller's own key list", () => {
    expect(redactAttrs({ email: "a@b.dev", ssn: "1" }, ["ssn"])).toEqual({
      email: "a@b.dev",
      ssn: REDACTED,
    });
  });
});

describe("parseLogBatch", () => {
  it("accepts a well-formed batch", () => {
    const result = parseLogBatch({ events: [event()] });

    expect(result.ok).toBe(true);
  });

  it("defaults a missing attrs bag", () => {
    const result = parseLogBatch({ events: [{ ...event(), attrs: undefined }] });

    expect(result.ok && result.batch.events[0]?.attrs).toEqual({});
  });

  it("keeps a csrf token from the body", () => {
    // sendBeacon cannot set headers, so the terminal flush puts it here.
    const result = parseLogBatch({ events: [event()], csrfToken: "t0ken" });

    expect(result.ok && result.batch.csrfToken).toBe("t0ken");
  });

  it.each([
    ["a non-object body", "nope", "body is not an object"],
    ["no events array", {}, "body has no events array"],
    ["an empty batch", { events: [] }, "batch is empty"],
    ["a non-string csrf token", { events: [event()], csrfToken: 1 }, "csrfToken is not a string"],
  ])("rejects %s", (_name, body, reason) => {
    const result = parseLogBatch(body);

    expect(result.ok ? "" : result.reason).toBe(reason);
  });

  it.each([
    ["a bad level", { level: "trace" }, "event has no valid level"],
    [
      "prose in the event field",
      { event: "user did a thing" },
      "event name is not a dotted low-cardinality name",
    ],
    ["an unparseable ts", { ts: "yesterday" }, "event has no valid ts"],
    [
      "a non-object attrs",
      { attrs: [] as unknown as Record<string, unknown> },
      "event attrs is not an object",
    ],
  ])("rejects %s", (_name, overrides, reason) => {
    const result = parseLogBatch({ events: [{ ...event(), ...overrides }] });

    expect(result.ok ? "" : result.reason).toBe(reason);
  });

  it("rejects a batch past the event cap", () => {
    const events = Array.from({ length: DEFAULT_INGEST_LIMITS.maxEventsPerBatch + 1 }, () =>
      event(),
    );
    const result = parseLogBatch({ events });

    expect(result.ok ? "" : result.reason).toBe("batch holds too many events");
  });

  it("rejects an event past the attribute cap", () => {
    const attrs: Record<string, unknown> = {};
    for (let index = 0; index <= DEFAULT_INGEST_LIMITS.maxAttributesPerEvent; index += 1) {
      attrs[`k${String(index)}`] = index;
    }

    const result = parseLogBatch({ events: [event({ attrs })] });

    expect(result.ok ? "" : result.reason).toBe("event carries too many attributes");
  });

  it("enforces a caller's own limits", () => {
    const limits: IngestLimits = { ...DEFAULT_INGEST_LIMITS, maxEventsPerBatch: 1 };
    const result = parseLogBatch({ events: [event(), event()] }, limits);

    expect(result.ok ? "" : result.reason).toBe("batch holds too many events");
  });
});
