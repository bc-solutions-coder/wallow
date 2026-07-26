import { describe, expect, it } from "vitest";

import {
  appsQueries,
  authQueries,
  ensureQueryBootstrapped,
  inquiriesQueries,
  mfaQueries,
  organizationsQueries,
  queryKeys,
  registerQueryBootstrap,
  settingsQueries,
  userQueries,
} from "./index";

describe("query barrel", () => {
  it("re-exports the bootstrap seam and key factory", () => {
    expect(registerQueryBootstrap).toBeTypeOf("function");
    expect(ensureQueryBootstrapped).toBeTypeOf("function");
    expect(queryKeys.organizations.all).toEqual(["orgs"]);
  });

  it("re-exports every domain query namespace", () => {
    expect(organizationsQueries).toBeDefined();
    expect(appsQueries).toBeDefined();
    expect(settingsQueries).toBeDefined();
    expect(mfaQueries).toBeDefined();
    expect(inquiriesQueries).toBeDefined();
    expect(userQueries).toBeDefined();
    expect(authQueries).toBeDefined();
  });
});
