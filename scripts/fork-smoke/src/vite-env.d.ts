/**
 * Ambient typing for the non-TS modules Vite resolves. The app's tsconfig sets
 * `types: ["node"]` and does not pull in `vite/client`, so `__root.tsx`'s
 * side-effect stylesheet import would otherwise be an unresolved module.
 */
declare module "*.css";
