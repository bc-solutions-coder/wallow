import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { OrganizationDetail } from "./OrganizationDetail";

/**
 * The org-detail page's two query error surfaces.
 *
 * `org === null` is reached by an errored read as much as by a resolved-empty
 * one, so the error branch has to be checked FIRST — otherwise a 500 renders
 * the flatly wrong "Organization not found." card. The inner clients section
 * reads a second query and carries its own surface.
 */

const ORG_ID = "o1";

/** Wire paths, `/api`-prefixed by the harness's base URL. */
const DETAIL_PATH = `/api/v1/identity/organizations/${ORG_ID}`;
const MEMBERS_PATH = `/api/v1/identity/organizations/${ORG_ID}/members`;
const CLIENTS_PATH = `/api/v1/identity/organizations/${ORG_ID}/clients`;

const ORG = { id: ORG_ID, name: "Acme", domain: null, memberCount: "0" };

/** RFC 7807 bodies the SDK's error interceptor brands as `WallowError`s. */
const DETAIL_PROBLEM = {
  status: 500,
  title: "Internal Server Error",
  detail: "Org lookup failed.",
};
const CLIENTS_PROBLEM = { status: 500, title: "Internal Server Error", detail: "Clients failed." };

function json(body: unknown, status = 200): Response {
  return Response.json(body, { status });
}

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("OrganizationDetail — query error state", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the ProblemDetails detail when the org query errors", async () => {
    harness.rejectJson(DETAIL_PROBLEM, 500);

    renderWithWallow(<OrganizationDetail orgId={ORG_ID} />, { harness });

    await expect
      .element(page.getByTestId("organization-detail-error"))
      .toHaveTextContent("Org lookup failed.");
  });

  it("does not render the not-found card when the org query errors", async () => {
    harness.rejectJson(DETAIL_PROBLEM, 500);

    renderWithWallow(<OrganizationDetail orgId={ORG_ID} />, { harness });

    await expect.element(page.getByTestId("organization-detail-error")).toBeInTheDocument();
    expect(page.getByTestId("organization-detail-not-found").elements()).toHaveLength(0);
    expect(page.getByTestId("organization-detail-heading").elements()).toHaveLength(0);
  });

  it("still renders the not-found card when the org query resolves empty", async () => {
    // The other half of the split: the error branch must not swallow the
    // resolved-null case.
    harness.resolveJson(null);

    renderWithWallow(<OrganizationDetail orgId={ORG_ID} />, { harness });

    await expect.element(page.getByTestId("organization-detail-not-found")).toBeInTheDocument();
    expect(page.getByTestId("organization-detail-error").elements()).toHaveLength(0);
  });
});

describe("OrganizationDetail — bound clients query error state", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the ProblemDetails detail when only the clients query errors", async () => {
    // The org and members reads succeed, so the page renders in full and the
    // clients section is the only surface that has to speak up.
    harness.respond((call) => {
      if (call.path === CLIENTS_PATH) {
        return json(CLIENTS_PROBLEM, 500);
      }
      if (call.path === MEMBERS_PATH) {
        return json([]);
      }
      return json(call.path === DETAIL_PATH ? ORG : {});
    });

    renderWithWallow(<OrganizationDetail orgId={ORG_ID} />, { harness });

    await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-clients-error"))
      .toHaveTextContent("Clients failed.");
    expect(page.getByTestId("organization-detail-applications-table").elements()).toHaveLength(0);
  });
});
