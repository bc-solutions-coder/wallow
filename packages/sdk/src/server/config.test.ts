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
    const config: BffConfig = loadBffConfigFromEnv(envWith({ SESSION_TTL_SECONDS: "" }));

    expect(config.sessionTtlSeconds).toBe(86400);
  });

  it("reads SESSION_TTL_SECONDS as a number", () => {
    const config: BffConfig = loadBffConfigFromEnv(envWith({ SESSION_TTL_SECONDS: "3600" }));

    expect(config.sessionTtlSeconds).toBe(3600);
    expect(typeof config.sessionTtlSeconds).toBe("number");
  });

  it("tolerates surrounding whitespace in SESSION_TTL_SECONDS", () => {
    const config: BffConfig = loadBffConfigFromEnv(envWith({ SESSION_TTL_SECONDS: "  1800  " }));

    expect(config.sessionTtlSeconds).toBe(1800);
  });

  it("throws when SESSION_TTL_SECONDS is not numeric", () => {
    expect(() => loadBffConfigFromEnv(envWith({ SESSION_TTL_SECONDS: "not-a-number" }))).toThrow(
      /SESSION_TTL_SECONDS/,
    );
  });

  it("throws when SESSION_TTL_SECONDS has a numeric prefix but trailing garbage", () => {
    expect(() => loadBffConfigFromEnv(envWith({ SESSION_TTL_SECONDS: "3600abc" }))).toThrow(
      /SESSION_TTL_SECONDS/,
    );
  });

  it("throws when SESSION_TTL_SECONDS is zero", () => {
    expect(() => loadBffConfigFromEnv(envWith({ SESSION_TTL_SECONDS: "0" }))).toThrow(
      /SESSION_TTL_SECONDS/,
    );
  });

  it("throws when SESSION_TTL_SECONDS is negative", () => {
    expect(() => loadBffConfigFromEnv(envWith({ SESSION_TTL_SECONDS: "-1" }))).toThrow(
      /SESSION_TTL_SECONDS/,
    );
  });

  it("throws when SESSION_TTL_SECONDS is fractional", () => {
    expect(() => loadBffConfigFromEnv(envWith({ SESSION_TTL_SECONDS: "60.5" }))).toThrow(
      /SESSION_TTL_SECONDS/,
    );
  });
});

describe("loadBffConfigFromEnv — cookieSecure", () => {
  it("defaults to true when COOKIE_SECURE is unset", () => {
    const config: BffConfig = loadBffConfigFromEnv(requiredEnv());

    expect(config.cookieSecure).toBe(true);
  });

  it("defaults to true when COOKIE_SECURE is empty", () => {
    const config: BffConfig = loadBffConfigFromEnv(envWith({ COOKIE_SECURE: "" }));

    expect(config.cookieSecure).toBe(true);
  });

  it("is false when COOKIE_SECURE is 'false'", () => {
    const config: BffConfig = loadBffConfigFromEnv(envWith({ COOKIE_SECURE: "false" }));

    expect(config.cookieSecure).toBe(false);
    expect(typeof config.cookieSecure).toBe("boolean");
  });

  it("is false when COOKIE_SECURE is 'false' in mixed case or padded", () => {
    expect(loadBffConfigFromEnv(envWith({ COOKIE_SECURE: "FALSE" })).cookieSecure).toBe(false);
    expect(loadBffConfigFromEnv(envWith({ COOKIE_SECURE: " False " })).cookieSecure).toBe(false);
  });

  it("is true when COOKIE_SECURE is 'true'", () => {
    const config: BffConfig = loadBffConfigFromEnv(envWith({ COOKIE_SECURE: "true" }));

    expect(config.cookieSecure).toBe(true);
  });

  it("fails secure: any unrecognized COOKIE_SECURE value keeps Secure on", () => {
    expect(loadBffConfigFromEnv(envWith({ COOKIE_SECURE: "0" })).cookieSecure).toBe(true);
    expect(loadBffConfigFromEnv(envWith({ COOKIE_SECURE: "no" })).cookieSecure).toBe(true);
    expect(loadBffConfigFromEnv(envWith({ COOKIE_SECURE: "garbage" })).cookieSecure).toBe(true);
  });
});

