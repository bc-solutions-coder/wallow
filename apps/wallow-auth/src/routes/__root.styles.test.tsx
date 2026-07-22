import type { DocumentStylesProps } from "@bc-solutions-coder/ui";
import { renderToString } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";

import { forkResolvedBranding, renderThemeStyle } from "../lib/branding";

/**
 * The document shell's half of the styling-centralization contract
 * (Wallow-w6s6.3.2): the hand-written theme `<style>` + stylesheet `<link>` pair
 * in `__root.tsx` must be replaced with the shared `<DocumentStyles/>` component
 * from `@bc-solutions-coder/ui`, so both apps deliver styles through one
 * implementation instead of each shell re-deriving the block (the drift that let
 * wallow-web ship without a `/client.css` link).
 *
 * These specs assert the DELEGATION — that the shell actually renders
 * `DocumentStyles`, handing it the fork's serialized theme CSS and the dev-branch
 * `stylesheetHref` (`null`, since the vitest env runs with `DEV=true`). The
 * markup-level regressions (theme `<style>` present, no dev stylesheet link, prod
 * `/client.css` link) stay owned by `__root.test.tsx` and must remain green
 * across the swap.
 *
 * `DocumentStyles` is wrapped in a spy that still delegates to the real
 * component, so the rendered head is unchanged while the call is observable.
 * `FocusOnNavigate` throws under a bare `renderToString(<Shell/>)` with no
 * `RouterProvider` (its `useRouterState` needs router context), so it is a
 * render-nothing sentinel here; `Outlet` and `ReadyIndicator` are stubbed for the
 * same reason — the point is the head the shell delegates, not those three.
 */
const { documentStylesSpy } = vi.hoisted(() => ({
  documentStylesSpy: vi.fn<(props: DocumentStylesProps) => void>(),
}));

vi.mock("@bc-solutions-coder/ui", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/ui")>();
  return {
    ...actual,
    FocusOnNavigate: () => null,
    DocumentStyles: (props: DocumentStylesProps) => {
      documentStylesSpy(props);
      // oxlint-disable-next-line new-cap -- DocumentStyles is a React component invoked as a function in this mock
      return actual.DocumentStyles(props);
    },
  };
});

vi.mock("@tanstack/react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@tanstack/react-router")>();
  return {
    ...actual,
    Outlet: () => <div data-testid="router-outlet" />,
  };
});

vi.mock("../components/ready-indicator", () => ({
  ReadyIndicator: () => <div data-testid="auth-ready-indicator" />,
}));

async function renderShell(): Promise<string> {
  const { Route } = await import("./__root");
  const Shell = Route.options.component!;
  return renderToString(<Shell />);
}

describe("routes/__root (styling delivery)", () => {
  it("delegates head styles to the shared DocumentStyles component", async () => {
    // The refactor's core: the shell must render `<DocumentStyles/>` rather than
    // hand-writing its own `<style>` + `<link>` pair. A shell that still inlines
    // the block never invokes this component.
    documentStylesSpy.mockClear();

    await renderShell();

    expect(documentStylesSpy).toHaveBeenCalledTimes(1);
  });

  it("hands DocumentStyles the fork theme css and the dev stylesheet branch", async () => {
    // The app shell — not the library — owns the dev/prod choice
    // (`import.meta.env.DEV ? null : "/client.css"`) and the theme serialization,
    // passing both in as props. Under the vitest env `DEV` is true, so the
    // stylesheet href resolves to `null` (dev branch, no `/client.css` link).
    documentStylesSpy.mockClear();

    await renderShell();

    expect(documentStylesSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        themeCss: renderThemeStyle(forkResolvedBranding),
        stylesheetHref: null,
      }),
    );
  });

  it("still renders the theme <style> and omits the dev stylesheet link through the delegation", async () => {
    // Regression guard: delegating to DocumentStyles must not change the head
    // markup — the fork's theme tokens still land in a `<style>`, and the dev
    // branch still links no stylesheet (`/client.css` does not exist on the dev
    // server). The production-link assertion stays in `__root.test.tsx`.
    const html: string = await renderShell();

    expect(html).toContain(renderThemeStyle(forkResolvedBranding));
    expect(html).not.toContain('rel="stylesheet"');
  });
});
