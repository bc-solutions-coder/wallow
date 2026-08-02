/**
 * Barrel pins for @bc-solutions-coder/auth (Wallow-x4qn.3).
 *
 * The package exists so an app's auth imports come from ONE place, which makes
 * two things structural rather than incidental:
 *
 *   1. THE SURFACE, in both directions. A dropped export sends the next migration
 *      back to reaching into the SDK (or re-inventing a current-user probe, which
 *      is exactly the duplication this package deletes), and an accidentally
 *      widened one turns a curated surface into a grab bag.
 *   2. IDENTITY of the SDK re-exports. `isAdmin`/`requireAuth`/`loginRedirect` are
 *      re-exported by reference, not wrapped, so app code importing them from here
 *      gets the SDK's own tested guards.
 *
 * The third pin — no `@tanstack/react-router` dependency, and react-query only
 * through the `@bc-solutions-coder/query` facade — used to be a source sweep over
 * `src/`. The facade half is a repo-root `no-restricted-imports` rule, which
 * catches it in the editor; the router half is carried by this package's manifest,
 * which declares no router to import.
 */

import { isAdmin, loginRedirect, requireAuth } from "@bc-solutions-coder/sdk";
import { describe, expect, it } from "vitest";

import * as auth from "./index";

/** This package's own contribution to the barrel. */
const OWN_EXPORTS: readonly string[] = [
  "currentUserQuery",
  "ensureCurrentUser",
  "hasPermission",
  "hasRole",
  "useCurrentUser",
];

/**
 * The SDK guards and claim helpers re-exported so auth imports come from one
 * package, held next to the bindings they must BE.
 *
 * Named imports rather than a namespace import: the repo-root oxlint
 * `no-restricted-imports` rule bans `import * as` from the SDK, because a
 * namespace import reaches the deleted module-global client symbols too.
 */
const SDK_GUARDS: Readonly<Record<string, unknown>> = { isAdmin, loginRedirect, requireAuth };

const REEXPORTED_FROM_SDK: readonly string[] = Object.keys(SDK_GUARDS);

describe("@bc-solutions-coder/auth barrel", () => {
  it("exports the current-user layer plus the re-exported SDK guards, and nothing else", () => {
    expect(Object.keys(auth).toSorted()).toEqual(
      [...OWN_EXPORTS, ...REEXPORTED_FROM_SDK].toSorted(),
    );
  });

  it("exposes its own members as functions", () => {
    for (const name of OWN_EXPORTS) {
      expect(typeof (auth as Record<string, unknown>)[name], name).toBe("function");
    }
  });

  it("re-exports the SDK guards by reference identity rather than wrapping them", () => {
    const authExports = auth as Record<string, unknown>;

    for (const name of REEXPORTED_FROM_SDK) {
      expect(authExports[name], name).toBe(SDK_GUARDS[name]);
    }
  });
});
