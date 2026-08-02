// The cases the rule must NOT report. A ban that only ever fires proves nothing about
// where it stops.

// Path and URL work is not a read — a spec still needs them to name things.
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

// A build config read as the resolved OBJECT rather than as text. This is the intended
// replacement for a `readFileSync` over `vite.config.ts`: the same claim, but asserted
// against what the bundler will actually run.
import viteConfig from "./vite.config.ts";

// The module under test, imported and asserted on. The whole point of the rule.
import { subject } from "./subject.ts";

// `import.meta.glob` is a bundler feature that IMPORTS modules; it hands back module
// namespaces, never a file's bytes, so it is categorically not a source read.
const screens = import.meta.glob("./screens/*.tsx");

export async function checks(name: string): Promise<unknown> {
  // A computed specifier is not statically judgeable, and a legitimate one is common.
  const entry = await import(`./screens/${name}.tsx`);
  // `require` of anything that is not the filesystem stays untouched.
  const path = require("node:path");

  return [
    join(dirname(fileURLToPath(import.meta.url)), "fixture"),
    viteConfig,
    subject,
    screens,
    entry,
    path,
  ];
}
