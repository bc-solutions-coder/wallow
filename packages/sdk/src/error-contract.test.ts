/**
 * The unified error contract (Wallow-pu6a.5.3).
 *
 * Every operation's failure path now rejects with a {@link WallowError} raised
 * by the client's error interceptor (`runtime-config.ts`), so the hand-written
 * envelope-unwrapping layers that each invented their own error semantics are
 * gone: `facade.ts`'s exported `unwrap` (threw the RAW body), `auth-client.ts`'s
 * private `unwrap` (threw a `WallowError`), and `mfa-client.ts`'s `MfaUnwrap` —
 * the injection point that existed ONLY to keep those two semantics divergent
 * per app.
 *
 * These are surface assertions, deliberately separate from the behavioural specs
 * in `runtime-config.test.ts`: they pin what must no longer exist.
 */

import { readFileSync, readdirSync } from "node:fs";
import { basename, dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import * as browserEntry from "./index";

const sourceRoot: string = resolve(dirname(fileURLToPath(import.meta.url)));

/**
 * Every hand-written source file in the package: the generated client is not
 * ours to police, and specs legitimately still name the symbols they assert on.
 */
function handWrittenSources(): readonly string[] {
  const files: string[] = [];

  const walk = (directory: string): void => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const path: string = join(directory, entry.name);
      if (entry.isDirectory()) {
        if (entry.name !== "generated") {
          walk(path);
        }
      } else if (entry.name.endsWith(".ts") && !entry.name.includes(".test.")) {
        files.push(path);
      }
    }
  };

  walk(sourceRoot);
  return files;
}

describe("the hand-written unwrap layers are gone", () => {
  it("declares no unwrap helper anywhere in the package", () => {
    const declaring: string[] = handWrittenSources().filter((path: string) =>
      /function unwrap\b/u.test(readFileSync(path, "utf8")),
    );

    // facade.ts (raw-throwing) and auth-client.ts (WallowError-throwing) are the
    // two the interceptor supersedes. Neither may survive.
    expect(declaring).toEqual([]);
  });

  it("no longer ships the facade module at all", () => {
    // Wallow-pu6a.5.5 deleted `facade.ts` outright rather than leaving it
    // unwrap-free: with the interceptor owning the error contract there is no
    // envelope layer left for it to host.
    expect(handWrittenSources().map((path: string) => basename(path))).not.toContain("facade.ts");
  });

  it("stops exporting unwrap from the browser entry", () => {
    expect(Object.keys(browserEntry)).not.toContain("unwrap");
  });

  it("declares no SdkEnvelope type — nothing unwraps an envelope any more", () => {
    const declaring: string[] = handWrittenSources().filter((path: string) =>
      /(?:interface|type)\s+SdkEnvelope\b/u.test(readFileSync(path, "utf8")),
    );

    expect(declaring).toEqual([]);
  });
});

describe("MfaUnwrap is gone", () => {
  it("names MfaUnwrap in no hand-written source file", () => {
    const naming: string[] = handWrittenSources().filter((path: string) =>
      /MfaUnwrap/u.test(readFileSync(path, "utf8")),
    );

    // The injected-unwrap seam existed only so wallow-web could keep raw-throw
    // semantics while wallow-auth threw WallowError. One contract, no seam.
    expect(naming).toEqual([]);
  });
});
