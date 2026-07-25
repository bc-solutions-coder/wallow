import { createFileRoute } from "@tanstack/react-router";

import { HelloCard } from "../features/hello/HelloCard";

/**
 * The app's only route (`/`). Unlike wallow-auth's index (which redirects to
 * `/login`), this renders a single hello screen so the reference app boots to
 * something visible with no backend dependency.
 */
export const Route = createFileRoute("/")({
  component: HelloCard,
});
