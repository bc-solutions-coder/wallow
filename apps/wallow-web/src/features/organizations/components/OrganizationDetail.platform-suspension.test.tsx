import {
  createSdkHarness,
  failsWith,
  routeHarness,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { OrganizationDetail } from "./OrganizationDetail";

/**
 * Platform suspension on the org page: the operator's reason renders read-only
 * for everyone, while the place/lift controls exist only for a caller whose
 * current-user read carries the global-admin authority. Real SDK over a faked
 * fetch; the current user comes off the same `users/me` entry the dashboard
 * reads, so seeding that one route decides which controls exist.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "1" };
const suspendedOrg = {
  ...org,
  platformSuspendedAt: "2026-08-30T00:00:00Z",
  platformSuspensionReason: "Fraud investigation",
};

const orgAdmin = {
  id: "u1",
  email: "ada@acme.io",
  firstName: "Ada",
  lastName: "L",
  roles: [],
  permissions: [],
  isGlobalAdmin: false,
};
const globalAdmin = { ...orgAdmin, isGlobalAdmin: true };

const members = [
  { id: "u1", email: "ada@acme.io", firstName: "Ada", lastName: "L", enabled: true, roles: [] },
];

const application = {
  clientId: "app-acme-portal",
  name: "Portal",
  kind: "application",
  status: "active",
  redirectUris: ["https://portal.acme.io/callback"],
  postLogoutRedirectUris: [],
  scopes: ["openid"],
  createdByUserId: "u1",
  createdAt: "2026-08-30T00:00:00Z",
  lastRotatedByUserId: null,
  lastRotatedAt: null,
};
const platformSuspendedClient = {
  ...application,
  platformSuspendedAt: "2026-08-30T00:00:00Z",
  platformSuspensionReason: "Terms of service violation",
};

const ROW = "organization-detail-application";
const ORG_SUSPENSION_PATH = "/api/v1/identity/organizations/o1/platform-suspension";
const CLIENT_SUSPENSION_PATH =
  "/api/v1/identity/organizations/o1/clients/app-acme-portal/platform-suspension";

let harness: SdkHarness;

function seed(options: {
  me?: unknown;
  organization?: unknown;
  clients?: readonly unknown[];
  answers?: Record<string, unknown>;
}): void {
  routeHarness(
    harness,
    {
      "GET /v1/identity/users/me": options.me ?? orgAdmin,
      "GET /v1/identity/organizations/o1": options.organization ?? org,
      "GET /v1/identity/organizations/o1/members": members,
      "GET /v1/identity/organizations/o1/clients": options.clients ?? [],
      "GET /v1/identity/scopes": [],
      ...options.answers,
    },
    { fallback: [] },
  );
}

async function placeOrgSuspension(reason: string): Promise<void> {
  await userEvent.click(page.getByTestId("organization-detail-platform-suspend"));
  await expect
    .element(page.getByTestId("organization-detail-platform-suspend-popup"))
    .toBeInTheDocument();
  await userEvent.fill(page.getByTestId("organization-detail-platform-suspend-reason"), reason);
  await userEvent.click(page.getByTestId("organization-detail-platform-suspend-confirm"));
}

describe("OrganizationDetail platform suspension — org admin", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("shows the organization's suspension reason read-only", async () => {
    seed({ organization: suspendedOrg });

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await expect
      .element(page.getByTestId("organization-detail-platform-suspension"))
      .toHaveTextContent("Fraud investigation");
    expect(page.getByTestId("organization-detail-platform-lift").query()).toBeNull();
    expect(page.getByTestId("organization-detail-platform-suspend").query()).toBeNull();
  });

  it("shows a client's suspension reason without any control to lift it", async () => {
    seed({ clients: [platformSuspendedClient] });

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await expect
      .element(page.getByTestId(`${ROW}-platform-suspension`))
      .toHaveTextContent("Terms of service violation");
    expect(page.getByTestId(`${ROW}-platform-lift`).query()).toBeNull();
    expect(page.getByTestId(`${ROW}-platform-suspend`).query()).toBeNull();
  });
});

describe("OrganizationDetail platform suspension — global admin", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("places a suspension on the organization with a reason", async () => {
    seed({
      me: globalAdmin,
      answers: { "POST /v1/identity/organizations/o1/platform-suspension": null },
    });

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await expect
      .element(page.getByTestId("organization-detail-platform-suspend"))
      .toBeInTheDocument();

    await placeOrgSuspension("Fraud investigation");

    await expect
      .poll(() =>
        harness.calls.find((call) => call.method === "POST" && call.path === ORG_SUSPENSION_PATH),
      )
      .toMatchObject({ body: { reason: "Fraud investigation" } });
  });

  it("lifts the organization's suspension", async () => {
    seed({
      me: globalAdmin,
      organization: suspendedOrg,
      answers: { "DELETE /v1/identity/organizations/o1/platform-suspension": null },
    });

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await userEvent.click(page.getByTestId("organization-detail-platform-lift"));

    await expect
      .poll(() =>
        harness.calls.some((call) => call.method === "DELETE" && call.path === ORG_SUSPENSION_PATH),
      )
      .toBe(true);
  });

  it("shows why a placement was refused", async () => {
    seed({
      me: globalAdmin,
      answers: {
        "POST /v1/identity/organizations/o1/platform-suspension": failsWith(
          {
            code: "Identity.OrganizationSuspendedByPlatform",
            detail: "The organization is suspended by the platform",
            status: 422,
          },
          422,
        ),
      },
    });

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await expect
      .element(page.getByTestId("organization-detail-platform-suspend"))
      .toBeInTheDocument();

    await placeOrgSuspension("Fraud investigation");

    await expect
      .element(page.getByTestId("organization-detail-platform-error"))
      .toHaveTextContent("The organization is suspended by the platform");
  });

  it("places a suspension on a client with a reason", async () => {
    seed({
      me: globalAdmin,
      clients: [application],
      answers: { [`POST ${CLIENT_SUSPENSION_PATH.replace("/api", "")}`]: platformSuspendedClient },
    });

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await expect.element(page.getByTestId(`${ROW}-item`)).toBeInTheDocument();

    await userEvent.click(page.getByTestId(`${ROW}-platform-suspend`));
    await expect.element(page.getByTestId(`${ROW}-platform-suspend-popup`)).toBeInTheDocument();
    await userEvent.fill(
      page.getByTestId(`${ROW}-platform-suspend-reason`),
      "Terms of service violation",
    );
    await userEvent.click(page.getByTestId(`${ROW}-platform-suspend-confirm`));

    await expect
      .poll(() =>
        harness.calls.find(
          (call) => call.method === "POST" && call.path === CLIENT_SUSPENSION_PATH,
        ),
      )
      .toMatchObject({ body: { reason: "Terms of service violation" } });
  });

  it("lifts a client's suspension", async () => {
    seed({
      me: globalAdmin,
      clients: [platformSuspendedClient],
      answers: { [`DELETE ${CLIENT_SUSPENSION_PATH.replace("/api", "")}`]: application },
    });

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await userEvent.click(page.getByTestId(`${ROW}-platform-lift`));

    await expect
      .poll(() =>
        harness.calls.some(
          (call) => call.method === "DELETE" && call.path === CLIENT_SUSPENSION_PATH,
        ),
      )
      .toBe(true);
  });
});
