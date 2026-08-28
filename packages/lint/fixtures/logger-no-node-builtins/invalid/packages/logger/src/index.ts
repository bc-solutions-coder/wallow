// The browser entry itself, recreated at the path the rule gates on. Every import
// form the rule judges: static (node:-prefixed and bare), dynamic, and require.

// expect-error: wallow/logger-no-node-builtins
import { readFileSync } from "node:fs";
// expect-error: wallow/logger-no-node-builtins
import { join } from "path";

export async function reads(): Promise<string> {
  // A dynamic import reaches past a static ban, so the rule judges it too.
  // expect-error: wallow/logger-no-node-builtins
  const os = await import("node:os");
  // expect-error: wallow/logger-no-node-builtins
  const crypto = require("crypto");

  return readFileSync(join("a", "b"), "utf8") + os.hostname() + String(crypto);
}
