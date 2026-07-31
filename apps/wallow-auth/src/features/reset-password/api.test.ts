import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Reset-password feature `api.ts` — a THIN RE-EXPORT SEAM over the RAW SDK barrel
 * (`@bc-solutions-coder/sdk`), added by Wallow-x4qn.9.4.
 *
 * READ THIS BEFORE "FIXING" IT. Every other seam in this app re-exports GENERATED
 * `{op}Mutation()` / `{op}Options()` factories from `@bc-solutions-coder/sdk/query`.
 * This one re-exports a raw operation, and that is deliberate:
 * `ResetPasswordForm` uses `@bc-solutions-coder/forms`' NO-MUTATION escape hatch
 * and calls `accountResetPassword` directly inside `useAppForm`'s `onSubmit`,
 * because it owns its own status-code branching — an expired or already-redeemed
 * token is not a form error to render beside a field, it is a different screen.
 * Wallow-x4qn.9.3 excluded this screen from the mutation conversion for exactly
 * that reason.
 *
 * So the seam still holds — `api.ts` is the feature's only data import — while
 * what sits behind it is the raw POST. The last describe below is the guard on that
 * rationale, and note the shape it does NOT take: the SDK's generator emits an
 * `{op}Mutation()` factory for EVERY non-GET operation unconditionally, so
 * `accountResetPasswordMutation` does exist on the query entry. The enforceable —
 * and the only true — invariant is that this feature never ADOPTS it, so what is
 * pinned is the absence from the seam's surface and from the feature's source, not
 * an absence from the SDK.
 *
 * Why identity and not just presence: a hand-written look-alike would carry the
 * same name, the same call shape and the same type, pass the screen's behavioural
 * specs, and spend a single-use reset token against an endpoint the OpenAPI
 * document does not describe. `toBe` is the only assertion that rules that out.
 *
 * Node project — it imports built package output and mounts nothing.
 */

import * as sdk from "@bc-solutions-coder/sdk";
import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The raw POST this feature really reaches, on the barrel and behind the seam. */
const RAW_OPERATION = "accountResetPassword";

/** The seam's whole surface, in the order an ESM namespace enumerates it. */
const SURFACE: readonly string[] = [RAW_OPERATION];

/** The generated factory the SDK does emit for that POST, and this feature must never adopt. */
const GENERATED_FACTORY = "accountResetPasswordMutation";

const featureDir: string = dirname(fileURLToPath(import.meta.url));

/** This file, excluded from the scan below: it has to name the factory it forbids. */
const SELF: string = relative(featureDir, fileURLToPath(import.meta.url));

/**
 * Files in THIS feature whose comment-stripped code contains `needle`.
 *
 * Scoped to the feature dir rather than to `src/`, because the invariant is about
 * this feature: whichever door someone walks through — the seam, a direct query-entry
 * import in the screen, an alias — the name has to appear in one of these files.
 *
 * Comments are stripped exactly as `features-api-seam.test.ts` does it, so the
 * prose above is not read as a use.
 * `withFileTypes` + `isFile()` matters: Vitest browser mode writes failure
 * screenshots into `components/__screenshots__/<spec>/` directories, and a name-only
 * filter would hand `readFileSync` a directory.
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

describe("api.ts re-exports the raw reset-password operation", () => {
  it("re-exports it by identity from @bc-solutions-coder/sdk", () => {
    expect(api.accountResetPassword).toBe(sdk.accountResetPassword);
  });

  it("exposes nothing beyond the operation the reset-password feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});

describe("why this seam differs from every other one", () => {
  it("scans a feature tree with the seam and the screen in it", () => {
    // A guard on the guard: an empty or misrooted walk would make the case below
    // pass over nothing at all, which is exactly how a security guard rots.
    const users: readonly string[] = featureFilesNaming(RAW_OPERATION);

    expect(users).toContain("api.ts");
    expect(users).toContain("components/ResetPasswordForm.tsx");
  });

  it("never adopts the generated mutation factory the SDK does emit for this POST", () => {
    // The first assertion is the PREMISE, stated so it cannot silently invert: the
    // generator emits an `{op}Mutation()` for every non-GET operation regardless of
    // who calls it, so this factory exists and is one import away at all times.
    // The other two are the invariant — nothing in this feature reaches for it,
    // neither the seam's surface nor the screen behind it. Adopting it would put the
    // failure back into the form's error surface, and this screen has to BRANCH on
    // the status instead: an expired or already-redeemed single-use token is a
    // different screen, not a message beside a field.
    expect(typeof query[GENERATED_FACTORY], `${GENERATED_FACTORY} is no longer generated`).toBe(
      "function",
    );
    expect(Object.keys(api), `the seam re-exports ${GENERATED_FACTORY}`).not.toContain(
      GENERATED_FACTORY,
    );
    expect(featureFilesNaming(GENERATED_FACTORY), "a file in this feature names it").toEqual([]);
  });

  it("has no bare operation on the generated entry to prefer instead", () => {
    // The half of this case that was always true, kept: the query entry carries
    // factories only, never the raw operation, so re-exporting `accountResetPassword`
    // from the barrel is not someone missing a same-named export one entry over.
    expect(RAW_OPERATION in query).toBe(false);
  });
});
