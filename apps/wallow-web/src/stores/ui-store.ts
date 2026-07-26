import { create } from "zustand";

/**
 * Global UI-ONLY state for wallow-web (Wallow-evd5.4.1).
 *
 * BOUNDARY: nothing fetched from the API may live here. Backend data belongs to
 * TanStack Query (keys from `@bc-solutions-coder/sdk/query`); this store holds
 * presentation state that must survive across components and routes — today the
 * dashboard nav drawer, which is toggled from `DashboardLayout` and rendered by
 * its sibling `DashboardNav`. Neither passes the flag to the other, which is why
 * this is a store rather than a `useState`.
 *
 * The bound store doubles as a React hook; subscribe with a selector
 * (`useUiStore((state) => state.isNavOpen)`) so a component only re-renders for
 * the slice it reads.
 */

/** The store's state: presentation flags plus the actions that mutate them. */
export interface UiState {
  /** Whether the dashboard nav drawer is expanded (collapsed on first paint). */
  isNavOpen: boolean;
  /** Flip the drawer open/closed — bound to the layout's nav toggle button. */
  toggleNav: () => void;
  /** Force the drawer closed — bound to the layout's backdrop. */
  closeNav: () => void;
}

export const useUiStore = create<UiState>()((set) => ({
  isNavOpen: false,
  toggleNav: () => {
    set((state) => ({ isNavOpen: !state.isNavOpen }));
  },
  closeNav: () => {
    set({ isNavOpen: false });
  },
}));
