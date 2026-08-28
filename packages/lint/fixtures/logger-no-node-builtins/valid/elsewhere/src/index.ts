// NOT under packages/logger/src/, and that is the point of this file: the rule
// self-gates on the path, so a config switches it on once at the top level and it
// stays silent over every other package — the SDK's server entry legitimately
// imports node:crypto, which is why import/no-nodejs-modules is off repo-wide.
import { readFileSync } from "node:fs";

export function loadManifest(path: string): unknown {
  return JSON.parse(readFileSync(path, "utf8"));
}
