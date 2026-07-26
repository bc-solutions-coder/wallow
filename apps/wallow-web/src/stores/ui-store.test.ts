import { beforeEach, describe, expect, it } from "vitest";

import { useUiStore } from "./ui-store";

/**
 * UI store spec (Wallow-evd5.4.1) — pure logic, so it runs on the vitest NODE
 * project (`src/**\/*.test.ts`), never in Chromium.
 *
 * The store is the wallow-web side of the epic's state boundary: TanStack Query
 * owns everything fetched from the API, and this store owns UI-only state that
 * more than one component needs. The dashboard nav drawer is that state — the
 * toggle lives in `DashboardLayout`, the drawer itself in `DashboardNav`.
 *
 * The store is a module-scope singleton, so every test resets it first.
 */
describe("ui store", () => {
  beforeEach(() => {
    useUiStore.setState({ isNavOpen: false });
  });

  it("starts with the nav drawer closed", () => {
    expect(useUiStore.getState().isNavOpen).toBe(false);
  });

  it("opens the nav drawer on toggleNav", () => {
    useUiStore.getState().toggleNav();

    expect(useUiStore.getState().isNavOpen).toBe(true);
  });

  it("returns to closed when toggleNav is called twice", () => {
    useUiStore.getState().toggleNav();
    useUiStore.getState().toggleNav();

    expect(useUiStore.getState().isNavOpen).toBe(false);
  });

  it("closes an open nav drawer on closeNav", () => {
    useUiStore.setState({ isNavOpen: true });

    useUiStore.getState().closeNav();

    expect(useUiStore.getState().isNavOpen).toBe(false);
  });

  it("leaves an already-closed nav drawer closed on closeNav", () => {
    useUiStore.getState().closeNav();

    expect(useUiStore.getState().isNavOpen).toBe(false);
  });

  it("notifies subscribers when the nav drawer changes", () => {
    const seen: boolean[] = [];
    const unsubscribe: () => void = useUiStore.subscribe((state) => {
      seen.push(state.isNavOpen);
    });

    useUiStore.getState().toggleNav();
    useUiStore.getState().closeNav();
    unsubscribe();
    useUiStore.getState().toggleNav();

    expect(seen).toStrictEqual([true, false]);
  });

  it("shares one instance across importers rather than a per-import factory", async () => {
    useUiStore.getState().toggleNav();

    const reimported = await import("./ui-store");

    expect(reimported.useUiStore.getState().isNavOpen).toBe(true);
  });

  it("holds UI-only state — no API data may leak into the store", () => {
    // The state boundary, pinned: anything fetched from the backend belongs to
    // TanStack Query. Adding a server-data key here should fail this test and
    // force the discussion (see docs/development/frontend-state.md).
    expect(Object.keys(useUiStore.getState()).toSorted()).toStrictEqual([
      "closeNav",
      "isNavOpen",
      "toggleNav",
    ]);
  });
});
