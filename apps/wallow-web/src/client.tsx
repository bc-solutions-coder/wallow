/**
 * Browser entry for wallow-web (Wallow-ffpq.3.1) — the bundle the app was
 * missing, and the root cause of the inert app: `__root.tsx` shipped SSR HTML
 * with no client bundle, so nothing hydrated and every form was dead outside
 * `pnpm dev`.
 *
 * `RouterClient` rehydrates the router from the state the SSR pass dehydrated
 * into the document before handing off to `RouterProvider`, so the client
 * resumes the server's matched route rather than re-resolving it from scratch.
 *
 * The whole document is hydrated (`hydrateRoot(document, ...)`), not a mount
 * node inside it, because the root route renders the entire `<html>` shell —
 * see `routes/__root.tsx`. Kept as `.tsx` because it depends on file-route
 * types and is bundled by Vite, not typechecked by tsc.
 */
import { RouterClient } from "@tanstack/react-router/ssr/client";
import { hydrateRoot } from "react-dom/client";

import { createRouter } from "./router";
import "./styles.css";

hydrateRoot(document, <RouterClient router={createRouter()} />);
