import { createClientViteConfig } from "@bc-solutions-coder/web-shell/server";
import { defineConfig } from "vite";

// Client bundle build for wallow-web (Wallow-ffpq.3.2, migrated onto the shared
// web-shell preset in Wallow-0q2s.8.5).
//
// The client-bundle preset (createClientViteConfig) owns the whole config: the
// react() + wallowStyles() plugin set and the stable, unhashed `client.js` output
// contract (dist/client) the document shell and standalone host depend on. The
// only per-app knob is `appDir`, against which the `src/client.tsx` entry resolves.
//
// The dev server (dev-server.ts) does NOT use this file — it drives Vite in
// middlewareMode with its own inline plugins. So this config only governs
// `vite build`.
export default defineConfig(createClientViteConfig({ appDir: import.meta.dirname }));
