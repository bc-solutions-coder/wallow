/**
 * Specs for the shared SDK test seam (Wallow-pu6a.5.1).
 *
 * These run on the NODE project: the harness is a transport recorder plus a call
 * to the real `createWallowSdk()`, with no DOM involved. The browser side of the
 * seam is covered by `render-with-wallow.test.tsx`.
 *
 * Everything below asserts against the transport (`harness.fetch`, the recorded
 * `calls`) or against a plain `client.get`/`client.post`, deliberately NOT
 * against the `{ data, error }` vs. thrown-error shape of a generated operation:
 * Wallow-pu6a.5.2 flips the generated client to `throwOnError`/`responseStyle`,
 * and this seam's contract must not move when it does.
 */
import { beforeEach, describe, expect, it } from "vitest";

import {
  createSdkHarness,
  DEFAULT_HARNESS_BASE_URL,
  type LegacyConfigurableClient,
  type SdkHarness,
} from "./sdk-harness";

const OK = 200;
const CREATED = 201;
const BAD_REQUEST = 400;
const CONFLICT = 409;
const SETTLE_MS = 25;

function get(harness: SdkHarness, url: string): Promise<unknown> {
  return harness.client.get({ url }) as Promise<unknown>;
}

function post(harness: SdkHarness, url: string, body: unknown): Promise<unknown> {
  return harness.client.post({ url, body }) as Promise<unknown>;
}

