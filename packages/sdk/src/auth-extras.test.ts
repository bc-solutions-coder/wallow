/**
 * Spec for `auth-extras` — the collapsed remains of `auth-client.ts`
 * (Wallow-pu6a.5.4).
 *
 * `auth-client.ts` was 368 lines of which almost all were renames of generated
 * operations, envelope unwrapping, and error mapping — work the generated client
 * now does itself (`responseStyle: 'data'` + `throwOnError: true` from task 5.2,
 * the `WallowError` interceptor from task 5.3). Three behaviors have no codegen
 * expression, and this file is the contract for exactly those three plus the
 * deletions that make them the ONLY survivors:
 *
 *   1. `getCurrentUser` softens 401 (and ONLY 401) into `null`;
 *   2. `consentInfoArgs` joins `scopes` with a SPACE and omits the key when empty;
 *   3. `validateRedirectUriArgs` omits the `clientId` KEY rather than sending it
 *      as `undefined`.
 *
 * `./generated` is mocked because these helpers are wrappers: the assertions are
 * about which operation is called, with which argument, and what happens to the
 * result — never about the wire.
 */

import { existsSync, readFileSync, readdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it, vi } from "vitest";

import {
  type ConsentInfoArgs,
  type ValidateRedirectUriArgs,
  consentInfoArgs,
  getCurrentUser,
  validateRedirectUriArgs,
} from "./auth-extras";
import { WallowError } from "./errors";
import type {
  AccountValidateRedirectUriData,
  AppsGetConsentInfoData,
  CurrentUserResponse,
} from "./generated";
import { type Client, createClient, createConfig } from "./generated/client";
import type { MfaStatusResponse } from "./index";

// Hoisted so the vi.mock factory and the test bodies share the same spy.
const mocks = vi.hoisted(() => ({
  usersGetCurrentUser: vi.fn(),
}));

vi.mock("./generated", () => ({ ...mocks }));

/** A real generated client instance — the thing app code passes as `sdk.client`. */
const client: Client = createClient(createConfig({ baseUrl: "https://api.test" }));

const SIGNED_IN_USER: CurrentUserResponse = {
  id: "3f1c4b0e-0000-4000-8000-000000000001",
  email: "admin@wallow.dev",
  roles: ["Admin"],
};

function unauthorized(): WallowError {
  return new WallowError({ status: 401, code: "UNAUTHORIZED", title: "Unauthorized" });
}

describe("getCurrentUser", () => {
  it("resolves the user and routes the call through the caller's client", async () => {
    mocks.usersGetCurrentUser.mockResolvedValue(SIGNED_IN_USER);

    await expect(getCurrentUser({ client })).resolves.toBe(SIGNED_IN_USER);
    expect(mocks.usersGetCurrentUser).toHaveBeenCalledWith({ client });
  });

  it("calls the operation with an empty options object when no client is supplied", async () => {
    mocks.usersGetCurrentUser.mockResolvedValue(SIGNED_IN_USER);

    await getCurrentUser();

    expect(mocks.usersGetCurrentUser).toHaveBeenCalledWith({});
  });

  it("resolves null when the API answers 401 — anonymous is an answer, not a failure", async () => {
    mocks.usersGetCurrentUser.mockRejectedValue(unauthorized());

    await expect(getCurrentUser({ client })).resolves.toBeNull();
  });

  // Softening anything beyond 401 would sign every real user out during an
  // outage, so each of these must reach the caller untouched.
  it.each([400, 403, 404, 500, 502, 503])(
    "rethrows the WallowError unchanged for status %i",
    async (status: number) => {
      const failure: WallowError = new WallowError({
        status,
        code: "SOMETHING_ELSE",
        title: "Something else",
      });
      mocks.usersGetCurrentUser.mockRejectedValue(failure);

      await expect(getCurrentUser({ client })).rejects.toBe(failure);
    },
  );

  it("rethrows a transport fault unchanged — a dead proxy is not a signed-out user", async () => {
    const failure: TypeError = new TypeError("fetch failed");
    mocks.usersGetCurrentUser.mockRejectedValue(failure);

    await expect(getCurrentUser({ client })).rejects.toBe(failure);
  });

  it("softens only the WallowError contract, not any 401-shaped object", async () => {
    // Task 5.3 makes WallowError the single failure contract. An unbranded
    // object carrying `status: 401` means something bypassed that contract —
    // which is a bug to surface, not an anonymous session to report.
    const impostor: { status: number } = { status: 401 };
    mocks.usersGetCurrentUser.mockRejectedValue(impostor);

    await expect(getCurrentUser({ client })).rejects.toBe(impostor);
  });

  it("resolves null when the endpoint answers with no body", async () => {
    // A 200 with no body is degenerate; fall to the LESS-privileged branch
    // rather than inventing a signed-in user out of nothing.
    mocks.usersGetCurrentUser.mockResolvedValue(undefined);

    await expect(getCurrentUser({ client })).resolves.toBeNull();
  });
});

describe("consentInfoArgs", () => {
  it("sends the requested scopes as ONE space-joined string", () => {
    const args: ConsentInfoArgs = consentInfoArgs("wallow-web", ["openid", "profile", "email"]);

    expect(args).toEqual({
      path: { clientId: "wallow-web" },
      query: { scopes: "openid profile email" },
    });
  });

  it("never comma-joins — a comma arrives at the API as one unknown scope name", () => {
    const args: ConsentInfoArgs = consentInfoArgs("wallow-web", ["openid", "profile"]);

    expect(args.query?.scopes).not.toContain(",");
  });

  it("preserves the requested scope order", () => {
    const args: ConsentInfoArgs = consentInfoArgs("wallow-web", ["email", "openid", "profile"]);

    expect(args.query?.scopes).toBe("email openid profile");
  });

  it.each([
    ["undefined", undefined],
    ["empty", [] as readonly string[]],
  ])("omits the query KEY entirely when scopes are %s", (_label: string, scopes) => {
    const args: ConsentInfoArgs = consentInfoArgs("wallow-web", scopes);

    expect(args).toEqual({ path: { clientId: "wallow-web" } });
    // `toEqual` treats a present-but-undefined member as absent, so the key
    // itself is checked: a bare `scopes=` on the wire is a different question.
    expect(Object.hasOwn(args, "query")).toBe(false);
  });
});

