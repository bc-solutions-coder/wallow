import {
  createSdkHarness,
  routeHarness,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { DEFAULT_AUTH_URL } from "@bc-solutions-coder/env/auth-origin";
import { forkBranding } from "@bc-solutions-coder/styles";
import { expectSwept } from "@bc-solutions-coder/testing/invalidation";
import { organizationClientBrandingGetBrandingQueryKey } from "../api";
import { OrganizationDetail } from "./OrganizationDetail";

/**
 * The application row's branding editor: the live sign-in preview tracking the
 * draft, the fork's own app name refused before any request, the "Preview
 * sign-in" link addressing the real sign-in screen for this client, and the
 * one multipart PUT a save issues.
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

/** What registration created: the display name mirrors the client name. */
const savedBranding = {
  clientId: "app-acme-dashboard",
  displayName: "Dashboard",
  tagline: null,
  logoUrl: null,
  themeJson: null,
};

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

function seedLoadedOrg(): void {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": [],
      "GET /v1/identity/organizations/o1/clients": [application],
      "GET /v1/identity/organizations/o1/clients/app-acme-dashboard/branding": savedBranding,
      "PUT /v1/identity/organizations/o1/clients/app-acme-dashboard/branding": savedBranding,
    },
    { fallback: [] },
  );
}

async function openEditor(): Promise<void> {
  await expect.element(page.getByTestId("organization-detail-application-branding")).toBeVisible();
  await userEvent.click(page.getByTestId("organization-detail-application-branding"));
  await expect
    .element(page.getByTestId("organization-detail-branding-display-name"))
    .toBeInTheDocument();
}

describe("OrganizationDetail branding editor", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("previews the draft live as the fields change", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openEditor();

    // The editor opens on what registration saved, previewed as-is.
    const previewName = page.getByTestId("organization-detail-branding-preview-name");
    await expect.element(previewName).toHaveTextContent("Dashboard");

    await userEvent.fill(
      page.getByTestId("organization-detail-branding-display-name"),
      "Acme Dashboard",
    );
    await expect.element(previewName).toHaveTextContent("Acme Dashboard");

    await userEvent.fill(
      page.getByTestId("organization-detail-branding-tagline"),
      "Sign in to Acme",
    );
    await expect
      .element(page.getByTestId("organization-detail-branding-preview-tagline"))
      .toHaveTextContent("Sign in to Acme");

    // Both modes filled so the assertion holds whichever mode the fork
    // defaults to — the preview renders the default mode's variables inline.
    await userEvent.fill(
      page.getByTestId("organization-detail-branding-light-primary"),
      "rgb(1, 2, 3)",
    );
    await userEvent.fill(
      page.getByTestId("organization-detail-branding-dark-primary"),
      "rgb(1, 2, 3)",
    );
    await expect
      .poll(() =>
        page.getByTestId("organization-detail-branding-preview").element().getAttribute("style"),
      )
      .toContain("--primary: rgb(1, 2, 3)");
  });

  it("refuses the fork's own app name before any request is made", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openEditor();

    await userEvent.fill(
      page.getByTestId("organization-detail-branding-display-name"),
      forkBranding.appName,
    );

    await expect
      .element(page.getByTestId("organization-detail-branding-error"))
      .toHaveTextContent("reserved for the platform");
    const save = page.getByTestId("organization-detail-branding-save").element();
    expect((save as HTMLButtonElement).disabled).toBe(true);
    expect(harness.calls.some((call) => call.method === "PUT")).toBe(false);
  });

  it("links Preview sign-in to the sign-in screen for this client", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openEditor();

    await expect
      .element(page.getByTestId("organization-detail-branding-preview-signin"))
      .toHaveAttribute("href", `${DEFAULT_AUTH_URL}/login?client_id=app-acme-dashboard`);
  });

  it("saves the draft in one PUT, sweeps the branding read, and closes", async () => {
    seedLoadedOrg();

    const { queryClient } = renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");
    await openEditor();

    await userEvent.fill(
      page.getByTestId("organization-detail-branding-tagline"),
      "Sign in to Acme",
    );
    await userEvent.click(page.getByTestId("organization-detail-branding-save"));

    // The editor closes back to the ledger once the PUT lands.
    await expect
      .poll(() => page.getByTestId("organization-detail-branding-card").elements())
      .toHaveLength(0);

    const put = harness.calls.find((call) => call.method === "PUT");
    expect(put?.path).toContain("/clients/app-acme-dashboard/branding");
    expect(put?.body).toMatchObject({ DisplayName: "Dashboard", Tagline: "Sign in to Acme" });
    await expectSwept(
      invalidateSpy,
      organizationClientBrandingGetBrandingQueryKey({
        path: { orgId: "o1", clientId: "app-acme-dashboard" },
      }),
    );
  });
});
