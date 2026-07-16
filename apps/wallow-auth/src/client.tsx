/**
 * Browser entry for wallow-auth (Wallow-vec7.1.5) — the counterpart to
 * `ssr.tsx`, and the bundle whose arrival this task was blocked on: without a
 * client bundle nothing hydrates, and without hydration there is no readiness
 * signal for E2E to wait on.
 *
 * `RouterClient` rehydrates the router from the state `ssr.tsx` dehydrated into
 * the document before handing off to `RouterProvider`, so the client resumes the
 * server's matched route rather than re-resolving it from scratch.
 *
 * The whole document is hydrated (`hydrateRoot(document, ...)`), not a mount
 * node inside it, because the root route renders the entire `<html>` shell —
 * see `routes/__root.tsx`. Kept as `.tsx` for the same reason `ssr.tsx` is: it
 * depends on file-route types and is bundled by Vite, not typechecked by tsc.
 */
import { RouterClient } from "@tanstack/react-router/ssr/client";
import { hydrateRoot } from "react-dom/client";

import { createRouter } from "./router";

hydrateRoot(document, <RouterClient router={createRouter()} />);