/**
 * Env-contract fail-fast (Wallow-pu6a.3.7).
 *
 * The loader used to throw on the FIRST missing variable, so a fork bringing up
 * a new environment fixed one name, restarted, and hit the next — one round trip
 * per variable. It now validates the whole contract and throws ONE error listing
 * every problem it found, at boot, so a misconfigured deployment never reaches
 * the first request and answers it with a 500.
 */
describe("loadBffConfigFromEnv — aggregated env-contract validation", () => {
  it("names EVERY missing required variable in a single error, not just the first", () => {
    const env: NodeJS.ProcessEnv = requiredEnv();
    delete env.OIDC_ISSUER;
    delete env.OIDC_CLIENT_SECRET;
    delete env.COOKIE_PASSWORD;

    let caught: unknown;
    try {
      loadBffConfigFromEnv(env);
    } catch (error: unknown) {
      caught = error;
    }

    expect(caught).toBeInstanceOf(Error);
    const message: string = (caught as Error).message;
    expect(message).toContain("OIDC_ISSUER");
    expect(message).toContain("OIDC_CLIENT_SECRET");
    expect(message).toContain("COOKIE_PASSWORD");
  });

  it("lists all seven when the environment is empty", () => {
    let caught: unknown;
    try {
      loadBffConfigFromEnv({} as NodeJS.ProcessEnv);
    } catch (error: unknown) {
      caught = error;
    }

    const message: string = (caught as Error).message;
    for (const key of [
      "OIDC_ISSUER",
      "OIDC_CLIENT_ID",
      "OIDC_CLIENT_SECRET",
      "OIDC_REDIRECT_URI",
      "OIDC_POST_LOGOUT_REDIRECT_URI",
      "BFF_API_BASE_URL",
      "COOKIE_PASSWORD",
    ]) {
      expect(message).toContain(key);
    }
  });

  it("treats an empty value as missing and names it alongside the absent ones", () => {
    const env: NodeJS.ProcessEnv = requiredEnv();
    env.OIDC_CLIENT_ID = "";
    delete env.BFF_API_BASE_URL;

    expect(() => loadBffConfigFromEnv(env)).toThrow(/OIDC_CLIENT_ID/u);
    expect(() => loadBffConfigFromEnv(env)).toThrow(/BFF_API_BASE_URL/u);
  });

  it("reports an invalid SESSION_TTL_SECONDS in the SAME error as a missing variable", () => {
    const env: NodeJS.ProcessEnv = envWith({ SESSION_TTL_SECONDS: "not-a-number" });
    delete env.COOKIE_PASSWORD;

    let caught: unknown;
    try {
      loadBffConfigFromEnv(env);
    } catch (error: unknown) {
      caught = error;
    }

    const message: string = (caught as Error).message;
    expect(message).toContain("SESSION_TTL_SECONDS");
    expect(message).toContain("COOKIE_PASSWORD");
  });

  it("throws exactly one error, so a caller cannot see a partial contract", () => {
    const env: NodeJS.ProcessEnv = requiredEnv();
    delete env.OIDC_ISSUER;
    delete env.OIDC_CLIENT_ID;

    const thrown: unknown[] = [];
    try {
      loadBffConfigFromEnv(env);
    } catch (error: unknown) {
      thrown.push(error);
    }

    expect(thrown).toHaveLength(1);
    // One error, one message — the second name must not require a second boot.
    expect((thrown[0] as Error).message.split("OIDC_").length - 1).toBeGreaterThanOrEqual(2);
  });

  it("still builds the config when the whole contract is satisfied", () => {
    const config: BffConfig = loadBffConfigFromEnv(requiredEnv());

    expect(config.issuer).toBe("https://auth.example.com");
    expect(config.cookiePassword).toBe("0".repeat(32));
  });
});

