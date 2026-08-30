import {
  createSdkHarness,
  routeHarness,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { expectSwept } from "@bc-solutions-coder/testing/invalidation";
import { organizationClientsListQueryKey } from "../api";
import { OrganizationDetail } from "./OrganizationDetail";

/**
 * The register-service-account stepper on the org-detail Service accounts
 * ledger: Basics → Scopes with no Redirects step, the required-field gate on
 * Register (a name and a scope), ungrantable platform-only scopes, the
 * `kind: service-account` body it posts, and the one-time reveal whose env
 * block names the `OIDC_SERVICE_*` variables.
 */

const PREFIX = "organization-detail-register-service-account";

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

const serviceAccount = {
  clientId: "sa-acme-nightly-sync",
  name: "Nightly sync",
  kind: "service-account",
  status: "active",
  redirectUris: [],
  postLogoutRedirectUris: [],
  scopes: ["inquiries.read"],
  createdByUserId: "u1",
  createdAt: "2026-08-30T00:00:00Z",
};

const scopes = [
  {
    id: { value: "s1" },
    code: "inquiries.read",
    displayName: "Read inquiries",
    category: "Inquiries",
    description: null,
    isDefault: false,
    platformOnly: false,
  },
  {
    id: { value: "s2" },
    code: "users.manage",
    displayName: "Manage users",
    category: "Platform",
    description: null,
    isDefault: false,
    platformOnly: true,
  },
];

/** What the register POST answers with on the success path. */
const registered = {
  client: serviceAccount,
  clientSecret: "secret-sa",
  issuer: "https://auth.example/auth",
  apiBaseUrl: "https://api.example",
};

let harness: SdkHarness;

function seedLoadedOrg(): void {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": [],
      "GET /v1/identity/organizations/o1/clients": [],
      "POST /v1/identity/organizations/o1/clients": registered,
      "GET /v1/identity/scopes": scopes,
    },
    { fallback: [] },
  );
}

async function openStepper(): Promise<void> {
  await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
  await userEvent.click(page.getByTestId(`${PREFIX}-open`));
  await expect.element(page.getByTestId(`${PREFIX}-name`)).toBeInTheDocument();
}

function submitButton(): HTMLButtonElement {
  return page.getByTestId(`${PREFIX}-submit`).element() as HTMLButtonElement;
}

/** Drive the stepper from Basics straight to Scopes. */
async function fillNameAndReachScopes(): Promise<void> {
  await userEvent.fill(page.getByTestId(`${PREFIX}-name`), "Nightly sync");
  await userEvent.click(page.getByTestId(`${PREFIX}-next`));
  await expect.element(page.getByTestId(`${PREFIX}-scope-inquiries-read`)).toBeInTheDocument();
}

describe("OrganizationDetail register-service-account stepper", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("walks Basics then Scopes and gates Register on a name and a scope", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openStepper();

    await expect.element(page.getByTestId(`${PREFIX}-step`)).toHaveTextContent("Basics");
    expect(submitButton().disabled).toBe(true);
    await userEvent.fill(page.getByTestId(`${PREFIX}-name`), "Nightly sync");
    expect(submitButton().disabled).toBe(true);

    // A service account has no redirect URIs, so Next lands on Scopes, and the
    // application stepper's login scopes are not on offer.
    await userEvent.click(page.getByTestId(`${PREFIX}-next`));
    await expect.element(page.getByTestId(`${PREFIX}-step`)).toHaveTextContent("Scopes");
    expect(page.getByTestId(`${PREFIX}-redirect-uris`).elements()).toHaveLength(0);
    expect(page.getByTestId(`${PREFIX}-scope-openid`).elements()).toHaveLength(0);
    expect(submitButton().disabled).toBe(true);

    await userEvent.click(page.getByTestId(`${PREFIX}-scope-inquiries-read`));
    await expect.poll(() => submitButton().disabled).toBe(false);
    await userEvent.click(page.getByTestId(`${PREFIX}-scope-inquiries-read`));
    await expect.poll(() => submitButton().disabled).toBe(true);
  });

  it("renders platform-only scopes as ungrantable", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openStepper();
    await fillNameAndReachScopes();

    const platformOnly = page.getByTestId(`${PREFIX}-scope-users-manage`);
    await expect.element(platformOnly).toHaveAttribute("aria-disabled", "true");
    await expect
      .element(page.getByTestId(`${PREFIX}-scope-users-manage-platform-only`))
      .toHaveTextContent("Platform only");
  });

  it("posts a service-account body and reveals the service env block once", async () => {
    seedLoadedOrg();

    const { queryClient } = renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");
    await openStepper();
    await fillNameAndReachScopes();
    await userEvent.click(page.getByTestId(`${PREFIX}-scope-inquiries-read`));
    await userEvent.click(page.getByTestId(`${PREFIX}-submit`));

    await expect.element(page.getByTestId(`${PREFIX}-success`)).toBeInTheDocument();
    await expect
      .element(page.getByTestId(`${PREFIX}-client-id`))
      .toHaveTextContent("sa-acme-nightly-sync");
    await expect
      .element(page.getByTestId(`${PREFIX}-client-secret`))
      .toHaveTextContent("secret-sa");

    const post = harness.calls.find(
      (call) => call.method === "POST" && call.path.endsWith("/organizations/o1/clients"),
    );
    expect(post?.body).toMatchObject({
      kind: "service-account",
      name: "Nightly sync",
      redirectUris: [],
      postLogoutRedirectUris: [],
      scopes: ["inquiries.read"],
    });

    const env: string = page.getByTestId(`${PREFIX}-env`).element().textContent;
    expect(env.split("\n")).toEqual([
      "OIDC_ISSUER=https://auth.example/auth",
      "OIDC_SERVICE_CLIENT_ID=sa-acme-nightly-sync",
      "OIDC_SERVICE_CLIENT_SECRET=secret-sa",
      "OIDC_SERVICE_SCOPES=inquiries.read",
      "BFF_API_BASE_URL=https://api.example",
    ]);
    await expect
      .element(page.getByTestId(`${PREFIX}-quickstart`))
      .toHaveAttribute("href", expect.stringContaining("/api/service-accounts.html"));
    expect(page.getByTestId(`${PREFIX}-form`).elements()).toHaveLength(0);

    await expectSwept(invalidateSpy, organizationClientsListQueryKey({ path: { orgId: "o1" } }));
  });
});
