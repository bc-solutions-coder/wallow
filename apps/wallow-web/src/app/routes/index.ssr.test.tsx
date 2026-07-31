import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { renderToString } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { Route } from "./index";

/**
 * SSR contract for the public home page: `GET /` returns server-rendered HTML
 * carrying `data-testid="home-heading"` and a non-empty heading, asserted
 * through `react-dom/server` — the vitest equivalent of curling the dev server.
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
 * The home `beforeLoad` reads the user through the router-context QueryClient,
 * not through a module-global SDK facade.
 */
describe("routes/index (cached current-user gate wiring)", () => {
  it("no longer imports the retired getWallowSdk facade", () => {
    const source: string = readFileSync(
      fileURLToPath(new URL("./index.tsx", import.meta.url)),
      "utf8",
    );

    expect(source).not.toMatch(/getWallowSdk|lib\/wallow-sdk/u);
    // Either spelling of the shared read from `@bc-solutions-coder/auth`: the
    // query plus `ensureQueryData`, or the `ensureCurrentUser` primer that
    // composes the pair.
    expect(source).toMatch(/currentUserQuery|ensureCurrentUser/u);
    expect(source).toMatch(/ensureQueryData|ensureCurrentUser/u);
  });
});
