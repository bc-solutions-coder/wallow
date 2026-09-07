import { create } from "zustand";

/**
 * Shared presentation state for the desktop rail and mobile drawer. The flags are independent and
 * are not persisted. The bound store is also a React hook; use a selector to subscribe to a
 * single value. Avoid changing this module-global store during SSR requests.
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

/**
 * Subscribe to or update the shared navigation state. Defaults to an expanded desktop rail and a
 * closed mobile drawer. The store is shared by all AppShell instances in the same module graph.
 */
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
