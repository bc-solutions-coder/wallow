import { assertThemeWiring } from "@bc-solutions-coder/testing/theme-wiring";

/**
 * The Tailwind utilities and the fork theme are both present in this app's
 * Vitest BROWSER project.
 *
 * Every screen here paints onto a card, so `--card` is the surface that matters
 * alongside the page `--background` and its `--foreground` pair.
 */
assertThemeWiring({
  tokens: ["--background", "--foreground", "--card"],
  probeClass: "bg-card text-card-foreground",
});
