import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * MFA-enroll feature `api.ts` — a THIN RE-EXPORT SEAM added by Wallow-x4qn.9.4,
 * and the ONE seam in this app that spans BOTH SDK entries. `MfaEnrollForm`
 * imports all three artifacts from `../api`.
 *
 * Two come from the generated query entry (`@bc-solutions-coder/sdk/query`):
 * `mfaEnrollTotpMutation` mints the secret, `mfaConfirmEnrollmentMutation`
 * confirms the first code.
 *
 * `mfaExchangeEnrollmentToken` comes from the RAW barrel, and the reason is
 * sequencing rather than shape. The exchange is what mints the
 * `Identity.MfaPartial` cookie, and `enroll/totp` fired without it simply 401s —
 * so the screen awaits the exchange inside its mount effect's `try`/`catch`,
 * BEFORE starting the enrollment, precisely so a failed exchange skips a call that
 * could only fail. A `useMutation` would put that ordering behind a callback.
 * Wallow-x4qn.9.3 left it imperative on purpose; this seam keeps it that way
 * while still making `api.ts` the feature's only data import.
 *
 * Note the shape the guard on that does NOT take: the SDK's generator emits an
 * `{op}Mutation()` factory for EVERY non-GET operation unconditionally, so
 * `mfaExchangeEnrollmentTokenMutation` does exist on the query entry — sitting one
 * import away from the two factories this seam DOES re-export, which is exactly why
 * the guard is needed. The enforceable invariant is that this feature never ADOPTS
 * it, not that it is unavailable.
 *
 * Why identity and not just presence: a hand-written look-alike for any of the
 * three would carry the same name, the same call shape and the same type, and the
 * screen driving it would pass every behavioural spec while talking to something
 * the OpenAPI document does not describe — here, with a half-enrolled
 * authenticator as the user-visible result. `toBe` is the only assertion that
 * rules that out.
 *
 * `MfaEnrollmentConfirmedResponse` and `isSafeReturnUrl` stay on the raw barrel at
 * the call site: a DTO type is not a data import and the return-url guard issues
 * no request.
 *
 * Node project — it imports built package output and mounts nothing.
 */

import * as sdk from "@bc-solutions-coder/sdk";
import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The imperative POST behind the seam: raw, and awaited before enrollment starts. */
const RAW_OPERATION = "mfaExchangeEnrollmentToken";

/** The seam's whole surface, in the order an ESM namespace enumerates it. */
const SURFACE: readonly string[] = [
  "mfaConfirmEnrollmentMutation",
  "mfaEnrollTotpMutation",
  RAW_OPERATION,
];

/** The generated factory the SDK does emit for that POST, and this feature must never adopt. */
const GENERATED_FACTORY = "mfaExchangeEnrollmentTokenMutation";

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

describe("api.ts re-exports the SDK mfa-enroll query surface", () => {
  it("re-exports each generated symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.mfaEnrollTotpMutation).toBe(query.mfaEnrollTotpMutation);
    expect(api.mfaConfirmEnrollmentMutation).toBe(query.mfaConfirmEnrollmentMutation);
  });

  it("exposes nothing beyond the artifacts the mfa-enroll feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});

describe("the imperative enrollment-token exchange stays a raw operation", () => {
  it("re-exports it by identity from the raw @bc-solutions-coder/sdk barrel", () => {
    expect(api.mfaExchangeEnrollmentToken).toBe(sdk.mfaExchangeEnrollmentToken);
  });

  it("scans a feature tree with the seam and the screen in it", () => {
    // A guard on the guard: an empty or misrooted walk would make the case below
    // pass over nothing at all, which is exactly how a security guard rots.
    const users: readonly string[] = featureFilesNaming(RAW_OPERATION);

    expect(users).toContain("api.ts");
    expect(users).toContain("components/MfaEnrollForm.tsx");
  });

  it("never adopts the generated mutation factory the SDK does emit for this POST", () => {
    // This is the seam where the temptation is real: two of three artifacts come from
    // the generated entry, so a reader will reach for a third — and the generator,
    // which emits an `{op}Mutation()` for every non-GET operation regardless of who
    // calls it, has one waiting. The first assertion states that premise so it cannot
    // silently invert; the other two hold the invariant that nothing in this feature
    // reaches for it, neither the seam's surface nor the screen behind it. Adopting it
    // would move the exchange behind a callback and break the ordering the mount
    // effect depends on: no `Identity.MfaPartial` cookie first, and `enroll/totp` 401s.
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
    // factories only, never the raw operation, so taking the exchange off the barrel
    // is not someone missing a same-named export one entry over.
    expect(RAW_OPERATION in query).toBe(false);
  });
});
