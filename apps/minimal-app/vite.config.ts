import { tanstackStart } from "@tanstack/react-start/plugin/vite";
import react from "@vitejs/plugin-react";
import { nitro } from "nitro/vite";
import { defineConfig } from "vite";

/**
 * PROTOTYPE — the Vite config an EXTERNAL relying party writes.
 *
 * Nothing here comes from a workspace package: `@bc-solutions-coder/config` is
 * private and an outside consumer cannot install it, so the two invariants it
 * used to supply are spelled out inline instead.
 */
const DEFAULT_PORT = 3010;

export default defineConfig({
  server: { port: Number(process.env.PORT ?? DEFAULT_PORT) },
  ssr: {
    // One react-query in the SSR graph. Vite externalizes ordinary deps for SSR
    // but bundles linked ones; with the SDK linked (workspace) its `./query`
    // entry arrives bundled while `@tanstack/react-router-ssr-query` stays
    // external and brings a second copy — two QueryClient contexts, and a
    // `useQuery` under SSR throws "No QueryClient set". Naming both here keeps
    // them in one graph. A consumer installing the SDK from the registry does
    // not hit the split, but the line is harmless and worth copying.
    noExternal: ["@tanstack/react-router-ssr-query", "@tanstack/react-query"],
  },
  plugins: [tanstackStart(), react(), nitro()],
});
