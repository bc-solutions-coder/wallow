import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { renderToString } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { Route } from "./index";

/**
 * SSR contract for the public home page (Wallow-8w1h.2.2 acceptance):
 * `GET /` must return server-rendered HTML containing an element with
 * data-testid="home-heading". We assert the route component renders that markup
 * via react-dom/server — the vitest equivalent of curling the dev server.
 */
describe("routes/index (public home SSR)", () => {
  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("server-renders an element carrying data-testid=home-heading", () => {
    const Home = Route.options.component!;
    const html = renderToString(<Home />);
    expect(html).toContain('data-testid="home-heading"');
  });

  it("server-renders visible heading text on the home page", () => {
    const Home = Route.options.component!;
    const html = renderToString(<Home />);
    // A non-empty <h1>..<h6> element must be present in the rendered shell.
    expect(html).toMatch(/<h[1-6][^>]*>[^<]*\S[^<]*<\/h[1-6]>/u);
  });
});

/**
 * Cached current-user gate (Wallow-evd5.2.3, regenerated in Wallow-pu6a.5.5):
 * the `beforeLoad` must read the user through the router-context QueryClient via
 * `ensureQueryData(currentUserQuery(context.sdk.client))` and no longer import
 * the retired `getWallowSdk().user.me()` facade.
 */
describe("routes/index (cached current-user gate wiring)", () => {
  it("no longer imports the retired getWallowSdk facade", () => {
    const source: string = readFileSync(
      fileURLToPath(new URL("./index.tsx", import.meta.url)),
      "utf8",
    );

    expect(source).not.toMatch(/getWallowSdk|lib\/wallow-sdk/u);
    expect(source).toMatch(/currentUserQuery/u);
    expect(source).toMatch(/ensureQueryData/u);
  });
});
