import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Forgot-password `api.ts` — a thin re-export seam over the RAW SDK barrel, not
 * the generated `{op}Mutation()` factory every other seam re-exports: the screen
 * must report the same outcome whether or not the address exists, and a mutation's
 * error surface would make the form an account-enumeration oracle.
 *
 * Identity (`toBe`), not presence — a hand-written look-alike carries the same
 * name, shape and type, passes every behavioural spec, and reaches an endpoint
 * the OpenAPI document does not describe.
 */

import * as sdk from "@bc-solutions-coder/sdk";
import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

const RAW_OPERATION = "accountForgotPassword";

const SURFACE: readonly string[] = [RAW_OPERATION];

/** The factory the SDK does emit for that POST, and this feature must never adopt. */
const GENERATED_FACTORY = "accountForgotPasswordMutation";

const featureDir: string = dirname(fileURLToPath(import.meta.url));

/** This file, excluded from the scan below: it has to name the factory it forbids. */
const SELF: string = relative(featureDir, fileURLToPath(import.meta.url));

/**
 * Files in THIS feature whose comment-stripped code contains `needle`.
 *
 * Comments are stripped so this file's own prose is not read as a use.
 * `withFileTypes` + `isFile()` matters: Vitest browser mode writes failure
 * screenshots into `components/__screenshots__/<spec>/` directories, and a
 * name-only filter would hand `readFileSync` a directory.
 */
function featureFilesNaming(needle: string): readonly string[] {
  return readdirSync(featureDir, { recursive: true, withFileTypes: true })
    .filter(
      (entry): boolean =>
        entry.isFile() && (entry.name.endsWith(".ts") || entry.name.endsWith(".tsx")),
    )
    .map((entry): string => relative(featureDir, join(entry.parentPath, entry.name)))
    .filter((path): boolean => path !== SELF)
    .filter((path): boolean =>
      readFileSync(join(featureDir, path), "utf8")
        .replaceAll(/\/\*[\s\S]*?\*\//gu, "")
        .replaceAll(/^\s*\/\/.*$/gmu, "")
        .includes(needle),
    )
    .toSorted();
}

describe("api.ts re-exports the raw forgot-password operation", () => {
  it("re-exports it by identity from @bc-solutions-coder/sdk", () => {
    expect(api.accountForgotPassword).toBe(sdk.accountForgotPassword);
  });

  it("exposes nothing beyond the operation the forgot-password feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});

describe("why this seam differs from every other one", () => {
  it("scans a feature tree with the seam and the screen in it", () => {
    // A guard on the guard: an empty or misrooted walk would make the case below
    // pass over nothing at all, which is exactly how a security guard rots.
    const users: readonly string[] = featureFilesNaming(RAW_OPERATION);

    expect(users).toContain("api.ts");
    expect(users).toContain("components/ForgotPasswordForm.tsx");
  });

  it("never adopts the generated mutation factory the SDK does emit for this POST", () => {
    // The first assertion is the PREMISE, stated so it cannot silently invert: the
    // generator emits an `{op}Mutation()` for every non-GET operation regardless of
    // who calls it, so this factory exists and is one import away at all times. The
    // other two are the invariant — nothing in this feature reaches for it.
    expect(typeof query[GENERATED_FACTORY], `${GENERATED_FACTORY} is no longer generated`).toBe(
      "function",
    );
    expect(Object.keys(api), `the seam re-exports ${GENERATED_FACTORY}`).not.toContain(
      GENERATED_FACTORY,
    );
    expect(featureFilesNaming(GENERATED_FACTORY), "a file in this feature names it").toEqual([]);
  });

  it("has no bare operation on the generated entry to prefer instead", () => {
    // The query entry carries factories only, never the raw operation, so
    // re-exporting `accountForgotPassword` from the barrel is not someone missing a
    // same-named export one entry over.
    expect(RAW_OPERATION in query).toBe(false);
  });
});
