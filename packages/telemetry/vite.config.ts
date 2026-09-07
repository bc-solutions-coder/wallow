import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";
import { mergeConfig } from "vite";

export default mergeConfig(
  defineLibraryConfig({
    configUrl: import.meta.url,
    entries: { index: "src/index.ts", server: "src/server.ts" },
  }),
  { build: { sourcemap: false, rolldownOptions: { external: /^node:/u } } },
);
