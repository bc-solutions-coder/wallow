/**
 * Ambient typing for the non-TS modules Vite resolves. The app's tsconfig sets
 * `types: ["node"]` and does not pull in `vite/client`, so `__root.tsx`'s
 * side-effect stylesheet import would otherwise be an unresolved module.
 *
 * Declared with no exported shape on purpose: the import exists for its side
 * effect (it pulls the Tailwind pipeline into the graph), and nothing reads a
 * value from it — the `?url` form is what ships a 404ing stylesheet under
 * Start's two-environment build.
 */
declare module "*.css";