describe("loadBffConfigFromEnv — existing behavior is unchanged", () => {
  it("still requires the seven required variables", () => {
    const env: NodeJS.ProcessEnv = requiredEnv();
    delete env.COOKIE_PASSWORD;

    expect(() => loadBffConfigFromEnv(env)).toThrow(/COOKIE_PASSWORD/);
  });

  it("still applies the existing scope and metadata defaults", () => {
    const config: BffConfig = loadBffConfigFromEnv(requiredEnv());

    expect(config.scopes).toEqual(["openid", "profile", "email", "offline_access"]);
    expect(config.metadataUrl).toBeUndefined();
  });
});

/**
 * The default session-cookie name carries the `__Host-` prefix
 * (Wallow-pu6a.3.2, finding F10).
 *
 * `__Host-` is the only cookie protection a subdomain cannot defeat: a browser
 * refuses to accept such a cookie unless it is `Secure`, `Path=/`, and carries
 * no `Domain`, which means a compromised or attacker-controlled sibling host
 * cannot overwrite the session cookie of `app.example.com`. Without it, cookie
 * fixation from any subdomain stays open.
 *
 * The prefix is only honoured over HTTPS, so it has to follow `cookieSecure`:
 * on a plain-http dev origin a `__Host-` cookie would be silently DROPPED by
 * the browser and login would fail with no error anywhere. `COOKIE_SECURE=false`
 * therefore also drops the prefix, and `COOKIE_HOST_PREFIX=false` exists as the
 * explicit opt-out for the rare deployment that terminates TLS but cannot meet
 * the prefix's other requirements.
 */
describe("loadBffConfigFromEnv — cookieName __Host- prefix", () => {
  it("defaults to __Host-wallow_bff when cookies are Secure", () => {
    const config: BffConfig = loadBffConfigFromEnv(requiredEnv());

    expect(config.cookieName).toBe("__Host-wallow_bff");
    expect(config.cookieSecure).toBe(true);
  });

  it("drops the prefix when COOKIE_SECURE is false, because the browser would reject it", () => {
    const config: BffConfig = loadBffConfigFromEnv(envWith({ COOKIE_SECURE: "false" }));

    expect(config.cookieName).toBe("wallow_bff");
  });

  it("honours an explicit COOKIE_NAME verbatim, prefix or not", () => {
    expect(loadBffConfigFromEnv(envWith({ COOKIE_NAME: "my_session" })).cookieName).toBe(
      "my_session",
    );
    expect(loadBffConfigFromEnv(envWith({ COOKIE_NAME: "__Host-my_session" })).cookieName).toBe(
      "__Host-my_session",
    );
  });

  it("does not double-prefix an explicit COOKIE_NAME that already carries it", () => {
    const config: BffConfig = loadBffConfigFromEnv(envWith({ COOKIE_NAME: "__Host-wallow_bff" }));

    expect(config.cookieName).toBe("__Host-wallow_bff");
  });

  it("drops the prefix when COOKIE_HOST_PREFIX is 'false'", () => {
    const config: BffConfig = loadBffConfigFromEnv(envWith({ COOKIE_HOST_PREFIX: "false" }));

    // The opt-out relaxes the cookie NAME only; Secure is a separate knob.
    expect(config.cookieName).toBe("wallow_bff");
    expect(config.cookieSecure).toBe(true);
  });

  it("treats the COOKIE_HOST_PREFIX opt-out case-insensitively and trims it", () => {
    expect(loadBffConfigFromEnv(envWith({ COOKIE_HOST_PREFIX: "FALSE" })).cookieName).toBe(
      "wallow_bff",
    );
    expect(loadBffConfigFromEnv(envWith({ COOKIE_HOST_PREFIX: " False " })).cookieName).toBe(
      "wallow_bff",
    );
  });

  it("fails secure: any unrecognized COOKIE_HOST_PREFIX value keeps the prefix", () => {
    expect(loadBffConfigFromEnv(envWith({ COOKIE_HOST_PREFIX: "" })).cookieName).toBe(
      "__Host-wallow_bff",
    );
    expect(loadBffConfigFromEnv(envWith({ COOKIE_HOST_PREFIX: "0" })).cookieName).toBe(
      "__Host-wallow_bff",
    );
    expect(loadBffConfigFromEnv(envWith({ COOKIE_HOST_PREFIX: "garbage" })).cookieName).toBe(
      "__Host-wallow_bff",
    );
  });
});
