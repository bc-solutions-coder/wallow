/**
 * Browser entry — the counterpart to `ssr.tsx`. `RouterClient` rehydrates the
 * router from the state `ssr.tsx` dehydrated into the document before handing off
 * to `RouterProvider`, so the client resumes the server's matched route.
 *
 * The whole document is hydrated (`hydrateRoot(document, ...)`), not a mount node
 * inside it, because the root route renders the entire `<html>` shell (see
 * `routes/__root.tsx`). Importing `./styles.css` here is what pulls the Tailwind
 * pipeline into the client bundle.
 */
import { RouterClient } from "@tanstack/react-router/ssr/client";
import { hydrateRoot } from "react-dom/client";

import { createRouter } from "./router";
import "./styles.css";

hydrateRoot(document, <RouterClient router={createRouter()} />);