describe("createSdkHarness", () => {
  let harness: SdkHarness;

  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("builds a real WallowSdk instance rather than a stub object", () => {
    // The whole point of the seam: `sdk` comes out of the SDK's own factory, so
    // the CSRF interceptor, request serialization and response parsing all run
    // for real. A hand-rolled fake would satisfy neither.
    expect(harness.sdk).toBeDefined();
    expect(harness.client).toBe(harness.sdk.client);
    expect(typeof harness.client.getConfig).toBe("function");
    expect(harness.client.getConfig().baseUrl).toBe(DEFAULT_HARNESS_BASE_URL);
  });

  it("honours an explicit baseUrl", () => {
    const custom = createSdkHarness({ baseUrl: "http://localhost:5001" });

    expect(custom.client.getConfig().baseUrl).toBe("http://localhost:5001");
  });

  it("starts with no recorded calls and no last call", () => {
    expect(harness.calls).toHaveLength(0);
    expect(harness.last).toBeUndefined();
  });

  it("routes every SDK request through its own transport and records it", async () => {
    harness.resolveJson({ id: "org-1" });

    await get(harness, "/v1/identity/organizations/org-1");

    expect(harness.calls).toHaveLength(1);
    expect(harness.last?.method).toBe("GET");
    expect(harness.last?.path).toBe("/api/v1/identity/organizations/org-1");
    expect(harness.last?.url).toBe(`${DEFAULT_HARNESS_BASE_URL}/v1/identity/organizations/org-1`);
  });

  it("records requests in order, with `last` tracking the most recent", async () => {
    harness.resolveJson({});

    await get(harness, "/v1/a");
    await get(harness, "/v1/b");

    expect(harness.calls.map((call) => call.path)).toEqual(["/api/v1/a", "/api/v1/b"]);
    expect(harness.last?.path).toBe("/api/v1/b");
  });

  it("decodes a JSON request body", async () => {
    harness.resolveJson({}, CREATED);

    await post(harness, "/v1/identity/organizations", { name: "Acme", slug: "acme" });

    expect(harness.last?.method).toBe("POST");
    expect(harness.last?.body).toEqual({ name: "Acme", slug: "acme" });
  });

  it("reports `undefined` for a body-less request rather than an empty string", async () => {
    harness.resolveJson({});

    await get(harness, "/v1/identity/users/me");

    expect(harness.last?.body).toBeUndefined();
  });

  it("captures outgoing headers under lowercased keys", async () => {
    harness.resolveJson({});

    await post(harness, "/v1/identity/organizations", { name: "Acme" });

    expect(harness.last?.headers["content-type"]).toContain("application/json");
  });

  it("records the query string on `url` while `path` stays the pathname", async () => {
    harness.resolveJson({});

    await get(harness, "/v1/identity/organizations?page=2");

    expect(harness.last?.path).toBe("/api/v1/identity/organizations");
    expect(harness.last?.url).toContain("page=2");
  });

  describe("responders", () => {
    it("defaults to an empty JSON object at 200 before any responder is programmed", async () => {
      const response = await harness.fetch(new Request(`${DEFAULT_HARNESS_BASE_URL}/v1/ping`));

      expect(response.status).toBe(OK);
      expect(await response.json()).toEqual({});
    });

    it("resolveJson serves the given payload at the given status", async () => {
      harness.resolveJson({ id: "org-1" }, CREATED);

      const response = await harness.fetch(new Request(`${DEFAULT_HARNESS_BASE_URL}/v1/orgs`));

      expect(response.status).toBe(CREATED);
      expect(response.ok).toBe(true);
      expect(await response.json()).toEqual({ id: "org-1" });
    });

    it("rejectJson serves a non-2xx response, defaulting to 400", async () => {
      harness.rejectJson({ detail: "Nope" });

      const response = await harness.fetch(new Request(`${DEFAULT_HARNESS_BASE_URL}/v1/orgs`));

      expect(response.status).toBe(BAD_REQUEST);
      expect(response.ok).toBe(false);
      expect(await response.json()).toEqual({ detail: "Nope" });
    });

    it("rejectJson honours an explicit status so RFC7807 problem details can be replayed", async () => {
      harness.rejectJson(
        { type: "about:blank", title: "Conflict", status: CONFLICT, detail: "Slug taken" },
        CONFLICT,
      );

      const response = await harness.fetch(new Request(`${DEFAULT_HARNESS_BASE_URL}/v1/orgs`));

      expect(response.status).toBe(CONFLICT);
    });

    it("pending never settles, so a spec can assert a loading state", async () => {
      harness.pending();
      let settled = false;

      void harness
        .fetch(new Request(`${DEFAULT_HARNESS_BASE_URL}/v1/orgs`))
        .then(() => (settled = true));
      await new Promise((resolve) => {
        setTimeout(resolve, SETTLE_MS);
      });

      expect(settled).toBe(false);
      // The request is still RECORDED even though it never resolves — that is
      // what lets a spec assert "the mutation fired" while the button is busy.
      expect(harness.calls).toHaveLength(1);
    });

    it("respond installs a per-request responder that sees the decoded call", async () => {
      harness.respond((call) =>
        Response.json({ echoedPath: call.path, echoedBody: call.body }, { status: OK }),
      );

      await post(harness, "/v1/identity/organizations", { name: "Acme" });
      const response = await harness.fetch(
        new Request(`${DEFAULT_HARNESS_BASE_URL}/v1/identity/organizations`, {
          method: "POST",
          body: JSON.stringify({ name: "Acme" }),
          headers: { "content-type": "application/json" },
        }),
      );

      expect(await response.json()).toEqual({
        echoedPath: "/api/v1/identity/organizations",
        echoedBody: { name: "Acme" },
      });
    });

    it("the latest responder wins over an earlier one", async () => {
      harness.resolveJson({ first: true });
      harness.resolveJson({ second: true });

      const response = await harness.fetch(new Request(`${DEFAULT_HARNESS_BASE_URL}/v1/orgs`));

      expect(await response.json()).toEqual({ second: true });
    });
  });

  it("reset clears recorded calls and restores the default responder", async () => {
    harness.rejectJson({ detail: "Nope" });
    await expect(get(harness, "/v1/a")).rejects.toThrow();

    harness.reset();
    const response = await harness.fetch(new Request(`${DEFAULT_HARNESS_BASE_URL}/v1/ping`));

    expect(harness.calls).toHaveLength(1);
    expect(harness.last?.path).toBe("/api/v1/ping");
    expect(response.status).toBe(OK);
    expect(await response.json()).toEqual({});
  });

  it("gives each harness its own SDK instance and its own recorder", async () => {
    // Per-instance isolation is the property the module-global `client.setConfig`
    // seam could not offer: two concurrent specs (or an SSR-vs-browser pair in
    // one spec) must not see each other's calls.
    const other = createSdkHarness();
    harness.resolveJson({});
    other.resolveJson({});

    await get(harness, "/v1/a");

    expect(harness.calls).toHaveLength(1);
    expect(other.calls).toHaveLength(0);
    expect(other.sdk).not.toBe(harness.sdk);
  });

  describe("legacyClients bridge (removed with Wallow-pu6a.5.5)", () => {
    it("points an already-constructed client at the harness transport", () => {
      const configs: { fetch?: typeof globalThis.fetch }[] = [];
      const legacy: LegacyConfigurableClient = {
        setConfig: (config) => {
          configs.push(config);
          return config;
        },
      };

      const bridged = createSdkHarness({ legacyClients: [legacy] });

      expect(configs).toHaveLength(1);
      expect(configs[0]?.fetch).toBe(bridged.fetch);
    });

    it("bridges every client it is given", () => {
      const seen: string[] = [];
      const make = (name: string): LegacyConfigurableClient => ({
        setConfig: (config) => {
          seen.push(name);
          return config;
        },
      });

      createSdkHarness({ legacyClients: [make("a"), make("b")] });

      expect(seen).toEqual(["a", "b"]);
    });

    it("records requests made through a bridged client alongside the SDK's own", async () => {
      let bridgedFetch: typeof globalThis.fetch | undefined;
      const legacy: LegacyConfigurableClient = {
        setConfig: (config) => {
          bridgedFetch = config.fetch;
          return config;
        },
      };
      const bridged = createSdkHarness({ legacyClients: [legacy] });
      bridged.resolveJson({ ok: true });

      await bridgedFetch?.(new Request(`${DEFAULT_HARNESS_BASE_URL}/v1/legacy`));

      expect(bridged.last?.path).toBe("/api/v1/legacy");
    });
  });
});
