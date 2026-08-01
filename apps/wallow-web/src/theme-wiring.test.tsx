import { assertThemeWiring } from "@bc-solutions-coder/testing/theme-wiring";

/**
 * The Tailwind utilities and the fork theme are both present in this app's
 * Vitest BROWSER project.
 *
 * `--background` covers the app surface; `--sidebar` and `--foreground` are the
 * two halves of the nav contrast pair.
 */
assertThemeWiring({
  tokens: ["--background", "--foreground", "--sidebar"],
  probeClass: "bg-sidebar text-sidebar-foreground",
});
