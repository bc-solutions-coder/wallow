import { createClientViteConfig } from "@bc-solutions-coder/web-shell/server";
import { defineConfig } from "vite";

// Browser bundle build. The shared web-shell preset owns the whole config: the
// react() + wallowStyles() plugin set and the stable, unhashed `client.js`
// output (dist/client) the document shell and standalone host depend on. The
// only per-app knob is `appDir`, against which the `src/client.tsx` entry
// resolves. The dev server (dev-server.ts) does not use this file.
export default defineConfig(createClientViteConfig({ appDir: import.meta.dirname }));
