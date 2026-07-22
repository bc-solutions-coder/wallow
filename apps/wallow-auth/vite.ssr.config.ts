import { createSsrViteConfig } from "@bc-solutions-coder/web-shell/server";
import { defineConfig } from "vite";

// Server bundle build for wallow-auth (Wallow-vec7.1.5, migrated onto the shared
// web-shell preset in Wallow-0q2s.8.5).
//
// This is the SSR half of `pnpm build`. The preset (createSsrViteConfig) bundles
// `src/ssr.tsx` (resolved against `appDir`) into `dist/server` with a react-only
// plugin set. The standalone host serves the emitted `dist/server/ssr.js`.
export default defineConfig(createSsrViteConfig({ appDir: import.meta.dirname }));
