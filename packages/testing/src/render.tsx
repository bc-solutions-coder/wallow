/**
 * Single seam for the shared component-render helper: a thin re-export of
 * `vitest-browser-react`'s `render`. Consuming apps import `render` from
 * `@bc-solutions-coder/testing` so future shared providers/wrappers can be added
 * in one place without touching every spec.
 */
export { render } from "vitest-browser-react";
