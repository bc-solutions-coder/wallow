import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createPassthroughHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { SetupForm } from "./SetupForm";

/**
 * Setup screen: validation, the confirm-password check, the strength meter,
 * submission and its RFC 7807 error split, and the success card.
 *
 * Runs the real SDK over a faked fetch (sdk-harness); assertions read the
 * recorded request. The screen makes no requests on mount — the one call is
 * the submit's POST — so the whole harness is the submit leg.
 */

const ADMIN_ENDPOINT = "/v1/identity/setup/admin";

const BAD_REQUEST = 400;
const CONFLICT = 409;

const VALID = {
  email: "admin@example.com",
  password: "N3w-Passw0rd!",
  firstName: "Ada",
  lastName: "Lovelace",
  organizationName: "Acme Inc.",
};

let harness: SdkHarness;

beforeEach(() => {
  harness = createPassthroughHarness();
});

function renderForm(seededOrganizationName?: string) {
  return renderWithWallow(<SetupForm seededOrganizationName={seededOrganizationName} />, {
    harness,
  });
}

/** Every recorded POST to the setup-admin endpoint, in order. */
function adminCalls() {
  return harness.calls.filter((call) => call.path === ADMIN_ENDPOINT);
}

/** Fill the whole form with valid values; `confirmPassword` defaults to matching. */
async function fillForm(confirmPassword: string = VALID.password): Promise<void> {
  await userEvent.fill(page.getByTestId("setup-email"), VALID.email);
  await userEvent.fill(page.getByTestId("setup-password"), VALID.password);
  await userEvent.fill(page.getByTestId("setup-confirm-password"), confirmPassword);
  await userEvent.fill(page.getByTestId("setup-first-name"), VALID.firstName);
  await userEvent.fill(page.getByTestId("setup-last-name"), VALID.lastName);
  await userEvent.fill(page.getByTestId("setup-organization-name"), VALID.organizationName);
}

describe("SetupForm", () => {
  it("renders every field of the single-form contract", async () => {
    renderForm();

    await expect.element(page.getByTestId("setup-heading")).toBeInTheDocument();
    for (const field of [
      "setup-email",
      "setup-password",
      "setup-confirm-password",
      "setup-first-name",
      "setup-last-name",
      "setup-organization-name",
      "setup-submit",
    ]) {
      await expect.element(page.getByTestId(field)).toBeInTheDocument();
    }
    // No request on mount: the route's beforeLoad owns the status question.
    expect(harness.calls).toHaveLength(0);
  });

  it("blocks an empty submit with field errors and no request", async () => {
    renderForm();

    await userEvent.click(page.getByTestId("setup-submit"));

    await expect.element(page.getByTestId("setup-email-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("setup-first-name-error")).toBeInTheDocument();
    expect(adminCalls()).toHaveLength(0);
  });

  it("rejects a mismatched confirm password without submitting", async () => {
    renderForm();

    await fillForm("Different-Passw0rd!");
    await userEvent.click(page.getByTestId("setup-submit"));

    await expect
      .element(page.getByTestId("setup-confirm-password-error"))
      .toHaveTextContent("Passwords do not match.");
    expect(adminCalls()).toHaveLength(0);
  });

  it("rates the password live: no meter when empty, Weak to Strong as it grows", async () => {
    renderForm();

    expect(page.getByTestId("setup-password-strength").query()).toBeNull();

    await userEvent.fill(page.getByTestId("setup-password"), "short1");
    await expect.element(page.getByTestId("setup-password-strength")).toHaveTextContent("Weak");

    await userEvent.fill(page.getByTestId("setup-password"), VALID.password);
    await expect.element(page.getByTestId("setup-password-strength")).toHaveTextContent("Strong");
  });

  it("posts exactly the API's five fields — confirmPassword never leaves the form", async () => {
    renderForm();

    await fillForm();
    await userEvent.click(page.getByTestId("setup-submit"));

    await vi.waitFor(() => {
      expect(adminCalls()).toHaveLength(1);
    });
    const call = adminCalls()[0];
    expect(call?.method).toBe("POST");
    expect(call?.body).toEqual({
      email: VALID.email,
      password: VALID.password,
      firstName: VALID.firstName,
      lastName: VALID.lastName,
      organizationName: VALID.organizationName,
    });
  });

  it("shows the success card with a full-document sign-in link after a 2xx", async () => {
    renderForm();

    await fillForm();
    await userEvent.click(page.getByTestId("setup-submit"));

    await expect.element(page.getByTestId("setup-success-heading")).toBeInTheDocument();
    // A plain `<a>`, not a router link: the login page's own gate must re-read
    // setup status on a fresh request, not this render's cached answer.
    await expect
      .element(page.getByRole("link", { name: "Sign in" }))
      .toHaveAttribute("href", "/login");
  });

  it("splits an RFC 7807 validation failure onto the matching field", async () => {
    harness.rejectJson(
      {
        status: BAD_REQUEST,
        title: "Validation failed",
        detail: "One or more validation errors occurred.",
        errors: { Email: ["Email is already in use."] },
      },
      BAD_REQUEST,
    );
    renderForm();

    await fillForm();
    await userEvent.click(page.getByTestId("setup-submit"));

    await expect
      .element(page.getByTestId("setup-email-error"))
      .toHaveTextContent("Email is already in use.");
  });

  it("surfaces a 409 (setup raced to completion elsewhere) in the banner", async () => {
    harness.rejectJson(
      {
        status: CONFLICT,
        title: "Conflict",
        detail: "Setup has already been completed.",
      },
      CONFLICT,
    );
    renderForm();

    await fillForm();
    await userEvent.click(page.getByTestId("setup-submit"));

    await expect
      .element(page.getByTestId("setup-error"))
      .toHaveTextContent("Setup has already been completed.");
    // Still the form, not the success card — the visitor can read the banner.
    await expect.element(page.getByTestId("setup-heading")).toBeInTheDocument();
  });
});

describe("SetupForm with a seeded organization", () => {
  const SEEDED = "Wallow";

  it("states the seeded organization read-only and submits it unchanged", async () => {
    renderForm(SEEDED);

    const organization = page.getByTestId("setup-organization-name");
    await expect.element(organization).toHaveValue(SEEDED);
    await expect.element(organization).toHaveAttribute("readonly");
    await expect.element(page.getByTestId("setup-organization-seeded")).toBeInTheDocument();

    await userEvent.fill(page.getByTestId("setup-email"), VALID.email);
    await userEvent.fill(page.getByTestId("setup-password"), VALID.password);
    await userEvent.fill(page.getByTestId("setup-confirm-password"), VALID.password);
    await userEvent.fill(page.getByTestId("setup-first-name"), VALID.firstName);
    await userEvent.fill(page.getByTestId("setup-last-name"), VALID.lastName);
    await userEvent.click(page.getByTestId("setup-submit"));

    await expect.element(page.getByTestId("setup-success-heading")).toBeInTheDocument();
    expect(adminCalls()).toHaveLength(1);
    expect(adminCalls()[0]?.body).toMatchObject({ organizationName: SEEDED });
  });

  it("asks for the organization when nothing was seeded", async () => {
    renderForm();

    const organization = page.getByTestId("setup-organization-name");
    await expect.element(organization).toHaveValue("");
    await expect.element(organization).not.toHaveAttribute("readonly");
    expect(page.getByTestId("setup-organization-seeded").query()).toBeNull();
  });
});
