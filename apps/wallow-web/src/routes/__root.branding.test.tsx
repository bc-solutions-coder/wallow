import { renderToString } from "react-dom/server";
import { afterEach, describe, expect, it, vi } from "vitest";

import { appIconUrl, forkBranding, forkResolvedBranding, renderThemeStyle } from "../lib/branding";

/**
 * The document shell's half of the styles-adoption contract (Wallow-ffpq.3.4),
 * mirroring wallow-auth's already-solved shell. Once the shared package is
 * wired, the shell must (1) point the favicon at the fork's icon from the site
 * ROOT — a relative URL 404s on every nested route at once, since the head is
 * byte-identical everywhere — and (2) emit the fork's palette as CSS custom
 * properties in a `<style>`, so every route resolves the `@theme` tokens the
 * class names reference. The current shell does neither.
 *
 * TanStack's `<Outlet/>` cannot render outside a `RouterProvider`, so it is
 * replaced with a sentinel; `ReadyIndicator`'s signal is a post-commit
 * `document.body` effect with no SSR markup, so it too is mocked — the point
 * here is the head the shell renders, not those two.
 */
vi.mock("@tanstack/react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@tanstack/react-router")>();
  return {
    ...actual,
    Outlet: () => <div data-testid="router-outlet" />,
  };
});

vi.mock("../components/ready-indicator", () => ({
  ReadyIndicator: () => <div data-testid="web-ready-indicator" />,
}));

// The shell also mounts `<FocusOnNavigate/>` (from `@bc-solutions-coder/ui`),
// whose `useRouterState` call throws under a bare `renderToString(<Shell/>)`
// with no `RouterProvider`. It renders nothing into the head this suite asserts
// on, so it is a render-nothing sentinel here; its wiring lives in
// `__root.focus.test.tsx`.
vi.mock("@bc-solutions-coder/ui", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/ui")>();
  return {
    ...actual,
    FocusOnNavigate: () => null,
  };
});

async function renderShell(): Promise<string> {
  const { Route } = await import("./__root");
  const Shell = Route.options.component!;
  return renderToString(<Shell />);
}

describe("routes/__root (brand assets and theme)", () => {
  it("points the favicon at the fork's icon, from the site root", async () => {
    // The fork's icon is the favicon — api/branding.json names one asset, and the
    // tab icon is the other place it belongs. `appIconUrl` is root-relative.
    const html: string = await renderShell();

    expect(html).toMatch(new RegExp(`<link[^>]*rel="icon"[^>]*href="${appIconUrl}"`, "u"));
  });

  it("references no brand asset relative to the current route", async () => {
    const html: string = await renderShell();

    expect(html).toContain(forkBranding.appIcon);
    expect(html).not.toMatch(new RegExp(`href="(?!/)[^"]*${forkBranding.appIcon}`, "u"));
  });

  it("emits the fork's theme tokens as CSS custom properties in the head", async () => {
    // The shared package owns the @theme token map; the shell renders the fork's
    // resolved palette into a <style> so every route has the tokens the class
    // names resolve against, mirroring AuthLayout's <HeadContent> block.
    const html: string = await renderShell();

    expect(html).toContain(renderThemeStyle(forkResolvedBranding));
  });
});

/**
 * The compiled-stylesheet half of the styles-adoption contract (Wallow-w6s6.3.3),
 * ported from wallow-auth's already-solved shell (its `__root.test.tsx`
 * "in production" block). wallow-web shipped WITHOUT this fix — its shell never
 * links `/client.css` at all, so every production/standalone route serves
 * unstyled. These assertions guard the fix once the shell adopts `DocumentStyles`.
 *
 * The prod branch turns on a module-level `import.meta.env.DEV ? null :
 * "/client.css"` const, so exercising it means re-evaluating the shell module
 * under `DEV=false`. The vitest env runs with `DEV=true`, so a fresh prod eval is
 * obtained by stubbing `DEV` off and cache-busting the `__root` import with a
 * unique query — the one mechanism that mints a new module instance whose
 * top-level const re-reads the stubbed env. The router mocks above apply to the
 * re-imported module too (vi.mock keys on the specifier, not the importer).
 */
describe("routes/__root (production stylesheet)", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it("links the compiled stylesheet at /client.css in production", async () => {
    // The production build extracts the entry CSS to the pinned, unhashed name
    // `/client.css` (vite.config.ts `assetFileNames`), and nothing references it
    // from `client.js` — Vite does not auto-inject entry CSS for a JS entry — so
    // unless the shell links it explicitly, every route serves unstyled. This is
    // the missing-`/client.css` bug wallow-web shipped (wallow-auth got the fix in
    // 4c67b243; wallow-web never did).
    vi.stubEnv("DEV", false);
    /* oxlint-disable no-inline-comments -- Vite's @vite-ignore magic comment must stay inline in the import */
    const prodRoot = (await import(
      /* @vite-ignore */ `./__root?prod=${Date.now()}`
    )) as typeof import("./__root");
    /* oxlint-enable no-inline-comments */
    const Shell = prodRoot.Route.options.component!;
    const html: string = renderToString(<Shell />);

    expect(html).toMatch(/<link[^>]*rel="stylesheet"[^>]*href="\/client\.css"/u);
  });

  it("omits the /client.css stylesheet link in dev", async () => {
    // In dev, Vite injects the entry CSS through the JS module graph and
    // `/client.css` does not exist on the dev server, so a link to it would 404 —
    // only the inline theme <style> belongs. The default vitest env is DEV=true,
    // so the standard `renderShell()` exercises the dev branch.
    const html: string = await renderShell();

    expect(html).not.toContain('rel="stylesheet"');
  });
});
