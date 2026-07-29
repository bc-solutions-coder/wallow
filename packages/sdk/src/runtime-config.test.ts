import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import openApiConfig from "../openapi-ts.config";
import { createWallowSdk, type WallowSdk } from "./create-sdk";
import { isWallowError, WallowError } from "./errors";
import { client as generatedClient } from "./generated/client.gen";
import { usersGetCurrentUser } from "./generated";
import { REQUEST_ID_HEADER } from "./request-id";
import {
  createClientConfig,
  toWallowError,
  type WallowErrorInterceptorClient,
  wireWallowErrorInterceptor,
} from "./runtime-config";
import { UNKNOWN_ERROR_CODE } from "./server/errors";
import { NETWORK_ERROR_CODE } from "./server/proxy";

describe("createClientConfig", () => {
  it("defaults the client to the same-origin BFF path with credentials included", () => {
    const config = createClientConfig();

    expect(config.baseUrl).toBe("/api");
    expect(config.credentials).toBe("include");
  });

  it("overrides the generated baseUrl from the OpenAPI document", () => {
    const config = createClientConfig({ baseUrl: "http://localhost:5001/" });

    expect(config.baseUrl).toBe("/api");
    expect(config.credentials).toBe("include");
  });

  it("preserves other options passed by the generated client", () => {
    const config = createClientConfig({
      baseUrl: "http://localhost:5001/",
      throwOnError: true,
    });

    expect(config.throwOnError).toBe(true);
  });
});

describe("openapi-ts.config", () => {
  it("wires runtimeConfigPath into the client-fetch plugin", async () => {
    const config = await openApiConfig;
    const plugins = config.plugins ?? [];

    const clientPlugin = plugins.find(
      (plugin) => typeof plugin === "object" && plugin.name === "@hey-api/client-fetch",
    );

    expect(clientPlugin).toEqual(
      expect.objectContaining({
        name: "@hey-api/client-fetch",
        runtimeConfigPath: "./src/runtime-config",
      }),
    );
  });
});

