import { defineLibraryConfig } from "../../tools/vite/library";

// The CSS entry (styles.css) is deliberately NOT part of this build. It ships
// as-authored and is consumed through the "./styles.css" export: Tailwind v4 is
// CSS-first, so the consuming app's own Tailwind pass resolves the
// `@import "tailwindcss"` and applies its own `@source` scanning. Pre-building
// it here would bake in this package's (empty) source scan instead.
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    index: "src/index.ts",
    assets: "src/assets.ts",
    vite: "src/vite.ts",
  },
});
