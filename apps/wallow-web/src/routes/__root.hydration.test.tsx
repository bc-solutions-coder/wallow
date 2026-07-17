import { renderToString } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";

/**
 * The document shell's half of the hydration contract (Wallow-ffpq.3.1). For the
 * app to hydrate at all the SSR shell must (1) load the client bundle with a
 * `<script type="module">` in the head, and (2) mount the `ReadyIndicator` so
 * every route emits the `data-app-ready` signal once interactive. The current
 * shell does neither, which is why the SSR'd HTML is inert.
 *
 * TanStack's `<Outlet/>` cannot render outside a `RouterProvider`, so it is
 * replaced with a sentinel. `ReadyIndicator` renders nothing of its own (its
 * signal is a post-commit `document.body` effect with no SSR markup), so it too
 * is mocked with a sentinel — the point here is to prove the shell *renders* it,
 * not to exercise its effect (that is `ready-indicator.test.tsx`'s job).
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

async function renderShell(): Promise<string> {
  const { Route } = await import("./__root");
  const Shell = Route.options.component!;
  return renderToString(<Shell />);
}

describe("routes/__root (hydration wiring)", () => {
  it("loads the client bundle with a module script in the head", async () => {
    const html: string = await renderShell();

    const script: RegExpMatchArray | null = html.match(/<script\b[^>]*>/u);
    if (script === null) {
      throw new Error("shell rendered no <script> tag, so no client bundle loads");
    }
    expect(script[0]).toContain('type="module"');
    // The entry is `/src/client.tsx` in dev and `/client.js` in a build; either
    // way the src points at the client bundle this task is introducing.
    expect(script[0]).toMatch(/src="[^"]*client(\.tsx|\.js)?"/u);
  });

  it("mounts the ReadyIndicator so every route emits the ready signal", async () => {
    const html: string = await renderShell();

    expect(html).toContain('data-testid="web-ready-indicator"');
  });
});
