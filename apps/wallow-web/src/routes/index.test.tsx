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
