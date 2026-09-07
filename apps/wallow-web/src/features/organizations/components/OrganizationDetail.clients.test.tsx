import {
  createSdkHarness,
  routeHarness,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { expectSwept, sweeps } from "@bc-solutions-coder/testing/invalidation";
import { organizationClientsListQueryKey, organizationsGetMembersQueryKey } from "../api";
import { OrganizationDetail } from "./OrganizationDetail";

/**
 * The org-detail client ledgers and the register-application stepper: the two
 * ledgers, the required-field gate on Register, ungrantable platform-only
 * scopes, the one-time reveal, and the sweep a registration issues.
 *
 * Runs the real SDK over a faked fetch (`createSdkHarness`) mounted on the
 * router context; `routeHarness` answers each in-flight read by URL.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

const application = {
  clientId: "app-acme-dashboard",
  name: "Dashboard",
  kind: "application",
  status: "active",
  redirectUris: ["https://app.example/cb"],
  postLogoutRedirectUris: [],
  scopes: ["openid"],
  createdByUserId: "u1",
  createdAt: "2026-08-30T00:00:00Z",
};

const serviceAccount = {
  ...application,
  clientId: "sa-acme-nightly",
  name: "Nightly sync",
  kind: "service-account",
  redirectUris: [],
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
  client: application,
  clientSecret: "secret-xyz",
  issuer: "https://auth.example/auth",
  apiBaseUrl: "https://api.example",
};

const telemetryConfiguration = {
  endpoint: "https://telemetry.example.com",
  credential: "telemetry-id.server-secret",
  environment: "production",
  release: "unknown",
};

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

function seedLoadedOrg(clients: readonly unknown[] = [], registration: unknown = registered): void {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": [],
      "GET /v1/identity/organizations/o1/clients": clients,
      "POST /v1/identity/organizations/o1/clients": registration,
      "GET /v1/identity/scopes": scopes,
      "POST /v1/identity/organizations/o1/clients/app-acme-dashboard/observability/rotate": {
        status: { status: "pending-rotation", revision: 2, acknowledgedRevision: 1 },
        configuration: { ...telemetryConfiguration, credential: "replacement.new-secret" },
      },
      "POST /v1/identity/organizations/o1/clients/app-acme-dashboard/observability/revoke": {
        status: "pending-revocation",
        revision: 2,
        acknowledgedRevision: 1,
      },
      "POST /v1/identity/organizations/o1/clients/app-acme-dashboard/observability/disable": {
        status: "pending-revocation",
        revision: 2,
        acknowledgedRevision: 1,
      },
      "POST /v1/identity/organizations/o1/clients/app-acme-dashboard/observability": {
        status: { status: "pending", revision: 1, acknowledgedRevision: 0 },
        configuration: telemetryConfiguration,
      },
    },
    { fallback: [] },
  );
}

async function openStepper(): Promise<void> {
  await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
  await userEvent.click(page.getByTestId("organization-detail-register-open"));
  await expect.element(page.getByTestId("organization-detail-register-name")).toBeInTheDocument();
}

function submitButton(): HTMLButtonElement {
  return page.getByTestId("organization-detail-register-submit").element() as HTMLButtonElement;
}

/** Drive the stepper through Basics and Redirects to the Scopes step. */
async function fillRequiredAndReachScopes(): Promise<void> {
  await userEvent.fill(page.getByTestId("organization-detail-register-name"), "Dashboard");
  await userEvent.click(page.getByTestId("organization-detail-register-next"));
  await userEvent.fill(
    page.getByTestId("organization-detail-register-redirect-uris"),
    "https://app.example/cb",
  );
  await userEvent.click(page.getByTestId("organization-detail-register-next"));
  await expect
    .element(page.getByTestId("organization-detail-register-scope-openid"))
    .toBeInTheDocument();
}

describe("OrganizationDetail client ledgers", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("renders the applications and service-accounts ledgers once the org loads", async () => {
    seedLoadedOrg([application, serviceAccount]);

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-applications-heading"))
      .toHaveTextContent("Applications");
    await expect
      .element(page.getByTestId("organization-detail-service-accounts-heading"))
      .toHaveTextContent("Service accounts");

    const applications = page.getByTestId("organization-detail-application-item");
    await expect.element(applications).toHaveTextContent("Dashboard");
    await expect.element(applications).toHaveTextContent("app-acme-dashboard");
    expect(applications.elements()).toHaveLength(1);

    const serviceAccounts = page.getByTestId("organization-detail-service-account-item");
    await expect.element(serviceAccounts).toHaveTextContent("sa-acme-nightly");
    expect(serviceAccounts.elements()).toHaveLength(1);
  });

  it("shows an empty state per ledger when the org has no clients", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await expect
      .element(page.getByTestId("organization-detail-applications-empty"))
      .toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-service-accounts-empty"))
      .toBeInTheDocument();
  });
});

