import { create } from "zustand";

/**
 * The shell's global nav state.
 *
 * BOUNDARY: nothing fetched from an API may live here. Backend data belongs to
 * TanStack Query (keys from `@bc-solutions-coder/sdk/query`); this store holds
 * presentation state that must survive across components and routes — the
 * controls that flip it live in `AppShell`'s main column while the rail and the
 * drawer that read it are its siblings. Neither passes the flags to the other,
 * which is why this is a store rather than a `useState`.
 *
 * TWO AXES, NOT ONE. The nav has two independent pieces of state that an earlier
 * single `isNavOpen` boolean conflated:
 *
 *   isNavCollapsed   — DESKTOP. Whether the persistent rail is narrowed to
 *                      icons. The rail is always present at desktop widths; this
 *                      only decides whether labels ride alongside the icons.
 *   isMobileNavOpen  — MOBILE. Whether the overlay drawer is showing. Below the
 *                      breakpoint there is no rail at all, so "open" here means
 *                      a temporary sheet over the page, not a widened rail.
 *
 * They must never be derived from each other: opening the mobile drawer says
 * nothing about how the desktop rail should look when the viewport grows back,
 * and collapsing the desktop rail must not leave a drawer hanging open. Keep
 * them separate.
 *
 * The bound store doubles as a React hook; subscribe with a selector
 * (`useNavStore((state) => state.isNavCollapsed)`) so a component only re-renders
 * for the slice it reads.
 *
 * It is a module-global singleton, which is why `zustand` is a PEER dependency
 * and why this module is exported from exactly one entry. Two resolved copies of
 * either would mean two stores, and a rail whose toggle silently stops moving it
 * — the same hazard class `@bc-solutions-coder/query` exists to solve for
 * `QueryClient`. `nav-store.test.ts` pins the identity across a package import.
 */

/** The store's state: presentation flags plus the actions that mutate them. */
export interface NavState {
  /** DESKTOP: whether the rail is narrowed to icons (expanded on first paint). */
  isNavCollapsed: boolean;
  /** Flip the desktop rail between expanded and the icon rail. */
  toggleNavCollapsed: () => void;
  /** MOBILE: whether the overlay drawer is showing (closed on first paint). */
  isMobileNavOpen: boolean;
  /** Show the mobile overlay drawer — bound to the mobile menu button. */
  openMobileNav: () => void;
  /** Hide the mobile overlay drawer — bound to the backdrop, nav links, and Escape. */
  closeMobileNav: () => void;
}

export const useNavStore = create<NavState>()((set) => ({
  isNavCollapsed: false,
  toggleNavCollapsed: () => {
    set((state) => ({ isNavCollapsed: !state.isNavCollapsed }));
  },
  isMobileNavOpen: false,
  openMobileNav: () => {
    set({ isMobileNavOpen: true });
  },
  closeMobileNav: () => {
    set({ isMobileNavOpen: false });
  },
}));
