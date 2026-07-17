/** @vitest-environment jsdom */
import { describe, expect, it, vi } from "vitest";

/**
 * The browser entry contract (Wallow-ffpq.3.1). wallow-web currently ships SSR
 * HTML with no client bundle, so nothing hydrates and every form is inert
 * outside `pnpm dev` — the 251 jsdom component tests mount components directly
 * and bypass this missing path entirely, giving false-green signal.
 *
 * This suite pins the one thing those tests cannot: importing the browser entry
 * must hydrate the whole document exactly once. `hydrateRoot` is spied through a
 * module mock, and `./router` is stubbed so importing the entry does not have to
 * build the real route tree — the assertion is purely about the hydration call.
 */
const hydrateRoot = vi.fn();

vi.mock("react-dom/client", () => ({ hydrateRoot }));
vi.mock("./router", () => ({ createRouter: (): object => ({}) }));

describe("the wallow-web browser entry", () => {
  it("hydrates the whole document exactly once on load", async () => {
    await import("./client");

    // Whole-document hydration (`hydrateRoot(document, ...)`), not a mount node:
    // the root route renders the entire `<html>` shell, so the client and server
    // trees must agree on the same root.
    expect(hydrateRoot).toHaveBeenCalledOnce();
    expect(hydrateRoot.mock.calls[0]?.[0]).toBe(document);
  });
});
