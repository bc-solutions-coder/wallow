import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "@shared/testing/harness-routes";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { expectSwept, sweeps } from "@shared/testing/invalidation";
import { clientsGetByTenantQueryKey, organizationsGetMembersQueryKey } from "../api";
import { OrganizationDetail } from "./OrganizationDetail";

/**
 * Org-detail bound-clients + register-client reachability spec
 * (Wallow-ffpq.3.6). Once an org loads, the detail page must surface the
 * org's bound clients (`organization-detail-clients-table`) and a
 * register-client form (display-name / client-type / redirect-uris / submit),
 * reachable straight from the org detail page.
 *
 * Data flows through the generated query options, so the network seam is the
 * `fetch` of the request-scoped client this spec's own `createSdkHarness()`
 * builds (Wallow-pu6a.5.5 — there is no shared module-global client left to
 * install a mock onto). The org / members / clients state is driven from the
 * transport rather than the cache: those three requests are in flight together,
 * so `routeHarness` answers each by URL rather than in call order.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

/** What the register POST answers with on the success path. */
const registered = {
  id: "c1",
  clientId: "client-abc",
  clientSecret: "secret-xyz",
  name: "Dashboard",
};

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

function seedLoadedOrg(): void {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": [],
      "GET /v1/identity/clients/by-tenant/o1": [],
      "POST /v1/identity/clients": registered,
    },
    { fallback: [] },
  );
}

describe("OrganizationDetail bound clients + register-client", () => {
  // Installed so the seeded queries resolve through the mocked client rather
  // than the real network if any refetch fires.

  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("renders the bound-clients table once the org loads", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
    await expect.element(page.getByTestId("organization-detail-clients-table")).toBeInTheDocument();
  });

  it("renders the register-client form fields reachable from the org detail page", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-display-name"))
      .toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-client-type"))
      .toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-redirect-uris"))
      .toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-submit"))
      .toBeInTheDocument();
  });

  it("sweeps the bound-clients query after a successful registration", async () => {
    // The row a registration creates only appears if the list this section reads
    // is invalidated — without this case the sweep in `RegisterClientFormFields`'
    // `onSuccess` can be deleted and the suite stays green (Wallow-lrlm.6.7).
    seedLoadedOrg();

    const { queryClient } = renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    // The controls arrive with the org read, so the form cannot be driven at
    // first paint. A non-empty display name is what gets past the zod schema.
    await expect
      .element(page.getByTestId("organization-detail-register-display-name"))
      .toBeInTheDocument();
    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    // Asserted by BEHAVIOUR, not identity: generated keys are flat
    // (`[{ _id, baseUrl, tags, ...args }]`), so `queriesForOperation(...)` hands
    // back an opaque predicate — run it against the real key instead.
    await expectSwept(invalidateSpy, clientsGetByTenantQueryKey({ path: { tenantId: "o1" } }));

    // And it is that operation the sweep names, not a blanket pass over the
    // cache: the members list on the same screen must be left alone.
    const membersKey = organizationsGetMembersQueryKey({ path: { id: "o1" } });
    expect(invalidateSpy.mock.calls.some((call) => sweeps(call[0], membersKey))).toBe(false);
  });
});
