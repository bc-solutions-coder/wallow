import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The canonical owner of the branding configuration schema is this package,
 * `@bc-solutions-coder/styles`, not the Blazor `Wallow.Auth` app that is being
 * deleted (Wallow-vec7.5.3). The schema itself already lives here in
 * `branding.ts`; these tests pin the *documentation* of that ownership so the
 * canonical status is recorded in git-tracked docs before `Wallow.Auth` (and,
 * later, `Wallow.Web`) is removed — the CLAUDE.md files that historically
 * carried this claim are gitignored and produce nothing shippable.
 *
 * The tests read the docs-site pages that today still name `Wallow.Auth` /
 * `Wallow.Web` as the canonical `BrandingOptions` owner and assert that (a) they
 * no longer make that claim and (b) some tracked doc names `packages/styles`
 * instead.
 */

/** Repo root, resolved the same way the sibling asset tests resolve it. */
const repoRoot: string = fileURLToPath(new URL("../../../", import.meta.url));

function readDoc(relativePath: string): string {
  return readFileSync(`${repoRoot}${relativePath}`, "utf8");
}

const frontendSetup: string = readDoc("docs/development/frontend-setup.md");
const configuration: string = readDoc("docs/getting-started/configuration.md");

/**
 * A doc sentence claiming the Blazor Auth or Web boundary is the *canonical*
 * owner of the branding options — the exact claim this task relocates.
 */
const blazorOwnershipClaim: RegExp = /(Wallow\.Auth|Auth boundary|Wallow\.Web)[^\n]*canonical/iu;

/** A doc statement naming this package as the canonical branding owner. */
const stylesOwnershipClaim: RegExp =
  /(?:packages\/styles|@bc-solutions-coder\/styles)[^\n]*canonical|canonical[^\n]*(?:packages\/styles|@bc-solutions-coder\/styles)/iu;

describe("branding ownership documentation", () => {
  it("frontend-setup no longer names Wallow.Auth/Wallow.Web as the canonical branding owner", () => {
    expect(frontendSetup).not.toMatch(blazorOwnershipClaim);
  });

  it("configuration no longer names the Auth boundary as the canonical branding owner", () => {
    expect(configuration).not.toMatch(blazorOwnershipClaim);
  });

  it("a tracked doc names packages/styles as the canonical branding owner", () => {
    const trackedDocs: string = `${frontendSetup}\n${configuration}`;
    expect(trackedDocs).toMatch(stylesOwnershipClaim);
  });
});
