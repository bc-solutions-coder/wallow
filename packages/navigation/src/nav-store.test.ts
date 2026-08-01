import { beforeEach, describe, expect, it } from "vitest";

import { useNavStore as entryUseNavStore } from "./index";
import { useNavStore } from "./nav-store";

/**
 * The shell's nav state: UI-only, and TWO independent axes — `isNavCollapsed` is
 * the desktop rail's width, `isMobileNavOpen` the mobile overlay drawer — so
 * acting on one must leave the other where it was, hence a "does not disturb the
 * other axis" case per axis. The store is a module-scope singleton, so every
 * test resets both.
 */
describe("nav store", () => {
  beforeEach(() => {
    useNavStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
  });

  describe("desktop rail (isNavCollapsed)", () => {
    it("starts expanded so labels are visible on first paint", () => {
      expect(useNavStore.getState().isNavCollapsed).toBe(false);
    });

    it("collapses the rail to icons on toggleNavCollapsed", () => {
      useNavStore.getState().toggleNavCollapsed();

      expect(useNavStore.getState().isNavCollapsed).toBe(true);
    });

    it("returns to expanded when toggleNavCollapsed is called twice", () => {
      useNavStore.getState().toggleNavCollapsed();
      useNavStore.getState().toggleNavCollapsed();

      expect(useNavStore.getState().isNavCollapsed).toBe(false);
    });

    it("re-expands a collapsed rail on toggleNavCollapsed", () => {
      useNavStore.setState({ isNavCollapsed: true });

      useNavStore.getState().toggleNavCollapsed();

      expect(useNavStore.getState().isNavCollapsed).toBe(false);
    });

    it("does not open the mobile drawer when the rail collapses", () => {
      useNavStore.getState().toggleNavCollapsed();

      expect(useNavStore.getState().isMobileNavOpen).toBe(false);
    });

    it("leaves an open mobile drawer open when the rail collapses", () => {
      useNavStore.setState({ isMobileNavOpen: true });

      useNavStore.getState().toggleNavCollapsed();

      expect(useNavStore.getState().isMobileNavOpen).toBe(true);
    });
  });

  describe("mobile drawer (isMobileNavOpen)", () => {
    it("starts closed so the page is not covered on first paint", () => {
      expect(useNavStore.getState().isMobileNavOpen).toBe(false);
    });

    it("shows the drawer on openMobileNav", () => {
      useNavStore.getState().openMobileNav();

      expect(useNavStore.getState().isMobileNavOpen).toBe(true);
    });

    it("leaves an already-open drawer open on openMobileNav", () => {
      useNavStore.setState({ isMobileNavOpen: true });

      useNavStore.getState().openMobileNav();

      expect(useNavStore.getState().isMobileNavOpen).toBe(true);
    });

    it("hides an open drawer on closeMobileNav", () => {
      useNavStore.setState({ isMobileNavOpen: true });

      useNavStore.getState().closeMobileNav();

      expect(useNavStore.getState().isMobileNavOpen).toBe(false);
    });

    it("leaves an already-closed drawer closed on closeMobileNav", () => {
      useNavStore.getState().closeMobileNav();

      expect(useNavStore.getState().isMobileNavOpen).toBe(false);
    });

    it("does not collapse the desktop rail when the drawer opens", () => {
      useNavStore.getState().openMobileNav();

      expect(useNavStore.getState().isNavCollapsed).toBe(false);
    });

    it("does not expand a collapsed desktop rail when the drawer closes", () => {
      useNavStore.setState({ isNavCollapsed: true, isMobileNavOpen: true });

      useNavStore.getState().closeMobileNav();

      expect(useNavStore.getState().isNavCollapsed).toBe(true);
    });
  });

  describe("store plumbing", () => {
    it("notifies subscribers when the desktop rail changes", () => {
      const seen: boolean[] = [];
      const unsubscribe: () => void = useNavStore.subscribe((state) => {
        seen.push(state.isNavCollapsed);
      });

      useNavStore.getState().toggleNavCollapsed();
      useNavStore.getState().toggleNavCollapsed();
      unsubscribe();
      useNavStore.getState().toggleNavCollapsed();

      expect(seen).toStrictEqual([true, false]);
    });

    it("notifies subscribers when the mobile drawer changes", () => {
      const seen: boolean[] = [];
      const unsubscribe: () => void = useNavStore.subscribe((state) => {
        seen.push(state.isMobileNavOpen);
      });

      useNavStore.getState().openMobileNav();
      useNavStore.getState().closeMobileNav();
      unsubscribe();
      useNavStore.getState().openMobileNav();

      expect(seen).toStrictEqual([true, false]);
    });

    it("is the same store a consumer gets from the package entry", () => {
      // Identity, not behaviour: a consumer importing the package and the shell
      // reading this module must hold ONE store. An entry that re-created or
      // wrapped it would pass every case above while a fork's toggle silently
      // stopped moving the rail.
      expect(entryUseNavStore).toBe(useNavStore);
    });

    it("shares one instance across importers rather than a per-import factory", async () => {
      useNavStore.getState().toggleNavCollapsed();

      const reimported = await import("./nav-store");

      expect(reimported.useNavStore.getState().isNavCollapsed).toBe(true);
    });

    it("holds UI-only state — no API data, and no re-conflated nav boolean", () => {
      // Two things at once. The state boundary: a server-data key here must
      // fail, because backend data belongs to TanStack Query. And the two-axis
      // split: a single `isNavOpen`/`toggleNav`/`closeNav` trio would drive the
      // desktop rail and the mobile drawer off one boolean.
      expect(Object.keys(useNavStore.getState()).toSorted()).toStrictEqual([
        "closeMobileNav",
        "isMobileNavOpen",
        "isNavCollapsed",
        "openMobileNav",
        "toggleNavCollapsed",
      ]);
    });
  });
});
