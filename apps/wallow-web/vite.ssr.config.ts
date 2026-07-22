import { createSsrViteConfig } from "@bc-solutions-coder/web-shell/server";
import { defineConfig } from "vite";

// Server bundle build for wallow-web (Wallow-ffpq.3.2, migrated onto the shared
// web-shell preset in Wallow-0q2s.8.5).
//
// This is the SSR half of `pnpm build`. The preset (createSsrViteConfig) bundles
// `src/ssr.tsx` (resolved against `appDir`) into `dist/server` with a react-only
// plugin set. The production SSR host (server.ts, Wallow-ffpq.3.3) serves the
// emitted `dist/server` artefact.
export default defineConfig(createSsrViteConfig({ appDir: import.meta.dirname }));
