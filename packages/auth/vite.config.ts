import { defineLibraryConfig } from "../../tools/vite/library";

// One browser-safe `.` barrel: the shared authn/authz surface.
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: { index: "src/index.ts" },
});
