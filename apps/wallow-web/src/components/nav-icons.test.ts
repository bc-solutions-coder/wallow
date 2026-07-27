import { describe, expect, it } from "vitest";

import { navIconLabels, navIcons, type NavIconComponent, type NavIconName } from "./nav-icons";

/**
 * Nav icon-set spec (Wallow-0byr.1) — pure logic (it inspects a map, it never
 * mounts anything), so it runs on the vitest NODE project (`src/**\/*.test.ts`)
 * and stays out of Chromium.
 *
 * The contract these tests pin is deliberately library-AGNOSTIC: they assert
 * that every nav destination and nav control resolves to a distinct icon
 * component under a distinct accessible name, never which library the icon came
 * from. Swapping icon libraries must not touch this file.
 */

/** The nav destinations — the items a user navigates with, minus the controls. */
const destinations: NavIconName[] = ["organizations", "apps", "settings", "inquiries", "signOut"];

/**
 * Every icon the nav owns: the destinations plus the toggle/menu/close controls,
 * pre-sorted so it doubles as the expected key set and as the iteration order for
 * the per-icon checks (iterating the module's own keys would pass vacuously).
 */
const everyIconName: NavIconName[] = [
  "apps",
  "close",
  "inquiries",
  "mobileMenu",
  "navToggle",
  "organizations",
  "settings",
  "signOut",
];

describe("nav icons", () => {
  it("exposes an icon for every nav destination and nav control", () => {
    expect(Object.keys(navIcons).toSorted()).toStrictEqual(everyIconName);
  });

  it("exposes an accessible name for every icon", () => {
    expect(Object.keys(navIconLabels).toSorted()).toStrictEqual(everyIconName);
  });

  it("resolves each icon to a React component rather than markup or a glyph string", () => {
    for (const name of everyIconName) {
      const icon: unknown = navIcons[name];

      // A component is either a function or a wrapper object (forwardRef/memo);
      // a string here would mean an icon font or a raw SVG blob, which the
      // per-icon tree-shaking requirement rules out.
      expect(typeof icon, `icon "${name}"`).toMatch(/^(function|object)$/u);
      expect(icon, `icon "${name}"`).not.toBeNull();
    }
  });

  it("names every icon with non-empty accessible text", () => {
    for (const name of everyIconName) {
      expect((navIconLabels[name] ?? "").trim(), `label for "${name}"`).not.toBe("");
    }
  });

  it("gives each nav destination its own icon so the rail stays readable", () => {
    const icons: NavIconComponent[] = destinations.map((name) => navIcons[name]);

    expect(new Set(icons).size).toBe(destinations.length);
  });

  it("gives each nav destination its own accessible name", () => {
    const labels: string[] = destinations.map((name) => navIconLabels[name]);

    expect(new Set(labels).size).toBe(destinations.length);
  });
});
