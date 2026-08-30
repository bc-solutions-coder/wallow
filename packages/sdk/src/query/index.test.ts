/**
 * Export-surface contract for the `./query` entry after Wallow-pu6a.5.5 deleted
 * the hand-written per-feature slices. Everything on this entry is now either
 * generated or one of the two curated invalidation predicates, so the pins are:
 * the generated artifacts are reachable, the curated ones are reachable, and the
 * retired hand-rolled factories are gone for good.
 */
import { describe, expect, it } from "vitest";

import * as queryEntry from "./index";

/** The three generated artifacts every operation gets, sampled across features. */
const GENERATED_EXPORTS: readonly string[] = [
  "organizationsGetAllOptions",
  "organizationsGetAllQueryKey",
  "organizationsCreateMutation",
  "usersGetCurrentUserOptions",
  "usersGetCurrentUserQueryKey",
  "organizationClientsListOptions",
  "organizationClientsRegisterMutation",
  "inquiriesGetAllOptions",
  "inquiriesSubmitMutation",
  "mfaGetStatusOptions",
  "mfaEnrollTotpMutation",
];

/** The curated layer — the only hand-written module left on this entry. */
const CURATED_EXPORTS: readonly string[] = ["queriesForOperation", "queriesWithTag"];

/**
 * The hand-written factories this entry used to expose. Deleted rather than
 * deprecated, because every one of them closed over the module-global client
 * that no longer exists.
 */
const RETIRED_EXPORTS: readonly string[] = [
  "appsQueries",
  "authQueries",
  "ensureQueryBootstrapped",
  "inquiriesQueries",
  "mfaQueries",
  "organizationsQueries",
  "queryKeys",
  "registerQueryBootstrap",
  "resetQueryBootstrapForTests",
  "settingsQueries",
  "userQueries",
];

describe("query barrel", () => {
  it.each([...GENERATED_EXPORTS, ...CURATED_EXPORTS])("exports %s", (name: string) => {
    expect((queryEntry as unknown as Record<string, unknown>)[name]).toBeTypeOf("function");
  });

  it.each(RETIRED_EXPORTS)("no longer exports the retired %s", (name: string) => {
    expect(Object.keys(queryEntry)).not.toContain(name);
  });

  it("builds a flat generated key carrying the operation id and its tags", () => {
    const [segment]: readonly unknown[] = queryEntry.organizationsGetAllQueryKey({
      baseUrl: "/api",
    });

    expect(segment).toMatchObject({ baseUrl: "/api", tags: ["Organizations"] });
  });
});
