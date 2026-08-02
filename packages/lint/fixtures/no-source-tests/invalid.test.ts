// Every way a spec can name the filesystem module. The rule reports the SPECIFIER,
// so each annotation targets the line the string literal sits on — keep these
// declarations on one line.

// expect-error: wallow/no-source-tests
import { readFileSync } from "node:fs";
// expect-error: wallow/no-source-tests
import { readFile } from "node:fs/promises";
// expect-error: wallow/no-source-tests
import fs from "fs";
// expect-error: wallow/no-source-tests
import "fs/promises";

import { join } from "node:path";

export async function reads(directory: string): Promise<string> {
  // A dynamic import is how a spec reaches past a static ban, so the rule judges it too.
  // expect-error: wallow/no-source-tests
  const late = await import("node:fs");
  // expect-error: wallow/no-source-tests
  const required = require("fs");

  return (
    readFileSync(join(directory, "a"), "utf8") +
    (await readFile(join(directory, "b"), "utf8")) +
    String(fs.existsSync(directory)) +
    String(late.existsSync(directory)) +
    String(required.existsSync(directory))
  );
}
