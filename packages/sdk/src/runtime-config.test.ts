import { ApiFailure, ClientErrorCode, isApiFailure } from "@bc-solutions-coder/api-errors";
import { describe, expect, it } from "vitest";

import openApiConfig from "../openapi-ts.config";
import { createWallowSdk, type WallowSdk } from "./create-sdk";
import { client as generatedClient } from "./generated/client.gen";
import { usersGetCurrentUser } from "./generated";
import { REQUEST_ID_HEADER } from "./request-id";
import {
  type ApiFailureInterceptorClient,
  createClientConfig,
  toFailure,
  wireApiFailureInterceptor,
} from "./runtime-config";

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
  it("starts out pointed at the BFF path with credentials included", () => {
    const config = generatedClient.getConfig();

    expect(config.baseUrl).toBe("/api");
    expect(config.credentials).toBe("include");
  });
});

/**
 * The unified error contract.
 *
 * With `throwOnError: true` + `responseStyle: "data"` every generated operation
 * rejects with the PARSED response body, whose shape differs per endpoint family
 * (RFC 7807 problem details for most, a bare `{ succeeded, error }` object for
 * the Identity auth and MFA controllers, nothing at all for an empty 401). The
 * error interceptor is the single place that difference is erased — by handing
 * the body to `@bc-solutions-coder/api-errors`' parser — and the ONLY place the
 * transport status is still reachable.
 */

/** The interceptor shape the generated client hands its error middleware. */
type ErrorInterceptor = (error: unknown, response: Response | undefined) => unknown;

/** A client shell that records what was registered on its error interceptors. */
function recordingClient(): {
  client: ApiFailureInterceptorClient;
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
function jsonResponse(
  status: number,
  body: unknown,
  headers: Record<string, string> = {},
): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json", ...headers },
  });
}

/** An SDK instance whose transport always answers with `response`. */
function sdkAnswering(response: () => Response): WallowSdk {
  return createWallowSdk({
    baseUrl: "http://api.test",
    fetch: () => Promise.resolve(response()),
  });
}

describe("toFailure", () => {
  it("reads the machine code from an RFC 7807 body's top-level code", () => {
    const failure = toFailure(
      {
        type: "https://httpstatuses.io/403",
        title: "Forbidden",
        status: 403,
        detail: "CSRF token missing",
        code: "Bff.CsrfInvalid",
      },
      jsonResponse(403, {}),
    );

    expect(failure.code).toBe("Bff.CsrfInvalid");
    expect(failure.status).toBe(403);
    expect(failure.title).toBe("Forbidden");
    expect(failure.detail).toBe("CSRF token missing");
  });

  it("reads the auth controllers' raw { succeeded, error } shape under the OAuth grammar", () => {
    const failure = toFailure({ succeeded: false, error: "invalid_code" }, jsonResponse(400, {}));

    expect(failure.code).toBe("OAuth.InvalidCode");
    expect(failure.title).toBe("invalid_code");
    expect(failure.status).toBe(400);
  });

  it("takes the transport status over the one the body names", () => {
    const failure = toFailure(
      { status: 418, code: "Teapot", title: "Teapot" },
      jsonResponse(500, {}),
    );

    expect(failure.status).toBe(500);
  });

  it("calls a body without a code an unrecognized response", () => {
    const failure = toFailure({ title: "Bad Gateway", status: 502 }, jsonResponse(502, {}));

    expect(failure.code).toBe(ClientErrorCode.CLIENT_UNRECOGNIZED_RESPONSE);
    expect(failure.status).toBe(502);
  });

  it("normalizes an empty error body, recovering the status from the transport", () => {
    // hey-api hands `undefined` when there was nothing to parse (an empty 401).
    const failure = toFailure(undefined, new Response(null, { status: 401 }));

    expect(failure.status).toBe(401);
    expect(failure.code).toBe(ClientErrorCode.CLIENT_UNRECOGNIZED_RESPONSE);
  });

  it("passes a plain-text body through as the raw body text", () => {
    const failure = toFailure("<html>upstream down</html>", new Response(null, { status: 502 }));

    expect(failure.status).toBe(502);
    expect(failure.code).toBe(ClientErrorCode.CLIENT_UNRECOGNIZED_RESPONSE);
  });

  it("reports a request that never landed as a 503 Transport.NetworkError", () => {
    const failure = toFailure(new TypeError("Failed to fetch"), undefined);

    expect(failure.status).toBe(503);
    expect(failure.code).toBe(ClientErrorCode.TRANSPORT_NETWORK_ERROR);
    expect(failure.cause).toBeInstanceOf(TypeError);
  });

  it("does not call a failure a network fault when a response did arrive", () => {
    // hey-api throws the parse error itself when a non-JSON body arrives on a
    // JSON operation; the response is still there, so the status is real.
    const failure = toFailure(
      new SyntaxError("Unexpected token <"),
      new Response("<html/>", { status: 500 }),
    );

    expect(failure.status).toBe(500);
    expect(failure.code).toBe(ClientErrorCode.CLIENT_UNRECOGNIZED_RESPONSE);
    expect(failure.cause).toBeInstanceOf(SyntaxError);
  });

  it("keeps the parsed body itself as the cause of an unrecognized response", () => {
    const body = { foo: 1 };
    const failure = toFailure(body, jsonResponse(502, {}));

    expect(failure.code).toBe(ClientErrorCode.CLIENT_UNRECOGNIZED_RESPONSE);
    expect(failure.cause).toBe(body);
  });

  it("brands the result so isApiFailure recognizes it", () => {
    const failure = toFailure({ code: "X", title: "x" }, jsonResponse(400, {}));

    expect(isApiFailure(failure)).toBe(true);
  });

  it("hands back an already-normalized ApiFailure by identity", () => {
    const original = new ApiFailure({ status: 409, code: "Conflict", title: "Conflict" });

    expect(toFailure(original, jsonResponse(409, {}))).toBe(original);
    expect(toFailure(original, undefined)).toBe(original);
  });
});

