import { defineLibraryConfig } from "../../tools/vite/library";

// One browser-safe `.` barrel: the TanStack Query facade. Externalizing every
// non-relative import — the shared preset's job — is the whole point here. A
// bundled copy of @tanstack/react-query would hand consumers a second instance
// with its own QueryClientProvider context, which is exactly what this package
// exists to prevent.
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: { index: "src/index.ts" },
});