describe("OrganizationDetail register-application stepper", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("opens on Basics and gates Register on name, a redirect URI and a scope", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openStepper();

    // Nothing filled: Register is disabled, Next is not — only REQUIRED fields
    // gate Register, so moving between steps is never blocked.
    expect(submitButton().disabled).toBe(true);
    await userEvent.fill(page.getByTestId("organization-detail-register-name"), "   ");
    expect(submitButton().disabled).toBe(true);
    await userEvent.fill(page.getByTestId("organization-detail-register-name"), "Dashboard");
    expect(submitButton().disabled).toBe(true);

    await userEvent.click(page.getByTestId("organization-detail-register-next"));
    await expect
      .element(page.getByTestId("organization-detail-register-redirect-uris"))
      .toBeInTheDocument();
    expect(submitButton().disabled).toBe(true);
    await userEvent.fill(
      page.getByTestId("organization-detail-register-redirect-uris"),
      "https://app.example/cb",
    );
    // `openid` is pre-selected, so the third requirement is already met.
    await expect.poll(() => submitButton().disabled).toBe(false);

    await userEvent.click(page.getByTestId("organization-detail-register-next"));
    await userEvent.click(page.getByTestId("organization-detail-register-scope-openid"));
    await expect.poll(() => submitButton().disabled).toBe(true);
  });

  it("renders platform-only scopes as ungrantable", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openStepper();
    await fillRequiredAndReachScopes();

    const grantable = page.getByTestId("organization-detail-register-scope-inquiries-read");
    await expect.element(grantable).toHaveAttribute("aria-checked", "false");
    await expect.element(grantable).not.toHaveAttribute("aria-disabled", "true");

    const platformOnly = page.getByTestId("organization-detail-register-scope-users-manage");
    await expect.element(platformOnly).toHaveAttribute("aria-disabled", "true");
    await expect
      .element(page.getByTestId("organization-detail-register-scope-users-manage-platform-only"))
      .toHaveTextContent("Platform only");

    await userEvent.click(grantable);
    await expect.element(grantable).toHaveAttribute("aria-checked", "true");
  });

  it("reveals the id, secret and env block once, then sweeps the ledger", async () => {
    seedLoadedOrg();

    const { queryClient } = renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");
    await openStepper();
    await fillRequiredAndReachScopes();
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-success"))
      .toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-client-id"))
      .toHaveTextContent("app-acme-dashboard");
    await expect
      .element(page.getByTestId("organization-detail-register-client-secret"))
      .toHaveTextContent("secret-xyz");

    const env: string = page.getByTestId("organization-detail-register-env").element().textContent;
    expect(env).toContain("OIDC_ISSUER=https://auth.example/auth");
    expect(env).toContain("OIDC_CLIENT_ID=app-acme-dashboard");
    expect(env).toContain("OIDC_CLIENT_SECRET=secret-xyz");
    expect(env).toContain("OIDC_REDIRECT_URI=https://app.example/cb");
    expect(env).toContain("OIDC_SCOPES=openid");
    expect(env).toContain("BFF_API_BASE_URL=https://api.example");
    expect(env).toMatch(/COOKIE_PASSWORD=[0-9a-f]{64}/);

    await expect
      .element(page.getByTestId("organization-detail-register-quickstart"))
      .toHaveAttribute("href", expect.stringContaining("/integrations/bff-pattern.html"));
    // The stepper is gone: the secret is shown once and the form does not
    // linger behind it to mint a second one by accident.
    expect(page.getByTestId("organization-detail-register-form").elements()).toHaveLength(0);

    // Asserted by BEHAVIOUR, not identity: generated keys are flat, so
    // `queriesForOperation(...)` hands back an opaque predicate — run it against
    // the real key instead.
    await expectSwept(invalidateSpy, organizationClientsListQueryKey({ path: { orgId: "o1" } }));
    const membersKey = organizationsGetMembersQueryKey({ path: { id: "o1" } });
    expect(invalidateSpy.mock.calls.some((call) => sweeps(call[0], membersKey))).toBe(false);
  });
  it("opts into observability and includes the separate one-time server configuration", async () => {
    seedLoadedOrg([], {
      ...registered,
      client: {
        ...application,
        telemetry: { status: "pending", revision: 1, acknowledgedRevision: 0 },
      },
      telemetry: {
        endpoint: "https://telemetry.example.com",
        credential: "telemetry-id.server-secret",
        environment: "production",
        release: "unknown",
      },
    });
    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openStepper();
    const checkbox = page.getByRole("checkbox", { name: "Enable observability" });
    await expect.element(checkbox).not.toBeChecked();
    await userEvent.click(checkbox);
    await fillRequiredAndReachScopes();
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));
    await expect
      .poll(
        () =>
          harness.calls.find((call) => call.method === "POST" && call.path.endsWith("/clients"))
            ?.body,
      )
      .toMatchObject({ enableObservability: true });
    await expect
      .element(page.getByTestId("organization-detail-register-env"))
      .toHaveTextContent("WALLOW_TELEMETRY_CREDENTIAL=telemetry-id.server-secret");
    await expect
      .element(page.getByTestId("organization-detail-register-env"))
      .toHaveTextContent("WALLOW_TELEMETRY_ENDPOINT=https://telemetry.example.com");
    await expect
      .element(page.getByText("Observability: Pending", { exact: true }))
      .toBeInTheDocument();
  });
  it("enables an existing application and clears the one-time reveal when dismissed", async () => {
    seedLoadedOrg([application]);
    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await userEvent.click(page.getByRole("button", { name: "Enable observability", exact: true }));
    await expect.element(page.getByRole("dialog")).toBeInTheDocument();
    await userEvent.click(page.getByRole("button", { name: "Copy server configuration" }));
    expect(await navigator.clipboard.readText()).toContain(
      "WALLOW_TELEMETRY_CREDENTIAL=telemetry-id.server-secret",
    );
    await userEvent.click(page.getByRole("button", { name: "Done", exact: true }));
    await expect.element(page.getByRole("dialog")).not.toBeInTheDocument();
    expect(
      page.getByText("WALLOW_TELEMETRY_CREDENTIAL=telemetry-id.server-secret").elements(),
    ).toHaveLength(0);
  });

  it("shows acknowledged collection and permanent provisioning failures in the ledger", async () => {
    seedLoadedOrg([
      { ...application, telemetry: { status: "active", revision: 1, acknowledgedRevision: 1 } },
      {
        ...serviceAccount,
        telemetry: {
          status: "action-required",
          revision: 1,
          acknowledgedRevision: 0,
          failure: "Provisioning was rejected. Contact the platform operator.",
        },
      },
    ]);
    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await expect
      .element(page.getByText("Observability: Active", { exact: true }))
      .toBeInTheDocument();
    await expect
      .element(page.getByText("Observability: Action required", { exact: true }))
      .toBeInTheDocument();
    await expect
      .element(
        page.getByText("Provisioning was rejected. Contact the platform operator.", {
          exact: true,
        }),
      )
      .toBeInTheDocument();
  });
  it.each(["pending", "action-required"])(
    "refreshes %s provisioning without navigation",
    async (status) => {
      seedLoadedOrg([
        { ...application, telemetry: { status, revision: 1, acknowledgedRevision: 0 } },
      ]);
      renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
      await expect
        .element(
          page.getByText(
            status === "pending" ? "Observability: Pending" : "Observability: Action required",
            { exact: true },
          ),
        )
        .toBeInTheDocument();
      seedLoadedOrg([
        { ...application, telemetry: { status: "active", revision: 1, acknowledgedRevision: 1 } },
      ]);
      await expect
        .poll(() => page.getByText("Observability: Active", { exact: true }).elements().length, {
          timeout: 10000,
        })
        .toBe(1);
    },
    15000,
  );
  it("reveals a rotated credential once and submits immediate revocation separately", async () => {
    seedLoadedOrg([
      { ...application, telemetry: { status: "active", revision: 1, acknowledgedRevision: 1 } },
    ]);
    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await userEvent.click(
      page.getByRole("button", { name: "Rotate telemetry credential", exact: true }),
    );
    await expect.element(page.getByRole("dialog")).toBeInTheDocument();
    await userEvent.click(page.getByRole("button", { name: "Copy server configuration" }));
    expect(await navigator.clipboard.readText()).toContain("replacement.new-secret");
    await userEvent.click(page.getByRole("button", { name: "Done", exact: true }));
    await expect.element(page.getByRole("dialog")).not.toBeInTheDocument();
    await userEvent.click(
      page.getByRole("button", { name: "Revoke telemetry credential", exact: true }),
    );
    await expect
      .poll(() =>
        harness.calls.some(
          (call) => call.method === "POST" && call.path.endsWith("/observability/revoke"),
        ),
      )
      .toBe(true);
  });
});