describe("generated client", () => {
  it("is constructed through createClientConfig", () => {
    const source = readFileSync(
      fileURLToPath(new URL("./generated/client.gen.ts", import.meta.url)),
      "utf8",
    );

    expect(source).toMatch(/from ['"]\.\.\/runtime-config['"]/);
    expect(source).toMatch(/createClient\(\s*createClientConfig\(/);
  });

  it("starts out pointed at the BFF path with credentials included", () => {
    const config = generatedClient.getConfig();

    expect(config.baseUrl).toBe("/api");
    expect(config.credentials).toBe("include");
  });
});

/**
 * The unified error contract (Wallow-pu6a.5.3).
 *
 * With `throwOnError: true` + `responseStyle: "data"` every generated operation
 * rejects with the PARSED response body, whose shape differs per endpoint family
 * (RFC 7807 problem details for most, a bare `{ succeeded, error }` object for
 * the Identity auth and MFA controllers, nothing at all for an empty 401). The
 * error interceptor is the single place that difference is erased, and the ONLY
 * place the transport status is still reachable.
 */

/** The interceptor shape the generated client hands its error middleware. */
type ErrorInterceptor = (error: unknown, response: Response | undefined) => unknown;

/** A client shell that records what was registered on its error interceptors. */
function recordingClient(): {
  client: WallowErrorInterceptorClient;
  registered: ErrorInterceptor[];
} {
  const registered: ErrorInterceptor[] = [];

  return {
    registered,
    client: {
      interceptors: {
        error: {
          use: (interceptor: ErrorInterceptor): unknown => registered.push(interceptor),
        },
      },
    },
  };
}

/** Await a rejection and hand back the thrown value, failing if none arrives. */
async function rejection(pending: Promise<unknown>): Promise<unknown> {
  return pending.then(
    () => {
      throw new Error("expected the operation to reject");
    },
    (error: unknown) => error,
  );
}

/** A JSON response carrying `body` at `status`. */
function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

/** An SDK instance whose transport always answers with `response`. */
function sdkAnswering(response: () => Response): WallowSdk {
  return createWallowSdk({
    baseUrl: "http://api.test",
    fetch: () => Promise.resolve(response()),
  });
}

describe("toWallowError", () => {
  it("reads the machine code from an RFC 7807 body's extensions.code", () => {
    const error = toWallowError(
      {
        status: 403,
        title: "Forbidden",
        detail: "CSRF token missing",
        extensions: { code: "CSRF_INVALID" },
      },
      403,
    );

    expect(error.status).toBe(403);
    expect(error.code).toBe("CSRF_INVALID");
    expect(error.title).toBe("Forbidden");
    expect(error.detail).toBe("CSRF token missing");
  });

  it("falls back to a flattened top-level code", () => {
    // Some serializer setups hoist RFC 7807 extension members onto the root.
    expect(toWallowError({ title: "Too Many Requests", code: "RATE_LIMITED" }, 429).code).toBe(
      "RATE_LIMITED",
    );
  });

  it("reads the MFA/auth controllers' raw { succeeded, error } shape", () => {
    // MfaController answers business failures with a bare anonymous object, not
    // problem details, so the code arrives under `error` and there is no title.
    const error = toWallowError({ succeeded: false, error: "invalid_code" }, 400);

    expect(error.status).toBe(400);
    expect(error.code).toBe("invalid_code");
  });

  it("prefers extensions.code over both a flattened code and a raw error member", () => {
    const error = toWallowError(
      { extensions: { code: "FROM_EXTENSIONS" }, code: "FROM_ROOT", error: "FROM_ERROR" },
      400,
    );

    expect(error.code).toBe("FROM_EXTENSIONS");
  });

  it("prefers a flattened code over the raw error member", () => {
    const error = toWallowError({ code: "FROM_ROOT", error: "FROM_ERROR" }, 400);

    expect(error.code).toBe("FROM_ROOT");
  });

  it("ignores a non-string code member rather than coercing it", () => {
    // OAuth-style bodies can carry an object under `error`; a stringified object
    // is not a machine code.
    expect(toWallowError({ error: { reason: "nested" } }, 400).code).toBe(UNKNOWN_ERROR_CODE);
  });

  it("takes the status the body names over the transport status", () => {
    expect(toWallowError({ status: 409, title: "Conflict" }, 400).status).toBe(409);
  });

  it("falls back to the transport status when the body names none", () => {
    expect(toWallowError({ title: "Unauthorized" }, 401).status).toBe(401);
  });

  it("falls back to 500 when neither the body nor the transport names a status", () => {
    expect(toWallowError({}, undefined).status).toBe(500);
  });

  it("defaults the title when the body carries no problem details", () => {
    expect(toWallowError({ error: "invalid_code" }, 400).title).toBe("Unknown error");
  });

  it("omits detail when the body's detail is not a string", () => {
    expect(toWallowError({ detail: 42 }, 400).detail).toBeUndefined();
  });

  it("normalizes an empty error body, recovering the status from the transport", () => {
    // A bare 401 from `/users/me` has NO body: the generated client throws the
    // empty response text, so the status lives only on the Response. Losing it
    // would turn every anonymous visitor into a 500 and break sign-out detection.
    const error = toWallowError("", 401);

    expect(error.status).toBe(401);
    expect(error.code).toBe(UNKNOWN_ERROR_CODE);
  });

  it("reports a request that never landed as a 503 NETWORK_ERROR, carrying its message", () => {
    // A network failure never reaches a response body, but it still has to
    // arrive as a WallowError rather than a bare TypeError — and it must stay
    // TELLABLE APART from a server that answered. `status` is a required number,
    // so "no status" cannot be the signal; the code is. Collapsing this into the
    // 500/UNKNOWN fallback is what left every screen's "unable to reach the
    // server" arm unreachable by construction (Wallow-sx3r).
    const error = toWallowError(new TypeError("Failed to fetch"), undefined);

    expect(error.status).toBe(503);
    expect(error.code).toBe("NETWORK_ERROR");
    expect(error.detail).toBe("Failed to fetch");
  });

  it("uses the same network-fault contract the BFF proxy raises on the server side", () => {
    // The browser's call to the BFF and the BFF's forward to the API are two
    // legs of one tunnel; a consumer must not have to learn two vocabularies for
    // the same fact. Pins the browser constant against the server entry's, the
    // way the unknown-code spec below pins UNKNOWN_ERROR_CODE.
    const error = toWallowError(new TypeError("Failed to fetch"), undefined);

    expect(error.code).toBe(NETWORK_ERROR_CODE);
  });

  it("does not call a failure a network fault when a response did arrive", () => {
    // The other way an Error reaches toWallowError: the response landed and only
    // its body could not be parsed. That is a server that ANSWERED, so it keeps
    // its real status and the ordinary unknown code — reporting it as
    // NETWORK_ERROR would tell a user with a working connection to check it.
    const error = toWallowError(new SyntaxError("Unexpected token < in JSON"), 502);

    expect(error.status).toBe(502);
    expect(error.code).toBe(UNKNOWN_ERROR_CODE);
    expect(error.detail).toBe("Unexpected token < in JSON");
  });

  it("brands the result so isWallowError recognizes it", () => {
    expect(isWallowError(toWallowError({ code: "NOT_FOUND" }, 404))).toBe(true);
  });

  it("hands back an already-normalized WallowError by identity", () => {
    // Interceptors chain, and the BFF tunnel already normalizes on the server
    // side; re-wrapping would bury the real code under an UNKNOWN.
    const already = new WallowError({ status: 404, code: "NOT_FOUND", title: "Not Found" });

    expect(toWallowError(already, 500)).toBe(already);
  });

  it("uses the same unknown-code constant the server entry publishes", () => {
    expect(toWallowError({}, 500).code).toBe(UNKNOWN_ERROR_CODE);
  });
});

describe("wireWallowErrorInterceptor", () => {
  it("registers exactly one error interceptor", () => {
    const { client, registered } = recordingClient();

    wireWallowErrorInterceptor(client);

    expect(registered).toHaveLength(1);
  });

  it("converts the thrown body through the registered interceptor", async () => {
    const { client, registered } = recordingClient();
    wireWallowErrorInterceptor(client);

    const converted = await registered[0]!(
      { title: "Bad Request", extensions: { code: "INVALID_TOTP" } },
      new Response(null, { status: 400 }),
    );

    expect(isWallowError(converted)).toBe(true);
    expect((converted as WallowError).code).toBe("INVALID_TOTP");
  });

  it("reads the status off the Response the client hands it", async () => {
    const { client, registered } = recordingClient();
    wireWallowErrorInterceptor(client);

    const converted = await registered[0]!("", new Response(null, { status: 401 }));

    expect((converted as WallowError).status).toBe(401);
  });

  it("survives a network fault, where the client has no Response to give", async () => {
    const { client, registered } = recordingClient();
    wireWallowErrorInterceptor(client);

    const converted = await registered[0]!(new TypeError("Failed to fetch"), undefined);

    expect(isWallowError(converted)).toBe(true);
    expect((converted as WallowError).status).toBe(503);
    expect((converted as WallowError).code).toBe(NETWORK_ERROR_CODE);
  });
});

/**
 * Request-id correlation on the browser side (Wallow-pu6a.6.7).
 *
 * The two halves arrive by different routes and only meet here: `requestId` is a
 * response HEADER the BFF echoed (the parsed body never carries it, so the
 * interceptor is the only place it is still reachable), and `traceId` is a BODY
 * member the API's problem details already carry. Both have to survive onto the
 * `WallowError` a component catches, or a user's bug report names nothing a
 * backend engineer can search Tempo for.
 */
describe("toWallowError correlation", () => {
  it("attaches the request id it is handed", () => {
    const error = toWallowError({ title: "Conflict", status: 409 }, 409, "req-abc-1");

    expect(error.requestId).toBe("req-abc-1");
  });

  it("attaches the request id to a transport fault, which has no body at all", () => {
    const error = toWallowError(new TypeError("Failed to fetch"), undefined, "req-abc-2");

    expect(error.requestId).toBe("req-abc-2");
  });

  it("leaves requestId undefined when none was given", () => {
    expect(toWallowError({ title: "Conflict" }, 409).requestId).toBeUndefined();
  });

  it("reads the backend trace id from extensions.traceId", () => {
    const error = toWallowError(
      { title: "Not Found", extensions: { code: "NOT_FOUND", traceId: "00-abc-def-01" } },
      404,
    );

    expect(error.traceId).toBe("00-abc-def-01");
  });

  it("reads a flattened top-level traceId", () => {
    expect(toWallowError({ title: "Bad Request", traceId: "00-root-01" }, 400).traceId).toBe(
      "00-root-01",
    );
  });

  it("ignores a non-string traceId rather than coercing it", () => {
    expect(toWallowError({ title: "Bad Request", traceId: 42 }, 400).traceId).toBeUndefined();
  });
});

/**
 * Field-level validation errors on the browser leg (Wallow-ov6w.1.2).
 *
 * `runtime-config.ts` parses the problem details a SECOND time, independently of
 * `server/errors.ts` — a body that reached the browser through the passthrough
 * topology never passed through `parseProblemDetails` at all. Both parsers
 * therefore have to read the `errors` member, and both have to distrust it: this
 * is a wire body, so an entry survives only when its value is an array of
 * strings, and `fieldErrors` stays `undefined` when no entry does.
 */
describe("toWallowError field errors", () => {
  it("reads the RFC 7807 errors member onto fieldErrors", () => {
    const error = toWallowError(
      {
        status: 400,
        title: "One or more validation errors occurred.",
        errors: { Name: ["'Name' must not be empty."], Email: ["Invalid email."] },
        extensions: { code: "VALIDATION_ERROR" },
      },
      400,
    );

    expect(error.fieldErrors).toEqual({
      Name: ["'Name' must not be empty."],
      Email: ["Invalid email."],
    });
  });

  it("leaves fieldErrors undefined when the body carries no errors member", () => {
    expect(toWallowError({ title: "Conflict", status: 409 }, 409).fieldErrors).toBeUndefined();
  });

  it("leaves fieldErrors undefined on a transport fault, which has no body", () => {
    expect(toWallowError(new TypeError("Failed to fetch"), undefined).fieldErrors).toBeUndefined();
  });

  it.each([
    ["a string errors member", "not an object"],
    ["an array errors member", ["Name"]],
    ["an empty errors member", {}],
    ["entries that are not arrays", { Name: "'Name' must not be empty." }],
    ["arrays holding non-strings", { Name: [42] }],
  ])("ignores %s rather than trusting the body", (_label: string, errors: unknown) => {
    expect(toWallowError({ title: "Bad Request", errors }, 400).fieldErrors).toBeUndefined();
  });

  it("keeps the well-formed entries and drops the malformed ones", () => {
    const error = toWallowError(
      { title: "Bad Request", errors: { Name: ["'Name' must not be empty."], Age: 42 } },
      400,
    );

    expect(error.fieldErrors).toEqual({ Name: ["'Name' must not be empty."] });
  });
});

describe("wireWallowErrorInterceptor correlation", () => {
  it("reads the request id off the Response the client hands it", async () => {
    const { client, registered } = recordingClient();
    wireWallowErrorInterceptor(client);

    const converted = await registered[0]!(
      { title: "Conflict", extensions: { code: "TENANT_SLUG_TAKEN" } },
      new Response(null, { status: 409, headers: { [REQUEST_ID_HEADER]: "req-from-header" } }),
    );

    expect((converted as WallowError).requestId).toBe("req-from-header");
  });

  it("survives a response that carries no correlation header", async () => {
    const { client, registered } = recordingClient();
    wireWallowErrorInterceptor(client);

    const converted = await registered[0]!("", new Response(null, { status: 401 }));

    expect((converted as WallowError).requestId).toBeUndefined();
  });
});

describe("createWallowSdk registers the error interceptor", () => {
  it("rejects a generated operation with a WallowError on an RFC 7807 failure", async () => {
    const sdk = sdkAnswering(() =>
      jsonResponse(403, {
        title: "Forbidden",
        detail: "CSRF token missing",
        extensions: { code: "CSRF_INVALID" },
      }),
    );

    const error = await rejection(usersGetCurrentUser({ client: sdk.client }));

    expect(isWallowError(error)).toBe(true);
    expect((error as WallowError).code).toBe("CSRF_INVALID");
    expect((error as WallowError).status).toBe(403);
  });

  it("rejects with the MFA controllers' raw-shape code", async () => {
    const sdk = sdkAnswering(() => jsonResponse(400, { succeeded: false, error: "invalid_code" }));

    const error = await rejection(usersGetCurrentUser({ client: sdk.client }));

    expect(isWallowError(error)).toBe(true);
    expect((error as WallowError).code).toBe("invalid_code");
  });

  it("recovers the transport status for an empty-body 401", async () => {
    const sdk = sdkAnswering(() => new Response(null, { status: 401 }));

    const error = await rejection(usersGetCurrentUser({ client: sdk.client }));

    expect(isWallowError(error)).toBe(true);
    expect((error as WallowError).status).toBe(401);
  });

  it("wraps a transport fault, which never produces a response at all", async () => {
    const sdk = createWallowSdk({
      baseUrl: "http://api.test",
      fetch: () => Promise.reject(new TypeError("Failed to fetch")),
    });

    const error = await rejection(usersGetCurrentUser({ client: sdk.client }));

    expect(isWallowError(error)).toBe(true);
  });

  it("carries the BFF's request id and the API's trace id onto the rejected error", async () => {
    // The whole correlation story, end to end: the id the BFF echoed on the
    // header and the trace id the API wrote into the body both land on the error
    // a component actually catches.
    const sdk = sdkAnswering(
      () =>
        new Response(
          JSON.stringify({
            title: "Internal Server Error",
            status: 500,
            traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
          }),
          {
            status: 500,
            headers: {
              "content-type": "application/problem+json",
              [REQUEST_ID_HEADER]: "e2e-req-1",
            },
          },
        ),
    );

    const error = await rejection(usersGetCurrentUser({ client: sdk.client }));

    expect((error as WallowError).requestId).toBe("e2e-req-1");
    expect((error as WallowError).traceId).toBe(
      "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    );
  });

  it("carries the API's field errors onto the rejected error", async () => {
    // What a form actually catches: a 400 from a failed command, with the
    // per-property messages still attached so each one can land on its field.
    const sdk = sdkAnswering(() =>
      jsonResponse(400, {
        title: "One or more validation errors occurred.",
        status: 400,
        errors: { Name: ["'Name' must not be empty."] },
        extensions: { code: "VALIDATION_ERROR" },
      }),
    );

    const error = await rejection(usersGetCurrentUser({ client: sdk.client }));

    expect(isWallowError(error)).toBe(true);
    expect((error as WallowError).fieldErrors).toEqual({ Name: ["'Name' must not be empty."] });
  });

  it("leaves the success path untouched", async () => {
    const sdk = sdkAnswering(() => jsonResponse(200, { id: "u1", email: "a@b.test" }));

    await expect(usersGetCurrentUser({ client: sdk.client })).resolves.toEqual({
      id: "u1",
      email: "a@b.test",
    });
  });
});
