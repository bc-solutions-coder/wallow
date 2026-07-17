import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// Server bundle build for wallow-web (Wallow-ffpq.3.2).
//
// This is the SSR half of `pnpm build`, split out of vite.config.ts when the
// browser bundle took over the default build (Vite builds one environment per
// invocation, and a second config file is the least surprising way to ask for
// the second — a `--mode` flag would also flip `import.meta.env.DEV`, which the
// document shell reads to pick the client entry path).
//
// It bundles the render entry (`src/ssr.tsx`) and, with it, the whole render
// tree — router, root shell, routes — so `pnpm build` proves the server-render
// pipeline compiles. The production SSR host (server.ts, Wallow-ffpq.3.3) serves
// the emitted `dist/server` artefact.
export default defineConfig({
  plugins: [react()],
  build: {
    ssr: "src/ssr.tsx",
    outDir: "dist/server",
    emptyOutDir: true,
  },
});
