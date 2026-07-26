import { createSsrViteConfig } from "@bc-solutions-coder/web-shell/server";
import { defineConfig } from "vite";

// Server (SSR) bundle build — the SSR half of `pnpm build`. The shared preset
// bundles `src/ssr.tsx` (resolved against `appDir`) into `dist/server`; the
// standalone host serves the emitted `dist/server/ssr.js`.
export default defineConfig(createSsrViteConfig({ appDir: import.meta.dirname }));
