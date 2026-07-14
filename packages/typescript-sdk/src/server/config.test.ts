import { describe, expect, it } from "vitest";

import { type BffConfig, loadBffConfigFromEnv } from "./config";

/** The seven variables the loader requires; every test starts from these. */
function requiredEnv(): NodeJS.ProcessEnv {
  return {
    OIDC_ISSUER: "https://auth.example.com",
    OIDC_CLIENT_ID: "wallow-bff",
    OIDC_CLIENT_SECRET: "s3cret",
    OIDC_REDIRECT_URI: "https://app.example.com/auth/callback",
    OIDC_POST_LOGOUT_REDIRECT_URI: "https://app.example.com/",
    BFF_API_BASE_URL: "https://api.example.com",
    COOKIE_PASSWORD: "0".repeat(32),
  } as NodeJS.ProcessEnv;
}

function envWith(overrides: Record<string, string>): NodeJS.ProcessEnv {
  return { ...requiredEnv(), ...overrides } as NodeJS.ProcessEnv;
}

describe("loadBffConfigFromEnv — sessionTtlSeconds", () => {
  it("defaults to 86400 seconds when SESSION_TTL_SECONDS is unset", () => {
    const config: BffConfig = loadBffConfigFromEnv(requiredEnv());

    expect(config.sessionTtlSeconds).toBe(86400);
  });

  it("defaults to 86400 seconds when SESSION_TTL_SECONDS is empty", () => {
    const config: BffConfig = loadBffConfigFromEnv(
      envWith({ SESSION_TTL_SECONDS: "" }),
    );

    expect(config.sessionTtlSeconds).toBe(86400);
  });

  it("reads SESSION_TTL_SECONDS as a number", () => {
    const config: BffConfig = loadBffConfigFromEnv(
      envWith({ SESSION_TTL_SECONDS: "3600" }),
    );

    expect(config.sessionTtlSeconds).toBe(3600);
    expect(typeof config.sessionTtlSeconds).toBe("number");
  });

  it("tolerates surrounding whitespace in SESSION_TTL_SECONDS", () => {
    const config: BffConfig = loadBffConfigFromEnv(
      envWith({ SESSION_TTL_SECONDS: "  1800  " }),
    );

    expect(config.sessionTtlSeconds).toBe(1800);
  });

  it("throws when SESSION_TTL_SECONDS is not numeric", () => {
    expect(() =>
      loadBffConfigFromEnv(envWith({ SESSION_TTL_SECONDS: "not-a-number" })),
    ).toThrow(/SESSION_TTL_SECONDS/);
  });

  it("throws when SESSION_TTL_SECONDS has a numeric prefix but trailing garbage", () => {
    expect(() =>
      loadBffConfigFromEnv(envWith({ SESSION_TTL_SECONDS: "3600abc" })),
    ).toThrow(/SESSION_TTL_SECONDS/);
  });

  it("throws when SESSION_TTL_SECONDS is zero", () => {
    expect(() =>
      loadBffConfigFromEnv(envWith({ SESSION_TTL_SECONDS: "0" })),
    ).toThrow(/SESSION_TTL_SECONDS/);
  });

  it("throws when SESSION_TTL_SECONDS is negative", () => {
    expect(() =>
      loadBffConfigFromEnv(envWith({ SESSION_TTL_SECONDS: "-1" })),
    ).toThrow(/SESSION_TTL_SECONDS/);
  });

  it("throws when SESSION_TTL_SECONDS is fractional", () => {
    expect(() =>
      loadBffConfigFromEnv(envWith({ SESSION_TTL_SECONDS: "60.5" })),
    ).toThrow(/SESSION_TTL_SECONDS/);
  });
});

describe("loadBffConfigFromEnv — cookieSecure", () => {
  it("defaults to true when COOKIE_SECURE is unset", () => {
    const config: BffConfig = loadBffConfigFromEnv(requiredEnv());

    expect(config.cookieSecure).toBe(true);
  });

  it("defaults to true when COOKIE_SECURE is empty", () => {
    const config: BffConfig = loadBffConfigFromEnv(
      envWith({ COOKIE_SECURE: "" }),
    );

    expect(config.cookieSecure).toBe(true);
  });

  it("is false when COOKIE_SECURE is 'false'", () => {
    const config: BffConfig = loadBffConfigFromEnv(
      envWith({ COOKIE_SECURE: "false" }),
    );

    expect(config.cookieSecure).toBe(false);
    expect(typeof config.cookieSecure).toBe("boolean");
  });

  it("is false when COOKIE_SECURE is 'false' in mixed case or padded", () => {
    expect(
      loadBffConfigFromEnv(envWith({ COOKIE_SECURE: "FALSE" })).cookieSecure,
    ).toBe(false);
    expect(
      loadBffConfigFromEnv(envWith({ COOKIE_SECURE: " False " })).cookieSecure,
    ).toBe(false);
  });

  it("is true when COOKIE_SECURE is 'true'", () => {
    const config: BffConfig = loadBffConfigFromEnv(
      envWith({ COOKIE_SECURE: "true" }),
    );

    expect(config.cookieSecure).toBe(true);
  });

  it("fails secure: any unrecognized COOKIE_SECURE value keeps Secure on", () => {
    expect(
      loadBffConfigFromEnv(envWith({ COOKIE_SECURE: "0" })).cookieSecure,
    ).toBe(true);
    expect(
      loadBffConfigFromEnv(envWith({ COOKIE_SECURE: "no" })).cookieSecure,
    ).toBe(true);
    expect(
      loadBffConfigFromEnv(envWith({ COOKIE_SECURE: "garbage" })).cookieSecure,
    ).toBe(true);
  });
});

describe("loadBffConfigFromEnv — existing behavior is unchanged", () => {
  it("still requires the seven required variables", () => {
    const env: NodeJS.ProcessEnv = requiredEnv();
    delete env.COOKIE_PASSWORD;

    expect(() => loadBffConfigFromEnv(env)).toThrow(/COOKIE_PASSWORD/);
  });

  it("still applies the existing scope, cookie name, and metadata defaults", () => {
    const config: BffConfig = loadBffConfigFromEnv(requiredEnv());

    expect(config.scopes).toEqual([
      "openid",
      "profile",
      "email",
      "offline_access",
    ]);
    expect(config.cookieName).toBe("wallow_bff");
    expect(config.metadataUrl).toBeUndefined();
  });
});
