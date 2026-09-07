import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

/** Build separate entries so Node configuration imports cannot load browser-only helpers. */
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    index: "src/index.ts",
    render: "src/render.tsx",
    "sdk-harness": "src/sdk-harness.ts",
    "browser-deps": "src/browser-deps.ts",
    "browser-mode-smoke": "src/browser-mode-smoke.ts",
    contrast: "src/contrast.ts",
    "render-with-wallow": "src/render-with-wallow.tsx",
    locators: "src/locators.ts",
    "catalog-select": "src/catalog-select.ts",
    invalidation: "src/invalidation.ts",
    "browser-styles-wiring": "src/browser-styles-wiring.ts",
    "theme-wiring": "src/theme-wiring.tsx",
    "navigation-escape": "src/navigation-escape.ts",
    "form-submission": "src/form-submission.ts",
    "router-stub": "src/router-stub.ts",
    "console-guard": "src/console-guard.ts",
    "network-escape": "src/network-escape.ts",
    "node-async-hooks-browser-shim": "src/node-async-hooks-browser-shim.ts",
  },
});
