import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { tanstackStart } from "@tanstack/react-start/plugin/vite";
import react from "@vitejs/plugin-react";
import { nitro } from "nitro/vite";
import { defineConfig } from "vite";

/**
 * The scratch app's only Vite config — deliberately the same shape as
 * apps/examples/minimal-app's, because that is the config a fork copies.
 *
 * `wallowStyles()` comes from the PACKED styles tarball, so this file also
 * proves the package's `./vite` subpath (a node-only entry) survives packing and
 * that its `publicDir` pointer still resolves from inside node_modules.
 */
export default defineConfig({
  environments: {
    // `nitro/vite` forces the client environment's `copyPublicDir` off, which
    // drops the brand assets `wallowStyles()` points `publicDir` at. Nitro sets
    // the flag with `??=`, so spelling it out here wins — the same override
    // apps/examples/minimal-app carries.
    client: { build: { copyPublicDir: true } },
  },
  plugins: [tanstackStart(), react(), nitro(), ...wallowStyles()],
});
