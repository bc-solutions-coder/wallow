import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

// A config-safe `.` barrel (plus `sdk-harness`, which imports no browser-only
// module) alongside the browser-only subpaths, each with its own named entry so
// a consumer loading this package at config time in plain Node never pulls the
// browser modules into the graph.
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    index: "src/index.ts",
    render: "src/render.tsx",
    "sdk-harness": "src/sdk-harness.ts",
    "browser-deps": "src/browser-deps.ts",
    contrast: "src/contrast.ts",
    "render-with-wallow": "src/render-with-wallow.tsx",
    locators: "src/locators.ts",
    "catalog-select": "src/catalog-select.ts",
    invalidation: "src/invalidation.ts",
    "browser-styles-wiring": "src/browser-styles-wiring.ts",
    "theme-wiring": "src/theme-wiring.tsx",
    "node-async-hooks-browser-shim": "src/node-async-hooks-browser-shim.ts",
  },
});
