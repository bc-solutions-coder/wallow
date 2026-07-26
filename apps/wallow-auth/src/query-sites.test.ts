import { readdirSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The nine query sites read through the SDK query layer (Wallow-evd5.3.1).
 *
 * wallow-auth never had an `api.ts` layer: every read was an inline `useQuery`
 * with a hand-written key literal spelled at the call site. Nine of them, and two
 * spelled the SAME endpoint two different ways — `RegisterForm` keyed the
 * external-provider list `['auth', 'external-providers']` while
 * `ExternalProviders` keyed it `['external-providers']`, so the login screen and
 * the register screen each fetched and cached it separately. Routing both through
 * `authQueries.externalProviders()` collapses them onto one cache entry, which is
 * only true for as long as neither file re-grows a key of its own.
 *
 * That is what makes this a SOURCE spec rather than a render spec: the invariant
 * is "no component owns a query key", and a component spec can only observe the
 * one component it renders. This mirrors `router.query-client.test.tsx`, which
 * counts `createQueryClient()` calls across `src/` for the same reason, and it
 * encodes the task's literal acceptance criterion (`grep -rn 'queryKey: ['
 * apps/wallow-auth/src` returns nothing).
 *
 * Test files are excluded from the walk: specs legitimately construct keys to
 * seed a cache. The generated route tree is excluded as machine-owned.
 */

/** An inline React Query key literal — the thing no app source file may contain. */
const INLINE_QUERY_KEY = /queryKey:\s*\[/u;

/**
 * Where a migrated site's factories must come from: the SDK query subpath, or a
 * thin local `api` re-export of it (the indirection wallow-web's features use).
 * Either is fine — what must not happen is a file growing its own query options.
 */
const FACTORY_IMPORT = /from\s+"(?:@bc-solutions-coder\/sdk\/query|[^"]*api)"/u;

const SRC_DIR: string = fileURLToPath(new URL(".", import.meta.url));

/** One migrated site: its path under `src/` and the factories it must now call. */
interface QuerySite {
  readonly file: string;
  readonly factories: readonly string[];
  /** The screen refuses to retry this read (a dead token stays dead). */
  readonly refusesRetries: boolean;
  /** The screen gates the read on a guard, so a bad link never reaches the wire. */
  readonly gated: boolean;
}

/**
 * The nine sites, in the order the investigation inventoried them. `RegisterForm`
 * carries two (the provider list and the client-tenant lookup); the two
 * redirect-validation sites share one factory because they validate the same kind
 * of URL against the same endpoint.
 */
const SITES: readonly QuerySite[] = [
  {
    file: "features/register/components/RegisterForm.tsx",
    factories: ["authQueries.externalProviders(", "authQueries.clientTenant("],
    refusesRetries: false,
    gated: true,
  },
  {
    file: "features/login/components/ExternalProviders.tsx",
    factories: ["authQueries.externalProviders("],
    refusesRetries: true,
    gated: false,
  },
  {
    file: "features/consent/components/ConsentScreen.tsx",
    factories: ["authQueries.consentInfo("],
    refusesRetries: true,
    gated: true,
  },
  {
    file: "features/invitation/components/InvitationScreen.tsx",
    factories: ["authQueries.invitation("],
    refusesRetries: true,
    gated: true,
  },
  {
    file: "routes/invitation.tsx",
    factories: ["userQueries.currentUser("],
    refusesRetries: true,
    gated: false,
  },
  {
    file: "features/verify-email/components/VerifyEmailConfirm.tsx",
    factories: ["authQueries.verifyEmail("],
    refusesRetries: true,
    gated: true,
  },
  {
    file: "features/mfa-challenge/components/MfaChallengeForm.tsx",
    factories: ["authQueries.redirectValidation("],
    refusesRetries: true,
    gated: true,
  },
  {
    file: "features/logout/components/LogoutScreen.tsx",
    factories: ["authQueries.redirectValidation("],
    refusesRetries: true,
    gated: true,
  },
];

/** The two spellings the login and register screens used for one endpoint. */
const COLLIDING_KEYS: readonly string[] = ['"external-providers"', "'external-providers'"];

function readSource(relativePath: string): string {
  return readFileSync(`${SRC_DIR}${relativePath}`, "utf8");
}

/** Every non-test, non-generated `.ts`/`.tsx` file under `src/`, relative to it. */
function appSourceFiles(): string[] {
  return readdirSync(SRC_DIR, { recursive: true, withFileTypes: true })
    .filter((entry) => entry.isFile())
    .map((entry) => `${entry.parentPath.slice(SRC_DIR.length)}/${entry.name}`.replace(/^\//u, ""))
    .filter((path) => /\.tsx?$/u.test(path))
    .filter((path) => !/\.test\.tsx?$/u.test(path))
    .filter((path) => path !== "routeTree.gen.ts");
}

describe("wallow-auth query sites (Wallow-evd5.3.1)", () => {
  it("keeps no inline query key anywhere in app source", () => {
    const offenders: string[] = appSourceFiles().filter((path) =>
      INLINE_QUERY_KEY.test(readSource(path)),
    );

    expect(offenders).toEqual([]);
  });

  it.each(SITES)("$file reads through the SDK query layer", ({ file, factories }) => {
    const source: string = readSource(file);

    expect(source).toMatch(FACTORY_IMPORT);
    for (const factory of factories) {
      expect(source).toContain(factory);
    }
  });

  it.each(SITES)(
    "$file keeps its retry and gating decisions",
    ({ file, refusesRetries, gated }) => {
      const source: string = readSource(file);

      // A factory supplies the key and the queryFn, NOT the screen's policy. These
      // options live at the call site today and must survive the migration by being
      // spread onto the factory — the component specs cannot catch their loss,
      // because their test QueryClients already default `retry: false`, and a lost
      // `enabled` gate only shows up as a request the screen was never meant to send.
      expect(source.includes("retry: false")).toBe(refusesRetries);
      expect(source.includes("enabled:")).toBe(gated);
    },
  );

  it("collapses the external-provider key collision onto one factory", () => {
    const register: string = readSource("features/register/components/RegisterForm.tsx");
    const login: string = readSource("features/login/components/ExternalProviders.tsx");

    // Both screens now derive the key from the same factory instead of spelling
    // it — so they share one cache entry rather than fetching the list twice.
    expect(register).toContain("authQueries.externalProviders(");
    expect(login).toContain("authQueries.externalProviders(");
    for (const key of COLLIDING_KEYS) {
      expect(register).not.toContain(key);
      expect(login).not.toContain(key);
    }
  });
});
