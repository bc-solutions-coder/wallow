import type { NavDestination } from "@bc-solutions-coder/navigation";
import { describe, expect, it } from "vitest";

import { ADMIN_ROLE, dashboardDestinations } from "./dashboard-destinations";

/**
 * The dashboard's nav manifest as data: one distinct icon and one distinct name
 * per destination, and exactly one admin gate.
 *
 * The contract is library-AGNOSTIC — nothing here names an icon library, so
 * swapping one must not touch this file. What the shell DOES with the manifest
 * belongs to `@bc-solutions-coder/navigation`; `DashboardLayout.test.tsx` covers
 * the rendered result.
 */

/** The manifest's entries in render order — the expected `id` set and iteration order. */
const EXPECTED_IDS: readonly string[] = [
  "nav-organizations",
  "nav-invitations",
  "nav-my-organizations",
  "nav-apps",
  "nav-settings",
  "nav-inquiries",
];

describe("the dashboard nav manifest", () => {
  it("declares its destinations in render order", () => {
    // Iterating the module's own ids would let a dropped destination pass.
    expect(dashboardDestinations.map((entry: NavDestination): string => entry.id)).toStrictEqual(
      EXPECTED_IDS,
    );
  });

  it("routes each destination under /dashboard", () => {
    for (const entry of dashboardDestinations) {
      expect(entry.to, `destination "${entry.id}"`).toMatch(/^\/dashboard\//u);
    }
  });

  it("resolves each icon to a React component rather than markup or a glyph string", () => {
    for (const entry of dashboardDestinations) {
      // A component is either a function or a wrapper object (forwardRef/memo);
      // a string here would mean an icon font or a raw SVG blob, which the
      // per-icon tree-shaking requirement rules out.
      expect(typeof entry.icon, `icon for "${entry.id}"`).toMatch(/^(function|object)$/u);
      expect(entry.icon, `icon for "${entry.id}"`).not.toBeNull();
    }
  });

  it("names every destination with non-empty accessible text", () => {
    for (const entry of dashboardDestinations) {
      expect(entry.label.trim(), `label for "${entry.id}"`).not.toBe("");
    }
  });

  it("gives each destination its own icon so the collapsed rail stays readable", () => {
    // In the icon rail the glyph is the ONLY thing distinguishing two rows.
    const icons = dashboardDestinations.map((entry: NavDestination) => entry.icon);

    expect(new Set(icons).size).toBe(dashboardDestinations.length);
  });

  it("gives each destination its own accessible name", () => {
    const labels: string[] = dashboardDestinations.map((entry: NavDestination) => entry.label);

    expect(new Set(labels).size).toBe(dashboardDestinations.length);
  });

  it("gates Organizations and Invitations, and only those, behind the admin role", () => {
    const gated: NavDestination[] = dashboardDestinations.filter(
      (entry: NavDestination): boolean => entry.requires !== undefined,
    );

    expect(gated.map((entry: NavDestination): string => entry.id)).toStrictEqual([
      "nav-organizations",
      "nav-invitations",
    ]);
    for (const entry of gated) {
      expect(entry.requires?.role, `role for "${entry.id}"`).toBe(ADMIN_ROLE);
    }
  });
});
