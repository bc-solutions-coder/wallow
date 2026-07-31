import { defineLibraryConfig } from "../../tools/vite/library";

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
    contrast: "src/contrast.ts",
    "render-with-wallow": "src/render-with-wallow.tsx",
  },
});
