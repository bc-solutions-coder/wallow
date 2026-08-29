import { runInNewContext } from "node:vm";

import { describe, expect, it } from "vitest";

import {
  readInjectedWebAppUrl,
  resolveWebAppUrl,
  WEB_APP_URL_GLOBAL_KEY,
  WEB_APP_URL_VAR,
  webAppUrlScript,
} from "./web-app-url";

describe("resolveWebAppUrl", () => {
  it("answers the configured absolute URL", () => {
    expect(resolveWebAppUrl({ [WEB_APP_URL_VAR]: "https://wallow.dev" })).toBe(
      "https://wallow.dev/",
    );
  });

  it("trims surrounding whitespace", () => {
    expect(resolveWebAppUrl({ [WEB_APP_URL_VAR]: "  http://localhost:3000/  " })).toBe(
      "http://localhost:3000/",
    );
  });

  it.each([
    ["unset", {}],
    ["blank", { [WEB_APP_URL_VAR]: "" }],
    ["whitespace", { [WEB_APP_URL_VAR]: "   " }],
    ["relative", { [WEB_APP_URL_VAR]: "/dashboard" }],
    ["not a URL", { [WEB_APP_URL_VAR]: "wallow dev" }],
    ["a non-http scheme", { [WEB_APP_URL_VAR]: "mailto:admin@wallow.dev" }],
  ])("answers undefined when the variable is %s", (_label, env) => {
    expect(resolveWebAppUrl(env)).toBeUndefined();
  });
});

describe("webAppUrlScript / readInjectedWebAppUrl", () => {
  /** Run the emitted script as the browser would: against a `window` of its own. */
  function publish(url: string | undefined): Record<string, unknown> {
    const scope: Record<string, unknown> = {};
    runInNewContext(webAppUrlScript(url), { window: scope });
    return scope;
  }

  it("round-trips a URL through the published global", () => {
    expect(readInjectedWebAppUrl(publish("https://wallow.dev/"))).toBe("https://wallow.dev/");
  });

  it("publishes null, read back as undefined, when nothing is configured", () => {
    const scope = publish(undefined);

    expect(scope[WEB_APP_URL_GLOBAL_KEY]).toBeNull();
    expect(readInjectedWebAppUrl(scope)).toBeUndefined();
  });

  it("never emits a raw < that could close the script element", () => {
    const hostile = "https://wallow.dev/</script><img src=x onerror=alert(1)>";

    expect(webAppUrlScript(hostile)).not.toContain("<");
    expect(readInjectedWebAppUrl(publish(hostile))).toBe(hostile);
  });

  it("answers undefined for a scope that is not an object or carries junk", () => {
    expect(readInjectedWebAppUrl(null)).toBeUndefined();
    expect(readInjectedWebAppUrl({ [WEB_APP_URL_GLOBAL_KEY]: 42 })).toBeUndefined();
    expect(readInjectedWebAppUrl({ [WEB_APP_URL_GLOBAL_KEY]: "" })).toBeUndefined();
  });
});
