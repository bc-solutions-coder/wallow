import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// Server bundle build for wallow-auth (Wallow-vec7.1.5).
//
// This is the SSR half of `pnpm build`, split out of vite.config.ts when the
// browser bundle took over the default build (Vite builds one environment per
// invocation, and a second config file is the least surprising way to ask for
// the second — a `--mode` flag would also flip `import.meta.env.DEV`, which the
// document shell reads to pick the client entry path).
//
// It bundles the render entry (`src/ssr.tsx`) and, with it, the whole render
// tree — router, root shell, routes, branding — so `pnpm build` proves the
// server-render pipeline compiles. Nothing consumes `dist/server` yet: the
// standalone host (server.ts) is proxy-only this phase and the dev server loads
// `src/ssr.tsx` through Vite. It is a build-time check today and the artefact
// the standalone SSR host will serve once that phase lands.
export default defineConfig({
  plugins: [react()],
  build: {
    ssr: "src/ssr.tsx",
    outDir: "dist/server",
    emptyOutDir: true,
  },
});