describe("wireApiFailureInterceptor", () => {
  it("registers exactly one error interceptor", () => {
    const { client, registered } = recordingClient();

    wireApiFailureInterceptor(client);

    expect(registered).toHaveLength(1);
  });

  it("converts the thrown body through the registered interceptor", async () => {
    const { client, registered } = recordingClient();
    wireApiFailureInterceptor(client);

    const [interceptor] = registered;
    const result = await interceptor?.(
      { title: "Not Found", status: 404, code: "Users.NotFound" },
      jsonResponse(404, {}),
    );

    expect(isApiFailure(result)).toBe(true);
    expect((result as ApiFailure).code).toBe("Users.NotFound");
  });

  it("reads the status off the Response the client hands it", async () => {
    const { client, registered } = recordingClient();
    wireApiFailureInterceptor(client);

    const [interceptor] = registered;
    const result = await interceptor?.({ code: "X", title: "x" }, jsonResponse(429, {}));

    expect((result as ApiFailure).status).toBe(429);
  });

  it("survives a network fault, where the client has no Response to give", async () => {
    const { client, registered } = recordingClient();
    wireApiFailureInterceptor(client);

    const [interceptor] = registered;
    const result = await interceptor?.(new TypeError("Failed to fetch"), undefined);

    expect(isApiFailure(result)).toBe(true);
    expect((result as ApiFailure).code).toBe(ClientErrorCode.TRANSPORT_NETWORK_ERROR);
  });
});

describe("toFailure correlation", () => {
  it("reads the request id off the response header", () => {
    const failure = toFailure(
      { code: "X", title: "x" },
      jsonResponse(500, {}, { [REQUEST_ID_HEADER]: "req-1" }),
    );

    expect(failure.requestId).toBe("req-1");
  });

  it("attaches the request id to a thrown Error that arrived with a response", () => {
    const failure = toFailure(
      new SyntaxError("Unexpected token <"),
      new Response("<html/>", { status: 500, headers: { [REQUEST_ID_HEADER]: "req-2" } }),
    );

    expect(failure.requestId).toBe("req-2");
  });

  it("leaves requestId undefined when the response carries no correlation header", () => {
    expect(toFailure({ code: "X", title: "x" }, jsonResponse(500, {})).requestId).toBeUndefined();
  });

  it("reads the backend trace id from the body", () => {
    const failure = toFailure(
      { code: "X", title: "x", traceId: "00-abc-def-01" },
      jsonResponse(500, {}),
    );

    expect(failure.traceId).toBe("00-abc-def-01");
  });

  it("reads Retry-After off the response", () => {
    const failure = toFailure(
      { code: "RateLimited", title: "Too Many Requests" },
      jsonResponse(429, {}, { "retry-after": "30" }),
    );

    expect(failure.retryAfter).toBe(30);
  });

  it("keeps Retry-After when only the body read failed", () => {
    const failure = toFailure(
      new SyntaxError("Unexpected token <"),
      new Response("<html/>", { status: 503, headers: { "retry-after": "5" } }),
    );

    expect(failure.status).toBe(503);
    expect(failure.retryAfter).toBe(5);
  });
});

