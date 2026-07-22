import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import * as generated from "./generated";

// Guards T7.1: the committed OpenAPI snapshot and the regenerated TS client must
// reflect F6's reworked Identity surface. F6 deleted OrganizationDomainsController
// and the membership-request endpoints, and reworked AppsController's register
// request (RegisterAppRequest gained PostLogoutRedirectUris). Until the snapshot +
// client are regenerated against the live F6 API, these assertions fail.

interface OpenApiSpec {
  paths: Record<string, unknown>;
  components: { schemas: Record<string, { properties?: Record<string, unknown> }> };
}

function loadSnapshot(): OpenApiSpec {
  const snapshotUrl: URL = new URL("../openapi/v1.json", import.meta.url);
  return JSON.parse(readFileSync(fileURLToPath(snapshotUrl), "utf8")) as OpenApiSpec;
}

const DELETED_SURFACE_PREFIXES: readonly string[] = [
  "/v1/identity/organization-domains",
  "/v1/identity/membership-requests",
];

const DELETED_GENERATED_OPERATIONS: readonly string[] = [
  "getV1IdentityOrganizationDomainsMatch",
  "postV1IdentityMembershipRequests",
];

describe("OpenAPI snapshot reflects the F6-reworked surface", () => {
  it("no longer serves the deleted organization-domains / membership-request paths", () => {
    const spec: OpenApiSpec = loadSnapshot();
    const survivingDeletedPaths: string[] = Object.keys(spec.paths).filter((path: string) =>
      DELETED_SURFACE_PREFIXES.some((prefix: string) => path.startsWith(prefix)),
    );

    expect(survivingDeletedPaths).toEqual([]);
  });

  it("exposes the reworked AppsController register contract with postLogoutRedirectUris", () => {
    const spec: OpenApiSpec = loadSnapshot();
    const registerRequest = spec.components.schemas.RegisterAppRequest;

    expect(registerRequest).toBeDefined();
    expect(Object.keys(registerRequest.properties ?? {})).toContain("postLogoutRedirectUris");
  });

  it("still serves the AppsController register endpoint (surface preserved)", () => {
    const spec: OpenApiSpec = loadSnapshot();

    expect(spec.paths).toHaveProperty("/v1/identity/apps/register");
  });
});

describe("generated SDK client reflects the F6-reworked surface", () => {
  it("no longer exports the deleted-surface operations", () => {
    const exportedNames: string[] = Object.keys(generated);

    for (const operation of DELETED_GENERATED_OPERATIONS) {
      expect(exportedNames).not.toContain(operation);
    }
  });

  it("still exports the AppsController register operation", () => {
    const exportedNames: string[] = Object.keys(generated);

    expect(exportedNames).toContain("postV1IdentityAppsRegister");
  });
});
