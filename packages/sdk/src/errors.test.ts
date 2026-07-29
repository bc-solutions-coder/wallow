/**
 * Contract for the shared error type reachable from the browser entry.
 *
 * `WallowError` used to be exported only from `./server`, which meant browser
 * code could not name the error it was catching. These specs pin three things:
 * the browser entry exposes `WallowError` and `isWallowError`; both entries
 * resolve the *same* class; and recognition works through a brand marker, so it
 * survives the module-graph duplication that silently breaks `instanceof`.
 */

import { describe, expect, it, vi } from "vitest";

import * as browserEntry from "./index";
import { isWallowError, WallowError } from "./index";
import { parseProblemDetails } from "./server/errors";
import * as serverEntry from "./server/index";

/** A representative error, matching what the proxy synthesizes for a 404. */
function makeError(): WallowError {
  return new WallowError({
    status: 404,
    code: "NOT_FOUND",
    title: "Not Found",
    detail: "No such organization.",
  });
}

describe("browser entry exports", () => {
  it("exports WallowError as a real Error subclass carrying the RFC 7807 members", () => {
    const error: WallowError = makeError();

    expect(error).toBeInstanceOf(Error);
    expect(error.name).toBe("WallowError");
    expect(error.status).toBe(404);
    expect(error.code).toBe("NOT_FOUND");
    expect(error.title).toBe("Not Found");
    expect(error.detail).toBe("No such organization.");
    expect(error.message).toBe("Not Found: No such organization.");
  });
});

describe("one shared definition across both entries", () => {
  it("resolves the browser and server entries to the same class object", () => {
    expect(serverEntry.WallowError).toBe(browserEntry.WallowError);
  });

  it("also exports isWallowError from the server entry", () => {
    expect(Object.keys(serverEntry)).toContain("isWallowError");
    expect(serverEntry.isWallowError).toBe(browserEntry.isWallowError);
  });

  it("recognizes an error produced by the server's parseProblemDetails", () => {
    const response: Response = new Response(null, { status: 403 });
    const body: string = JSON.stringify({
      title: "Forbidden",
      status: 403,
      extensions: { code: "FORBIDDEN" },
    });

    const error: unknown = parseProblemDetails(response, body);

    expect(isWallowError(error)).toBe(true);
  });
});

/**
 * Field-level validation errors (Wallow-ov6w.1.2).
 *
 * A 400 from the API is a `ValidationProblemDetails`: its RFC 7807 `errors`
 * member maps a property name to the messages FluentValidation produced for it.
 * That member is the only thing that can put a message under the field that
 * caused it, so it has to survive onto the error a form catches — a form that
 * only has `title` can do nothing better than a banner saying "one or more
 * validation errors occurred".
 */
describe("field errors", () => {
  it("carries RFC 7807 validation errors when provided", () => {
    const error: WallowError = new WallowError({
      status: 400,
      code: "VALIDATION_ERROR",
      title: "One or more validation errors occurred.",
      fieldErrors: { Name: ["'Name' must not be empty."], Email: ["Invalid email."] },
    });

    expect(error.fieldErrors).toEqual({
      Name: ["'Name' must not be empty."],
      Email: ["Invalid email."],
    });
  });

  it("leaves fieldErrors undefined when the problem details had none", () => {
    const error: WallowError = new WallowError({ status: 500, code: "UNKNOWN", title: "boom" });

    expect(error.fieldErrors).toBeUndefined();
  });

  it("survives the server's parseProblemDetails onto the shared error type", () => {
    const response: Response = new Response(null, { status: 400 });
    const body: string = JSON.stringify({
      title: "One or more validation errors occurred.",
      status: 400,
      errors: { Name: ["'Name' must not be empty."] },
      extensions: { code: "VALIDATION_ERROR" },
    });

    const error: WallowError = parseProblemDetails(response, body);

    expect(error.fieldErrors).toEqual({ Name: ["'Name' must not be empty."] });
  });
});

describe("isWallowError", () => {
  it("recognizes a WallowError instance", () => {
    expect(isWallowError(makeError())).toBe(true);
  });

  it("narrows unknown to WallowError", () => {
    const value: unknown = makeError();

    if (!isWallowError(value)) {
      expect.unreachable("isWallowError must recognize a WallowError instance");
    }

    // Reached only when narrowed; `value.status` compiles only if the predicate
    // narrowed `unknown` to `WallowError`.
    expect(value.status).toBe(404);
  });

  it.each([
    ["a plain Error", new Error("boom")],
    ["a TypeError", new TypeError("boom")],
    ["null", null],
    ["undefined", undefined],
    ["a string", "WallowError"],
    ["a number", 404],
    ["an empty object", {}],
    ["an unbranded look-alike", { status: 404, code: "NOT_FOUND", title: "Not Found" }],
  ])("rejects %s", (_label: string, value: unknown) => {
    expect(isWallowError(value)).toBe(false);
  });
});

describe("brand check survives what breaks instanceof", () => {
  it("recognizes an instance from a duplicated module graph", async () => {
    vi.resetModules();
    const duplicate: typeof browserEntry = await import("./index");

    // Guard: the duplication must be real, otherwise this spec proves nothing.
    expect(duplicate.WallowError).not.toBe(WallowError);

    const fromDuplicate: unknown = new duplicate.WallowError({
      status: 500,
      code: "UPSTREAM_FAILURE",
      title: "Upstream failure",
    });

    // instanceof is genuinely broken across the duplicate — this is the exact
    // failure mode the brand check exists to survive.
    expect(fromDuplicate instanceof WallowError).toBe(false);
    expect(isWallowError(fromDuplicate)).toBe(true);

    // ...and symmetrically, the duplicate's own predicate accepts ours.
    expect(duplicate.isWallowError(makeError())).toBe(true);
  });

  it("recognizes an instance whose prototype chain has been detached", () => {
    const error: WallowError = makeError();
    Object.setPrototypeOf(error, Error.prototype);

    // Prototype identity — everything `instanceof` consults — is gone.
    expect(error instanceof WallowError).toBe(false);
    expect(isWallowError(error)).toBe(true);
  });
});
