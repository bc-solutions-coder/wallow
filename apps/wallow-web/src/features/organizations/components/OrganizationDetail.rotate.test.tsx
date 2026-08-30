import {
  createSdkHarness,
  failsWith,
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
 * Rotating a client's secret from the org page: the row names who created
 * the client and who last rotated it, the rotate dialog sends the
 * revoke-active-tokens choice, and the reveal that follows regenerates the
 * env block around the NEW secret. Real SDK over a faked fetch.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

const members = [
  { id: "u1", email: "ada@acme.io", firstName: "Ada", lastName: "L", enabled: true, roles: [] },
  { id: "u2", email: "bob@acme.io", firstName: "", lastName: "", enabled: true, roles: [] },
];

const serviceAccount = {
  clientId: "sa-acme-nightly",
  name: "Nightly sync",
  kind: "service-account",
  status: "active",
  redirectUris: [],
  postLogoutRedirectUris: [],
  scopes: ["organizations.read"],
  createdByUserId: "u1",
  createdAt: "2026-08-30T00:00:00Z",
  lastRotatedByUserId: null,
  lastRotatedAt: null,
};

const rotatedByBob = {
  ...serviceAccount,
  lastRotatedByUserId: "u2",
  lastRotatedAt: "2026-08-31T09:00:00Z",
};

/** What the rotate POST answers with: the same client, a fresh secret. */
const rotated = {
  client: rotatedByBob,
  clientSecret: "secret-new",
  issuer: "https://auth.example/auth",
  apiBaseUrl: "https://api.example",
};

let harness: SdkHarness;

const ROTATE_PATH = "/api/v1/identity/organizations/o1/clients/sa-acme-nightly/rotate-secret";

function seed(clients: readonly unknown[], rotateAnswer: unknown = rotated): void {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": members,
      "GET /v1/identity/organizations/o1/clients": clients,
      "POST /v1/identity/organizations/o1/clients/sa-acme-nightly/rotate-secret": rotateAnswer,
      "GET /v1/identity/scopes": [],
    },
    { fallback: [] },
  );
}

async function openRotateDialog(): Promise<void> {
  await expect
    .element(page.getByTestId("organization-detail-service-account-item"))
    .toBeInTheDocument();
  await userEvent.click(page.getByTestId("organization-detail-service-account-rotate"));
  await expect
    .element(page.getByTestId("organization-detail-service-account-rotate-popup"))
    .toBeInTheDocument();
}

describe("OrganizationDetail secret rotation", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("names who created the client and who last rotated its secret", async () => {
    seed([serviceAccount, { ...rotatedByBob, clientId: "sa-acme-other", name: "Other" }]);

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    const created = page.getByTestId("organization-detail-service-account-created");
    await expect.element(created.first()).toHaveTextContent("Created by Ada L");
    const rotatedLines = page.getByTestId("organization-detail-service-account-rotated");
    await expect.element(rotatedLines.first()).toHaveTextContent("Secret never rotated");
    // A member with no name falls back to their email.
    await expect.element(rotatedLines.nth(1)).toHaveTextContent("Secret rotated by bob@acme.io");
  });

  it("sends the revoke choice and regenerates the env block around the new secret", async () => {
    seed([serviceAccount]);

    const { queryClient } = renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");
    await openRotateDialog();

    await userEvent.click(page.getByTestId("organization-detail-service-account-rotate-revoke"));
    await userEvent.click(page.getByTestId("organization-detail-service-account-rotate-confirm"));

    await expect
      .element(page.getByTestId("organization-detail-register-service-account-success"))
      .toHaveTextContent("Secret rotated");
    await expect
      .element(page.getByTestId("organization-detail-register-service-account-client-secret"))
      .toHaveTextContent("secret-new");
    const env: string = page
      .getByTestId("organization-detail-register-service-account-env")
      .element().textContent;
    expect(env).toContain("OIDC_SERVICE_CLIENT_ID=sa-acme-nightly");
    expect(env).toContain("OIDC_SERVICE_CLIENT_SECRET=secret-new");
    expect(env).toContain("BFF_API_BASE_URL=https://api.example");

    const post = harness.calls.find((call) => call.method === "POST" && call.path === ROTATE_PATH);
    expect(post?.body).toEqual({ revokeActiveTokens: true });

    // The dialog closes (after its exit transition) and the ledger is re-read
    // for the new "rotated by" line.
    await expect
      .element(page.getByTestId("organization-detail-service-account-rotate-popup"))
      .not.toBeInTheDocument();
    await expectSwept(invalidateSpy, organizationClientsListQueryKey({ path: { orgId: "o1" } }));
  });

  it("sends revokeActiveTokens=false when the option is left unticked", async () => {
    seed([serviceAccount]);

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openRotateDialog();
    await userEvent.click(page.getByTestId("organization-detail-service-account-rotate-confirm"));

    await expect
      .element(page.getByTestId("organization-detail-register-service-account-success"))
      .toBeInTheDocument();
    const post = harness.calls.find((call) => call.method === "POST" && call.path === ROTATE_PATH);
    expect(post?.body).toEqual({ revokeActiveTokens: false });
  });

  it("keeps the dialog open and shows the error when rotation fails", async () => {
    seed(
      [serviceAccount],
      failsWith({ title: "Rotation is not allowed right now.", status: 409 }, 409),
    );

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openRotateDialog();
    await userEvent.click(page.getByTestId("organization-detail-service-account-rotate-confirm"));

    await expect
      .element(page.getByTestId("organization-detail-service-account-rotate-error"))
      .toHaveTextContent("Rotation is not allowed right now.");
    await expect
      .element(page.getByTestId("organization-detail-service-account-rotate-popup"))
      .toBeInTheDocument();
  });
});
