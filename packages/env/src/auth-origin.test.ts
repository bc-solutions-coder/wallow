import { describe, expect, it } from "vitest";

import {
  AUTH_URL_GLOBAL_KEY,
  authUrlScript,
  DEFAULT_AUTH_URL,
  readInjectedAuthUrl,
  resolveAuthUrl,
} from "./auth-origin";

describe("resolveAuthUrl", () => {
  it("answers the environment's WALLOW_AUTH_URL, trimmed of trailing slashes", () => {
    expect(resolveAuthUrl({ WALLOW_AUTH_URL: "https://wallow.dev/auth/" })).toBe(
      "https://wallow.dev/auth",
    );
  });

  it("treats an unset or blank variable as the local dev default", () => {
    expect(resolveAuthUrl({})).toBe(DEFAULT_AUTH_URL);
    expect(resolveAuthUrl({ WALLOW_AUTH_URL: "   " })).toBe(DEFAULT_AUTH_URL);
    expect(resolveAuthUrl()).toBe(DEFAULT_AUTH_URL);
  });
});

describe("authUrlScript / readInjectedAuthUrl", () => {
  it("round-trips through the published global", () => {
    const script: string = authUrlScript("https://wallow.dev/auth");
    // The script is `window["__WALLOW_AUTH_URL__"]=<json>;` — parse the payload back.
    const payload: string = script.slice(script.indexOf("=") + 1).replace(/;$/u, "");
    const scope: Record<string, unknown> = { [AUTH_URL_GLOBAL_KEY]: JSON.parse(payload) };
    expect(readInjectedAuthUrl(scope)).toBe("https://wallow.dev/auth");
  });

  it("emits no raw < so the inline script cannot be ended early", () => {
    expect(authUrlScript("https://x.test/</script>")).not.toContain("<");
  });

  it("reads back undefined for anything that is not a non-blank string", () => {
    expect(readInjectedAuthUrl(undefined)).toBeUndefined();
    expect(readInjectedAuthUrl({})).toBeUndefined();
    expect(readInjectedAuthUrl({ [AUTH_URL_GLOBAL_KEY]: 7 })).toBeUndefined();
    expect(readInjectedAuthUrl({ [AUTH_URL_GLOBAL_KEY]: " " })).toBeUndefined();
  });
});
