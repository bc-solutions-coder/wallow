import { beforeEach, describe, expect, it } from "vitest";

import { useUiStore } from "./ui-store";

/**
 * The store holds UI-only state more than one component needs; everything
 * fetched from the API belongs to TanStack Query instead.
 *
 * The nav has TWO independent axes — `isNavCollapsed` is the desktop rail's
 * width, `isMobileNavOpen` the mobile overlay drawer — and acting on one must
 * leave the other where it was, hence a "does not disturb the other axis" case
 * per axis. The store is a module-scope singleton, so every test resets both.
 */
describe("ui store", () => {
  beforeEach(() => {
    useUiStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
  });

  describe("desktop rail (isNavCollapsed)", () => {
    it("starts expanded so labels are visible on first paint", () => {
      expect(useUiStore.getState().isNavCollapsed).toBe(false);
    });

    it("collapses the rail to icons on toggleNavCollapsed", () => {
      useUiStore.getState().toggleNavCollapsed();

      expect(useUiStore.getState().isNavCollapsed).toBe(true);
    });

    it("returns to expanded when toggleNavCollapsed is called twice", () => {
      useUiStore.getState().toggleNavCollapsed();
      useUiStore.getState().toggleNavCollapsed();

      expect(useUiStore.getState().isNavCollapsed).toBe(false);
    });

    it("re-expands a collapsed rail on toggleNavCollapsed", () => {
      useUiStore.setState({ isNavCollapsed: true });

      useUiStore.getState().toggleNavCollapsed();

      expect(useUiStore.getState().isNavCollapsed).toBe(false);
    });

    it("does not open the mobile drawer when the rail collapses", () => {
      useUiStore.getState().toggleNavCollapsed();

      expect(useUiStore.getState().isMobileNavOpen).toBe(false);
    });

    it("leaves an open mobile drawer open when the rail collapses", () => {
      useUiStore.setState({ isMobileNavOpen: true });

      useUiStore.getState().toggleNavCollapsed();

      expect(useUiStore.getState().isMobileNavOpen).toBe(true);
    });
  });

  describe("mobile drawer (isMobileNavOpen)", () => {
    it("starts closed so the page is not covered on first paint", () => {
      expect(useUiStore.getState().isMobileNavOpen).toBe(false);
    });

    it("shows the drawer on openMobileNav", () => {
      useUiStore.getState().openMobileNav();

      expect(useUiStore.getState().isMobileNavOpen).toBe(true);
    });

    it("leaves an already-open drawer open on openMobileNav", () => {
      useUiStore.setState({ isMobileNavOpen: true });

      useUiStore.getState().openMobileNav();

      expect(useUiStore.getState().isMobileNavOpen).toBe(true);
    });

    it("hides an open drawer on closeMobileNav", () => {
      useUiStore.setState({ isMobileNavOpen: true });

      useUiStore.getState().closeMobileNav();

      expect(useUiStore.getState().isMobileNavOpen).toBe(false);
    });

    it("leaves an already-closed drawer closed on closeMobileNav", () => {
      useUiStore.getState().closeMobileNav();

      expect(useUiStore.getState().isMobileNavOpen).toBe(false);
    });

    it("does not collapse the desktop rail when the drawer opens", () => {
      useUiStore.getState().openMobileNav();

      expect(useUiStore.getState().isNavCollapsed).toBe(false);
    });

    it("does not expand a collapsed desktop rail when the drawer closes", () => {
      useUiStore.setState({ isNavCollapsed: true, isMobileNavOpen: true });

      useUiStore.getState().closeMobileNav();

      expect(useUiStore.getState().isNavCollapsed).toBe(true);
    });
  });

  describe("store plumbing", () => {
    it("notifies subscribers when the desktop rail changes", () => {
      const seen: boolean[] = [];
      const unsubscribe: () => void = useUiStore.subscribe((state) => {
        seen.push(state.isNavCollapsed);
      });

      useUiStore.getState().toggleNavCollapsed();
      useUiStore.getState().toggleNavCollapsed();
      unsubscribe();
      useUiStore.getState().toggleNavCollapsed();

      expect(seen).toStrictEqual([true, false]);
    });

    it("notifies subscribers when the mobile drawer changes", () => {
      const seen: boolean[] = [];
      const unsubscribe: () => void = useUiStore.subscribe((state) => {
        seen.push(state.isMobileNavOpen);
      });

      useUiStore.getState().openMobileNav();
      useUiStore.getState().closeMobileNav();
      unsubscribe();
      useUiStore.getState().openMobileNav();

      expect(seen).toStrictEqual([true, false]);
    });

    it("shares one instance across importers rather than a per-import factory", async () => {
      useUiStore.getState().toggleNavCollapsed();

      const reimported = await import("./ui-store");

      expect(reimported.useUiStore.getState().isNavCollapsed).toBe(true);
    });

    it("holds UI-only state — no API data, and no re-conflated nav boolean", () => {
      // Two things at once. The state boundary: a server-data key here must
      // fail, because backend data belongs to TanStack Query. And the two-axis
      // split: a single `isNavOpen`/`toggleNav`/`closeNav` trio would drive the
      // desktop rail and the mobile drawer off one boolean.
      expect(Object.keys(useUiStore.getState()).toSorted()).toStrictEqual([
        "closeMobileNav",
        "isMobileNavOpen",
        "isNavCollapsed",
        "openMobileNav",
        "toggleNavCollapsed",
      ]);
    });
  });
});
