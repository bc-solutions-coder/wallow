import { existsSync, readdirSync } from "node:fs";

import { defineLibraryConfig } from "../../tools/vite/library";

// `preserveModules` keeps one output module per source module so `dist/` mirrors
// `src/` and each component stays addressable at
// `dist/components/<name>/index.js` — the path the package's `"./*"` subpath
// export points at. Every component's folder barrel is also declared as an
// entry: a re-export-only module is otherwise inlined into its importer and no
// `index.js` is emitted for it.
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    index: "src/index.ts",
    ...componentEntries(),
  },
  preserveModules: true,
});

/**
 * Every `src/components/<name>/index.ts`, keyed by the output path it must land
 * at — `entryFileNames: "[name].js"` turns the key into the emitted filename, so
 * `components/button/index` produces `dist/components/button/index.js`.
 */
function componentEntries(): Record<string, string> {
  const componentsDir = new URL("src/components/", import.meta.url);
  const entries: Record<string, string> = {};

  for (const entry of readdirSync(componentsDir, { withFileTypes: true })) {
    if (entry.isDirectory() && existsSync(new URL(`${entry.name}/index.ts`, componentsDir))) {
      entries[`components/${entry.name}/index`] = `src/components/${entry.name}/index.ts`;
    }
  }

  return entries;
}
