/**
 * The `x-request-id` correlation primitive (Wallow-pu6a.6.7).
 *
 * These are the rules the BFF proxy and the browser error path both build on,
 * so they are pinned here once rather than re-asserted at each call site: what
 * the header is called, what counts as a usable id, and what happens to one the
 * caller made up.
 */
import { describe, expect, it } from "vitest";

import * as browserEntry from "./index";
import {
  isValidRequestId,
  MAX_REQUEST_ID_LENGTH,
  newRequestId,
  REQUEST_ID_HEADER,
  resolveRequestId,
} from "./request-id";
import * as serverEntry from "./server/index";

/** Headers carrying `x-request-id: value`, the shape an inbound request has. */
function inbound(value: string): Headers {
  return new Headers({ [REQUEST_ID_HEADER]: value });
}

describe("REQUEST_ID_HEADER", () => {
  it("is the conventional x-request-id spelling", () => {
    // Lowercase because `Headers` normalizes to it, and the proxy compares the
    // name it writes against the name it reads.
    expect(REQUEST_ID_HEADER).toBe("x-request-id");
  });

  it("is reachable from both public entry points", () => {
    // The BFF writes the header and the browser reads it off a response; a
    // consumer wiring either side must not have to hardcode the string.
    expect(browserEntry.REQUEST_ID_HEADER).toBe(REQUEST_ID_HEADER);
    expect(serverEntry.REQUEST_ID_HEADER).toBe(REQUEST_ID_HEADER);
  });
});

describe("isValidRequestId", () => {
  it("accepts a UUID, the shape newRequestId emits", () => {
    expect(isValidRequestId("3f2504e0-4f89-11d3-9a0c-0305e82c3301")).toBe(true);
  });

  it("accepts the other id shapes upstream callers actually send", () => {
    // A W3C traceparent, a hex span id, and an opaque gateway id: every one of
    // these arrives at a real BFF, and rewriting them would break the caller's
    // own correlation.
    expect(isValidRequestId("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01")).toBe(true);
    expect(isValidRequestId("00f067aa0ba902b7")).toBe(true);
    expect(isValidRequestId("req_01HQ8Z.4K9")).toBe(true);
  });

  it("rejects an empty id", () => {
    expect(isValidRequestId("")).toBe(false);
  });

  it("rejects an id longer than MAX_REQUEST_ID_LENGTH", () => {
    expect(isValidRequestId("a".repeat(MAX_REQUEST_ID_LENGTH))).toBe(true);
    expect(isValidRequestId("a".repeat(MAX_REQUEST_ID_LENGTH + 1))).toBe(false);
  });

  it("rejects CR and LF, which would forge a second header or log record", () => {
    expect(isValidRequestId("abc\r\nx-admin: 1")).toBe(false);
    expect(isValidRequestId("abc\ndef")).toBe(false);
    expect(isValidRequestId("abc\rdef")).toBe(false);
  });

  it("rejects whitespace and control characters", () => {
    expect(isValidRequestId("abc def")).toBe(false);
    expect(isValidRequestId(" abc")).toBe(false);
    expect(isValidRequestId("abc\tdef")).toBe(false);
    expect(isValidRequestId("abc\u0000def")).toBe(false);
  });

  it("rejects markup and quoting characters that have no place in a correlation key", () => {
    expect(isValidRequestId("<script>")).toBe(false);
    expect(isValidRequestId('abc"def')).toBe(false);
    expect(isValidRequestId("abc;def")).toBe(false);
  });
});

describe("newRequestId", () => {
  it("emits an id that satisfies isValidRequestId", () => {
    expect(isValidRequestId(newRequestId())).toBe(true);
  });

  it("emits a distinct id on every call", () => {
    const ids: Set<string> = new Set<string>();
    for (let index: number = 0; index < 100; index += 1) {
      ids.add(newRequestId());
    }

    expect(ids.size).toBe(100);
  });
});

describe("resolveRequestId", () => {
  it("keeps the caller's id when it is usable", () => {
    // The caller already logged this id against its own request; replacing it
    // would sever the correlation the header exists to carry.
    expect(resolveRequestId(inbound("3f2504e0-4f89-11d3-9a0c-0305e82c3301"))).toBe(
      "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
    );
  });

  it("reads the header case-insensitively, as Headers delivers it", () => {
    expect(resolveRequestId(new Headers({ "X-Request-Id": "upstream-id-1" }))).toBe(
      "upstream-id-1",
    );
  });

  it("generates an id when the request carries none", () => {
    const resolved: string = resolveRequestId(new Headers());

    expect(isValidRequestId(resolved)).toBe(true);
  });

  it("generates a fresh id per request rather than reusing one", () => {
    expect(resolveRequestId(new Headers())).not.toBe(resolveRequestId(new Headers()));
  });

  it("replaces an unusable caller id instead of echoing it", () => {
    // A forged id must never reach an outbound header or a log line, and the
    // request still has to be correlatable, so it gets a generated id.
    for (const forged of ["", "a".repeat(MAX_REQUEST_ID_LENGTH + 1), "<script>"]) {
      const resolved: string = resolveRequestId(inbound(forged));

      expect(resolved).not.toBe(forged);
      expect(isValidRequestId(resolved)).toBe(true);
    }
  });
});