describe("validateRedirectUriArgs", () => {
  it("carries the uri and the client when both are known", () => {
    const args: ValidateRedirectUriArgs = validateRedirectUriArgs(
      "https://app.test/callback",
      "wallow-web",
    );

    expect(args).toEqual({ query: { uri: "https://app.test/callback", clientId: "wallow-web" } });
  });

  it.each([
    ["undefined", undefined],
    ["blank", ""],
  ])("omits the clientId KEY when the client is %s", (_label: string, clientId?: string) => {
    const args: ValidateRedirectUriArgs = validateRedirectUriArgs(
      "https://app.test/callback",
      clientId,
    );
    const query: NonNullable<ValidateRedirectUriArgs["query"]> = args.query ?? {};

    expect(args).toEqual({ query: { uri: "https://app.test/callback" } });
    // The generated client would put a bare `clientId=` on the wire, and an
    // unknown client fails CLOSED to the AuthUrl-only origin set — a different
    // question from asking unscoped.
    expect(Object.hasOwn(query, "clientId")).toBe(false);
  });
});

describe("the shaped arguments plug into the generated operations", () => {
  it("matches the generated operations' argument types", () => {
    const consent: Pick<AppsGetConsentInfoData, "path" | "query"> = consentInfoArgs("wallow-web", [
      "openid",
    ]);
    const redirect: Pick<AccountValidateRedirectUriData, "query"> = validateRedirectUriArgs(
      "https://app.test/callback",
    );

    expect(consent.path.clientId).toBe("wallow-web");
    expect(redirect.query?.uri).toBe("https://app.test/callback");
  });
});

const srcDir: string = dirname(fileURLToPath(import.meta.url));

/** Hand-written source files: everything under `src/` except the generated client and specs. */
function handWrittenSources(): readonly string[] {
  return readdirSync(srcDir, { recursive: true, encoding: "utf8" }).filter(
    (entry: string): boolean =>
      entry.endsWith(".ts") &&
      !entry.startsWith("generated") &&
      !entry.includes("generated/") &&
      !entry.endsWith(".test.ts"),
  );
}

/**
 * The five response interfaces `mfa-client.ts` hand-wrote because the MFA
 * operations used to resolve `200: unknown`. The regenerated client now emits a
 * real schema type for each, so a hand-written copy can only shadow it.
 */
const HAND_WRITTEN_MFA_INTERFACES: readonly string[] = [
  "MfaStatusResponse",
  "MfaEnrollResponse",
  "MfaConfirmResponse",
  "MfaDisableResponse",
  "MfaRegenerateBackupCodesResponse",
];

describe("the collapsed hand-written surface", () => {
  it("deletes auth-client.ts", () => {
    expect(existsSync(join(srcDir, "auth-client.ts"))).toBe(false);
  });

  it.each(HAND_WRITTEN_MFA_INTERFACES)(
    "declares %s nowhere outside the generated client",
    (name: string) => {
      const declaration: RegExp = new RegExp(`(interface|type)\\s+${name}\\b`, "u");
      const offenders: readonly string[] = handWrittenSources().filter((file: string): boolean =>
        declaration.test(readFileSync(join(srcDir, file), "utf8")),
      );

      expect(offenders).toEqual([]);
    },
  );

  it("drops the barrel's hand-written MFA type override and exports auth-extras", () => {
    const barrel: string = readFileSync(join(srcDir, "index.ts"), "utf8");

    expect(barrel).toMatch(/export \* from "\.\/auth-extras";/u);
    expect(barrel).not.toContain("./auth-client");
    // The override block existed only to beat the generated types; with the
    // hand-written interfaces gone there is nothing left to beat.
    for (const name of HAND_WRITTEN_MFA_INTERFACES) {
      expect(barrel).not.toContain(name);
    }
  });

  it("resolves the barrel's MFA types to the GENERATED schema", () => {
    // `backupCodeCount` is `number | string` in the generated schema and was
    // `number` in the hand-written interface: assigning a string compiles only
    // when the generated type is the one the barrel exports.
    const status: MfaStatusResponse = { enabled: true, method: null, backupCodeCount: "3" };

    expect(status.backupCodeCount).toBe("3");
  });

  it("keeps auth-extras to the three behaviors and nothing else", () => {
    const source: string = readFileSync(join(srcDir, "auth-extras.ts"), "utf8");
    const codeLines: readonly string[] = source
      .split("\n")
      .map((line: string): string => line.trim())
      .filter(
        (line: string): boolean =>
          line !== "" && !line.startsWith("//") && !line.startsWith("/*") && !line.startsWith("*"),
      );

    expect(codeLines.length).toBeLessThanOrEqual(70);
  });

  it("exports exactly the three surviving behaviors", async () => {
    const authExtras: Record<string, unknown> = await import("./auth-extras");

    expect(new Set(Object.keys(authExtras))).toEqual(
      new Set(["consentInfoArgs", "getCurrentUser", "validateRedirectUriArgs"]),
    );
  });
});
