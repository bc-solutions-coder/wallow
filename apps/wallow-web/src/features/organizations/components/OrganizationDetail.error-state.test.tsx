import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { OrganizationDetail } from "./OrganizationDetail";

/**
 * The org-detail page's two query error surfaces.
 *
 * A missing org never reaches this component: the route loader turns the API's
 * 404 into the router's not-found path before the page renders. What is left
 * for the page is a read that failed outright, which it renders as the failure
 * banner rather than as an empty org. The inner clients section reads a second
 * query and carries its own surface.
 */

const ORG_ID = "o1";

/** Wire paths, `/api`-prefixed by the harness's base URL. */
const DETAIL_PATH = `/api/v1/identity/organizations/${ORG_ID}`;
const MEMBERS_PATH = `/api/v1/identity/organizations/${ORG_ID}/members`;
const CLIENTS_PATH = `/api/v1/identity/organizations/${ORG_ID}/clients`;

const ORG = { id: ORG_ID, name: "Acme", domain: null, memberCount: "0" };

/** RFC 7807 bodies the SDK's error interceptor parses into `ApiFailure`s. */
const DETAIL_PROBLEM = {
  status: 500,
  code: "Server.Error",
  title: "Internal Server Error",
  detail: "Org lookup failed.",
};
const CLIENTS_PROBLEM = {
  status: 500,
  code: "Server.Error",
  title: "Internal Server Error",
  detail: "Clients failed.",
};

function json(body: unknown, status = 200): Response {
  return Response.json(body, { status });
}

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("OrganizationDetail — query error state", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the server-failure copy, not the 500's detail, when the org query errors", async () => {
    harness.rejectJson(DETAIL_PROBLEM, 500);

    renderWithWallow(<OrganizationDetail orgId={ORG_ID} />, { harness });

    await expect
      .element(page.getByTestId("organization-detail-error"))
      .toHaveTextContent("Something went wrong on our side. Please try again later.");
  });

  it("renders no org heading when the org query errors", async () => {
    harness.rejectJson(DETAIL_PROBLEM, 500);

    renderWithWallow(<OrganizationDetail orgId={ORG_ID} />, { harness });

    await expect.element(page.getByTestId("organization-detail-error")).toBeInTheDocument();
    expect(page.getByTestId("organization-detail-heading").elements()).toHaveLength(0);
  });

  it("retries the org read from the banner", async () => {
    let attempts = 0;
    harness.respond((call) => {
      if (call.path === DETAIL_PATH) {
        attempts += 1;
        return attempts === 1 ? json(DETAIL_PROBLEM, 500) : json(ORG);
      }
      return json([]);
    });

    renderWithWallow(<OrganizationDetail orgId={ORG_ID} />, { harness });

    await expect.element(page.getByTestId("organization-detail-error")).toBeInTheDocument();
    await userEvent.click(page.getByRole("button", { name: "Try again" }));

    await expect.element(page.getByTestId("organization-detail-heading")).toHaveTextContent("Acme");
  });
});

describe("OrganizationDetail — bound clients query error state", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the server-failure copy, not the 500's detail, when only the clients query errors", async () => {
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
      .toHaveTextContent("Something went wrong on our side. Please try again later.");
    expect(page.getByTestId("organization-detail-applications-table").elements()).toHaveLength(0);
  });
});
