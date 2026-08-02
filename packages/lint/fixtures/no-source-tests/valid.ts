// NOT a spec, and that is the whole point of this file: the rule self-gates on the
// `*.test.*` filename, so a config switches it on once at the top level and it stays
// silent over source. Real tooling under `scripts/` and `packages/config` reads the
// filesystem for a living; only a TEST is claiming to assert behaviour while doing it.
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";

export function loadManifest(path: string): unknown {
  return JSON.parse(readFileSync(path, "utf8"));
}

export const requireFrom = createRequire(import.meta.url);
