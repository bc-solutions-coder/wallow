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
 * The per-client settings editor an application row opens: a refresh-token
 * lifetime field pre-filled from the row, whose Save PATCHes the client with
 * its URIs and scopes echoed back. Blank keeps the current lifetime (null on
 * the wire), an out-of-range value blocks Save before any request goes out,
 * and only application rows carry the trigger. The register stepper's own
 * optional lifetime field rides the registration POST. Runs the real SDK over
 * a faked fetch (`createSdkHarness`) mounted on the router context.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

const application = {
  clientId: "app-acme-dashboard",
  name: "Dashboard",
  kind: "application",
  status: "active",
  redirectUris: ["https://app.example/cb"],
  postLogoutRedirectUris: ["https://app.example/bye"],
  backchannelLogoutUri: "https://app.example/backchannel",
  backchannelLogoutSessionRequired: false,
  scopes: ["openid"],
  createdByUserId: "u1",
  createdAt: "2026-08-30T00:00:00Z",
  refreshTokenLifetime: 86_400,
};

const serviceAccount = {
  ...application,
  clientId: "sa-acme-nightly",
  name: "Nightly sync",
  kind: "service-account",
  redirectUris: [],
  refreshTokenLifetime: null,
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
];

/** What the register POST answers with on the success path. */
const registered = {
  client: application,
  clientSecret: "secret-xyz",
  issuer: "https://auth.example/auth",
  apiBaseUrl: "https://api.example",
};

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

function seedLoadedOrg(): void {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": [],
      "GET /v1/identity/organizations/o1/clients": [application, serviceAccount],
      "POST /v1/identity/organizations/o1/clients": registered,
      "PATCH /v1/identity/organizations/o1/clients/app-acme-dashboard": {
        ...application,
        refreshTokenLifetime: 3600,
      },
      "GET /v1/identity/scopes": scopes,
    },
    { fallback: [] },
  );
}

async function openSettings(): Promise<void> {
  await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
  await userEvent.click(page.getByTestId("organization-detail-application-settings"));
  await expect
    .element(page.getByTestId("organization-detail-client-settings-lifetime"))
    .toBeInTheDocument();
}

function lifetimeInput(): HTMLInputElement {
  return page
    .getByTestId("organization-detail-client-settings-lifetime")
    .element() as HTMLInputElement;
}

function saveButton(): HTMLButtonElement {
  return page
    .getByTestId("organization-detail-client-settings-save")
    .element() as HTMLButtonElement;
}

/** The PATCH requests recorded so far — the ledger reads never PATCH. */
function patchCalls() {
  return harness.calls.filter((call) => call.method === "PATCH");
}

describe("OrganizationDetail client settings editor", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("opens from an application row pre-filled with its lifetime; service accounts have no trigger", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openSettings();

    expect(lifetimeInput().value).toBe("86400");
    // Only applications hand out refresh tokens, so only their rows open the editor.
    expect(
      page.getByTestId("organization-detail-service-account-settings").elements(),
    ).toHaveLength(0);
  });

  it("saves the typed lifetime, echoing the client's URIs and scopes, then sweeps the ledger", async () => {
    seedLoadedOrg();

    const { queryClient } = renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");
    await openSettings();

    await userEvent.fill(page.getByTestId("organization-detail-client-settings-lifetime"), "3600");
    await userEvent.click(page.getByTestId("organization-detail-client-settings-save"));

    // The editor closes back to the ledger once the save lands.
    await expect
      .poll(() => page.getByTestId("organization-detail-client-settings-card").elements().length)
      .toBe(0);

    expect(patchCalls()).toHaveLength(1);
    expect(patchCalls()[0]?.body).toEqual({
      redirectUris: ["https://app.example/cb"],
      postLogoutRedirectUris: ["https://app.example/bye"],
      backchannelLogoutUri: "https://app.example/backchannel",
      backchannelLogoutSessionRequired: false,
      scopes: ["openid"],
      refreshTokenLifetime: 3600,
    });
    await expectSwept(invalidateSpy, organizationClientsListQueryKey({ path: { orgId: "o1" } }));
  });

  it("saves the toggled back-channel session switch", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openSettings();

    await userEvent.click(page.getByTestId("organization-detail-client-settings-session-required"));
    await userEvent.click(page.getByTestId("organization-detail-client-settings-save"));

    await expect
      .poll(() => page.getByTestId("organization-detail-client-settings-card").elements().length)
      .toBe(0);
    expect(patchCalls()[0]?.body).toMatchObject({ backchannelLogoutSessionRequired: true });
  });

  it("saves blank as null, keeping the current lifetime", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openSettings();

    await userEvent.fill(page.getByTestId("organization-detail-client-settings-lifetime"), "");
    await userEvent.click(page.getByTestId("organization-detail-client-settings-save"));

    await expect
      .poll(() => page.getByTestId("organization-detail-client-settings-card").elements().length)
      .toBe(0);
    expect(patchCalls()[0]?.body).toMatchObject({ refreshTokenLifetime: null });
  });

  it("blocks Save on an out-of-range value before any request goes out", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openSettings();

    await userEvent.fill(page.getByTestId("organization-detail-client-settings-lifetime"), "10");
    await expect
      .element(page.getByTestId("organization-detail-client-settings-error"))
      .toBeInTheDocument();
    expect(saveButton().disabled).toBe(true);
    expect(patchCalls()).toHaveLength(0);
  });
});

describe("register stepper refresh-token lifetime", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("carries a filled lifetime into the registration request", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("organization-detail-register-open"));

    await userEvent.fill(page.getByTestId("organization-detail-register-name"), "Dashboard");
    await userEvent.fill(
      page.getByTestId("organization-detail-register-refresh-token-lifetime"),
      "120",
    );
    await userEvent.click(page.getByTestId("organization-detail-register-next"));
    await userEvent.fill(
      page.getByTestId("organization-detail-register-redirect-uris"),
      "https://app.example/cb",
    );
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-success"))
      .toBeInTheDocument();
    const post = harness.calls.find(
      (call) => call.method === "POST" && call.path.endsWith("/organizations/o1/clients"),
    );
    expect(post?.body).toMatchObject({ refreshTokenLifetime: 120 });
  });

  it("carries the ticked back-channel session switch into the registration request", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("organization-detail-register-open"));

    await userEvent.fill(page.getByTestId("organization-detail-register-name"), "Dashboard");
    await userEvent.click(page.getByTestId("organization-detail-register-next"));
    await userEvent.fill(
      page.getByTestId("organization-detail-register-redirect-uris"),
      "https://app.example/cb",
    );
    await userEvent.click(
      page.getByTestId("organization-detail-register-backchannel-logout-session-required"),
    );
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-success"))
      .toBeInTheDocument();
    const post = harness.calls.find(
      (call) => call.method === "POST" && call.path.endsWith("/organizations/o1/clients"),
    );
    expect(post?.body).toMatchObject({ backchannelLogoutSessionRequired: true });
  });

  it("omits the lifetime from the registration request when left blank", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("organization-detail-register-open"));

    await userEvent.fill(page.getByTestId("organization-detail-register-name"), "Dashboard");
    await userEvent.click(page.getByTestId("organization-detail-register-next"));
    await userEvent.fill(
      page.getByTestId("organization-detail-register-redirect-uris"),
      "https://app.example/cb",
    );
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-success"))
      .toBeInTheDocument();
    const post = harness.calls.find(
      (call) => call.method === "POST" && call.path.endsWith("/organizations/o1/clients"),
    );
    expect(post?.body).not.toHaveProperty("refreshTokenLifetime");
  });
});
