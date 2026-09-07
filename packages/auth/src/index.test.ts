/**
 * Verify the public runtime exports and reference identity of SDK guard re-exports.
 */

import { loginRedirect, requireAuth } from "@bc-solutions-coder/sdk";
import { describe, expect, it } from "vitest";

import * as auth from "./index";

/** This package's own contribution to the barrel. */
const OWN_EXPORTS: readonly string[] = [
  "currentUserQuery",
  "ensureCurrentUser",
  "hasPermission",
  "hasRole",
  "isAdmin",
  "isGlobalAdmin",
  "useCurrentUser",
];

/**
 * SDK route guards must be the same bindings exported by the SDK. This package owns its typed
 * current-user role helpers.
 */
const SDK_GUARDS: Readonly<Record<string, unknown>> = { loginRedirect, requireAuth };

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
