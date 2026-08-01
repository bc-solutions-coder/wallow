import { describe, expect, it } from "vitest";

import {
  defaultNavControlIcons,
  defaultNavControlLabels,
  type NavControlIcons,
  type NavIconComponent,
} from "./nav-icons";

/**
 * The three CONTROL icons the package ships defaults for. Destination icons are
 * not here — they arrive on each `destinations` entry, so a consumer's manifest
 * is where their distinctness is a fact.
 *
 * The contract is library-AGNOSTIC: nothing below names an icon library, so
 * swapping one must not touch this file.
 */

/** Pre-sorted, so it doubles as the expected key set and the iteration order —
 * iterating the module's own keys would pass vacuously. */
const everyControl: (keyof NavControlIcons)[] = ["close", "mobileMenu", "navToggle"];

describe("default nav control icons", () => {
  it("ships an icon for every control the shell renders", () => {
    expect(Object.keys(defaultNavControlIcons).toSorted()).toStrictEqual(everyControl);
  });

  it("ships an accessible name for every control", () => {
    expect(Object.keys(defaultNavControlLabels).toSorted()).toStrictEqual(everyControl);
  });

  it("resolves each icon to a React component rather than markup or a glyph string", () => {
    for (const control of everyControl) {
      const icon: unknown = defaultNavControlIcons[control];

      // A component is either a function or a wrapper object (forwardRef/memo);
      // a string here would mean an icon font or a raw SVG blob, which the
      // per-icon tree-shaking requirement rules out.
      expect(typeof icon, `icon "${control}"`).toMatch(/^(function|object)$/u);
      expect(icon, `icon "${control}"`).not.toBeNull();
    }
  });

  it("names every control with non-empty accessible text", () => {
    for (const control of everyControl) {
      expect((defaultNavControlLabels[control] ?? "").trim(), `label for "${control}"`).not.toBe(
        "",
      );
    }
  });

  it("gives each control its own icon", () => {
    // The desktop toggle collapses a rail that stays on screen; the mobile
    // button summons a drawer that is not there. One glyph for both would say
    // they do the same thing.
    const icons: NavIconComponent[] = everyControl.map(
      (control) => defaultNavControlIcons[control],
    );

    expect(new Set(icons).size).toBe(everyControl.length);
  });

  it("gives each control its own accessible name", () => {
    const labels: string[] = everyControl.map((control) => defaultNavControlLabels[control]);

    expect(new Set(labels).size).toBe(everyControl.length);
  });
});
