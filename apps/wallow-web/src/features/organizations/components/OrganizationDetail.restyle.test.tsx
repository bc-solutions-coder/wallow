import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "../../../test/harness-routes";
import { beforeEach, describe, expect, it } from "vitest";

import {
  allByTestId,
  byTestId,
  expectClasses,
  expectTag,
  expectTokenColorsOnly,
  parentOf,
  waitForTestId,
  within,
} from "../../../test/style-contract";
import { OrganizationDetail } from "./OrganizationDetail";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Restyle spec for the org-detail body (Wallow-urec.4.3). It asserts only the
 * chrome the restyle adds; every behaviour (which state renders when, the
 * archive/reactivate mutations, the register-client flow, and all the
 * `organization-detail-*` testids) stays pinned by `OrganizationDetail.test.tsx`
 * and `OrganizationDetail.clients.test.tsx`, which the restyle must not edit.
 *
 * The structural point of this page: the Blazor original
 * (`2e039fcb:...Dashboard/OrganizationDetail.razor`) is NOT one giant card — the
 * page is a plain column and each section (members, bound clients, register
 * client) is its own card surface. Today the whole component is wrapped in a
 * single `ui` `Card`, whose `p-6 space-y-6` fights the recipe's `px-6 py-4`
 * table cells exactly as it did on the apps list (Wallow-urec.4.1), so the outer
 * card goes and the sections gain their own surfaces.
 *
 * New testids (pure additions — no spec pins a heading or the register form
 * element today): `organization-detail-clients-heading`,
 * `organization-detail-register-form`.
 */

const ORG = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

const MEMBERS = [
  {
    id: "u1",
    email: "ada@acme.io",
    firstName: "Ada",
    lastName: "L",
    enabled: true,
    roles: ["Owner"],
  },
];

const CLIENTS = [{ id: "c1", clientId: "acme-web", name: "Acme Web" }];

/** Render the loaded detail (org + members + bound clients seeded). */
async function renderLoaded(): Promise<HTMLElement> {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": ORG,
      "GET /v1/identity/organizations/o1/members": MEMBERS,
      "GET /v1/identity/clients/by-tenant/o1": CLIENTS,
    },
    { fallback: [] },
  );
  renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
  return waitForTestId("organization-detail-heading");
}

/** Render the missing-org branch. */
async function renderNotFound(): Promise<HTMLElement> {
  harness.resolveJson(null);
  renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
  return waitForTestId("organization-detail-not-found");
}

describe("OrganizationDetail (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("lays the page out as a column rather than one giant card", async () => {
    await renderLoaded();

    const root = parentOf(byTestId("organization-detail-back-link"));
    expectTag(root, "div");
    expect(
      root.classList.contains("bg-card"),
      "the detail page is a column of section cards, not a single card surface",
    ).toBe(false);
    expectClasses(root, "space-y-8");
  });

  it("styles the back link as a gold text link", async () => {
    await renderLoaded();

    const link = byTestId("organization-detail-back-link");
    expectClasses(link, "text-sm text-primary hover:opacity-80 no-underline inline-block");
    // Regression guard: the same link, to the same place, with the same words.
    expect(link.getAttribute("href")).toBe("/dashboard/organizations");
    expect(link.textContent?.trim()).toBe("Back to organizations");
  });

  it("titles the page with the org name as an h1", async () => {
    await renderLoaded();

    const heading = byTestId("organization-detail-heading");
    expectTag(heading, "h1");
    expect(heading.textContent).toBe("Acme");
    expectClasses(heading, "text-3xl font-bold text-foreground");
  });

  it("groups the lifecycle actions into one button row", async () => {
    await renderLoaded();

    const archive = byTestId("organization-detail-archive");
    const reactivate = byTestId("organization-detail-reactivate");

    const actions = parentOf(archive);
    expectClasses(actions, "flex gap-3");
    expect(actions.contains(reactivate)).toBe(true);

    // The shared Button is `w-full` by default; in a row the two actions size to
    // their labels instead of stretching.
    expectClasses(archive, "w-auto");
    expectClasses(reactivate, "w-auto");
  });

  it("titles the bound-clients section", async () => {
    await renderLoaded();

    const heading = byTestId("organization-detail-clients-heading");
    expectTag(heading, "h2");
    expect(heading.textContent).toBe("Bound Clients");
    expectClasses(heading, "text-xl font-semibold text-foreground mb-4");
  });

  it("wraps the bound-clients list in the card surface", async () => {
    await renderLoaded();

    const table = byTestId("organization-detail-clients-table");
    expectTag(table, "ul");
    expectClasses(table, "divide-y divide-border");

    const surface = parentOf(table);
    expectTag(surface, "div");
    expectClasses(surface, "bg-card rounded-lg shadow-sm border border-border overflow-hidden");
  });

  it("styles every bound-client row as a table row", async () => {
    await renderLoaded();

    const rows = allByTestId("organization-detail-client-row");
    expect(rows).toHaveLength(CLIENTS.length);
    for (const row of rows) {
      expectTag(row, "li");
      expectClasses(row, "flex items-center justify-between px-6 py-4 hover:bg-background/50");
    }
  });

  it("presents the register-client form on its own padded card", async () => {
    await renderLoaded();

    const form = byTestId("organization-detail-register-form");
    expectTag(form, "form");
    expectClasses(form, "space-y-6");

    const surface = parentOf(form);
    expectClasses(surface, "bg-card rounded-lg shadow-sm border border-border p-8");
  });

  it("styles the register submit as the gold pill action", async () => {
    await renderLoaded();

    const submit = byTestId("organization-detail-register-submit");
    expectClasses(submit, "rounded-full");
    // Regression guard: same button, same words.
    expect(submit.textContent?.trim()).toBe("Register client");
  });

  it("presents the not-found state as a centered card that keeps its sentence", async () => {
    const notFound = await renderNotFound();

    expectClasses(notFound, "bg-card rounded-lg shadow-sm border border-border p-12 text-center");

    const heading = within(notFound, "h2");
    expect(heading.textContent).toBe("Organization not found.");
    expectClasses(heading, "text-xl font-semibold text-foreground mb-2");
  });

  it("styles the loaded page with theme tokens only", async () => {
    await renderLoaded();

    expectTokenColorsOnly(parentOf(byTestId("organization-detail-back-link")));
  });

  it("styles the not-found state with theme tokens only", async () => {
    const notFound = await renderNotFound();

    expectTokenColorsOnly(parentOf(notFound));
  });
});