describe("toFailure field errors", () => {
  it("reads the RFC 7807 errors member onto fieldErrors", () => {
    const failure = toFailure(
      {
        title: "One or more validation errors occurred.",
        status: 400,
        code: "Validation.Failed",
        errors: { Email: ["'Email' is not a valid email address."] },
      },
      jsonResponse(400, {}),
    );

    expect(failure.fieldErrors).toEqual({ Email: ["'Email' is not a valid email address."] });
  });

  it("leaves fieldErrors undefined when the body carries no errors member", () => {
    expect(toFailure({ code: "X", title: "x" }, jsonResponse(400, {})).fieldErrors).toBeUndefined();
  });

  it("leaves fieldErrors undefined on a transport fault, which has no body", () => {
    expect(toFailure(new TypeError("Failed to fetch"), undefined).fieldErrors).toBeUndefined();
  });
});

describe("createWallowSdk registers the error interceptor", () => {
  it("rejects a generated operation with an ApiFailure on an RFC 7807 failure", async () => {
    const sdk = sdkAnswering(() =>
      jsonResponse(403, {
        title: "Forbidden",
        detail: "CSRF token missing",
        code: "Bff.CsrfInvalid",
      }),
    );

    const error = await rejection(usersGetCurrentUser({ client: sdk.client }));

    expect(isApiFailure(error)).toBe(true);
    expect((error as ApiFailure).code).toBe("Bff.CsrfInvalid");
    expect((error as ApiFailure).status).toBe(403);
    expect((error as ApiFailure).detail).toBe("CSRF token missing");
  });

  it("rejects with the auth controllers' raw shape under the OAuth grammar", async () => {
    const sdk = sdkAnswering(() => jsonResponse(400, { succeeded: false, error: "invalid_code" }));

    const error = await rejection(usersGetCurrentUser({ client: sdk.client }));

    expect(isApiFailure(error)).toBe(true);
    expect((error as ApiFailure).code).toBe("OAuth.InvalidCode");
    expect((error as ApiFailure).title).toBe("invalid_code");
  });

  it("recovers the transport status for an empty-body 401", async () => {
    const sdk = sdkAnswering(() => new Response(null, { status: 401 }));

    const error = await rejection(usersGetCurrentUser({ client: sdk.client }));

    expect(isApiFailure(error)).toBe(true);
    expect((error as ApiFailure).status).toBe(401);
    expect((error as ApiFailure).code).toBe(ClientErrorCode.CLIENT_UNRECOGNIZED_RESPONSE);
  });

  it("wraps a transport fault, which never produces a response at all", async () => {
    const sdk = createWallowSdk({
      baseUrl: "http://api.test",
      fetch: () => Promise.reject(new TypeError("Failed to fetch")),
    });

    const error = await rejection(usersGetCurrentUser({ client: sdk.client }));

    expect(isApiFailure(error)).toBe(true);
    expect((error as ApiFailure).code).toBe(ClientErrorCode.TRANSPORT_NETWORK_ERROR);
    expect((error as ApiFailure).status).toBe(503);
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
            code: "Server.Unhandled",
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

    expect((error as ApiFailure).requestId).toBe("e2e-req-1");
    expect((error as ApiFailure).traceId).toBe(
      "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    );
  });

  it("carries the API's field errors and Retry-After onto the rejected error", async () => {
    // What a form actually catches: a 400 from a failed command, with the
    // per-property messages still attached so each one can land on its field.
    const sdk = sdkAnswering(() =>
      jsonResponse(
        400,
        {
          title: "One or more validation errors occurred.",
          status: 400,
          errors: { Name: ["'Name' must not be empty."] },
          code: "Validation.Failed",
        },
        { "retry-after": "5" },
      ),
    );

    const error = await rejection(usersGetCurrentUser({ client: sdk.client }));

    expect(isApiFailure(error)).toBe(true);
    expect((error as ApiFailure).fieldErrors).toEqual({ Name: ["'Name' must not be empty."] });
    expect((error as ApiFailure).retryAfter).toBe(5);
  });

  it("leaves the success path untouched", async () => {
    const sdk = sdkAnswering(() => jsonResponse(200, { id: "u1", email: "a@b.test" }));

    await expect(usersGetCurrentUser({ client: sdk.client })).resolves.toEqual({
      id: "u1",
      email: "a@b.test",
    });
  });
});
