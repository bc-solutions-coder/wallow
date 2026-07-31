import {
  createSdkHarness,
  type SdkCall,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { RegisterAppForm } from "./RegisterAppForm";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * The optional branding subsection's post-register upsert.
 *
 * Branding cannot ride the register request body: it is a different endpoint,
 * multipart, keyed on a clientId that first exists in the register RESPONSE. The
 * endpoint 400s on a blank `DisplayName`, so an untouched or whitespace-only
 * section must fire nothing. Assertions read the RECORDED OUTGOING REQUESTS, so
 * "the branding reached the wire" is a claim about the wire, not about a spy.
 */

/** The register response the harness answers a successful submit with. */
const OK_RESPONSE = {
  clientId: "client-abc",
  clientSecret: "secret-xyz",
  registrationAccessToken: "rat-123",
};

const REGISTER_PATH = "/api/v1/identity/apps/register";
/** Built from `OK_RESPONSE.clientId` — the id the REGISTER call returned. */
const BRANDING_PATH = "/api/v1/identity/apps/client-abc/branding";

const OK_STATUS = 200;
const BAD_REQUEST_STATUS = 400;

/** What the API answers a rejected branding upsert with. */
const BRANDING_PROBLEM = {
  type: "https://httpstatuses.io/400",
  title: "Bad Request",
  status: BAD_REQUEST_STATUS,
  detail: "The logo must be a PNG or JPEG image.",
};

/** Every recorded request to `path`, in order. */
function callsTo(path: string): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path === path);
}

/** Every recorded request to ANY branding endpoint, whatever the clientId. */
function brandingCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path.endsWith("/branding"));
}

/** Register succeeds; branding answers `brandingStatus` with `brandingBody`. */
function programHarness(brandingStatus: number, brandingBody: unknown): void {
  harness.respond((call: SdkCall) =>
    call.path === REGISTER_PATH
      ? Response.json(OK_RESPONSE, { status: OK_STATUS })
      : Response.json(brandingBody, { status: brandingStatus }),
  );
}

/** Fill the register form's own required field, so only branding is under test. */
async function fillRegistrationBasics(): Promise<void> {
  await userEvent.type(page.getByTestId("app-display-name"), "My App");
}

describe("RegisterAppForm branding/logo upsert", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders an optional branding display-name input in the form view", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });
    await expect.element(page.getByTestId("app-branding-display-name")).toBeInTheDocument();
  });

  it("renders an optional branding tagline input", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });
    await expect.element(page.getByTestId("app-branding-tagline")).toBeInTheDocument();
  });

  it("renders a logo file input for the branding upsert", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });
    await expect.element(page.getByTestId("app-logo-input")).toBeInTheDocument();
  });

  it("posts the typed branding to the branding endpoint for the returned clientId", async () => {
    programHarness(OK_STATUS, {});
    renderWithWallow(<RegisterAppForm />, { harness });

    await fillRegistrationBasics();
    await userEvent.type(page.getByTestId("app-branding-display-name"), "Wallow Console");
    await userEvent.type(page.getByTestId("app-branding-tagline"), "Ship faster");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await vi.waitFor(() => {
      expect(brandingCalls()).toHaveLength(1);
    });

    const branding: SdkCall | undefined = brandingCalls()[0];
    expect(branding?.method).toBe("POST");
    // `client-abc` is nowhere in the form — it can only have come from the
    // register RESPONSE.
    expect(branding?.path).toBe(BRANDING_PATH);
    expect(branding?.body).toEqual({
      DisplayName: "Wallow Console",
      Tagline: "Ship faster",
    });
    expect(callsTo(REGISTER_PATH)).toHaveLength(1);
  });

  it("carries a chosen logo file in that same branding request", async () => {
    programHarness(OK_STATUS, {});
    renderWithWallow(<RegisterAppForm />, { harness });

    await fillRegistrationBasics();
    await userEvent.type(page.getByTestId("app-branding-display-name"), "Wallow Console");
    await userEvent.upload(
      page.getByTestId("app-logo-input"),
      new File(["png-bytes"], "logo.png", { type: "image/png" }),
    );
    await userEvent.click(page.getByTestId("app-register-submit"));

    await vi.waitFor(() => {
      expect(brandingCalls()).toHaveLength(1);
    });

    const body = brandingCalls()[0]?.body as { DisplayName?: string; logo?: unknown };
    expect(body.DisplayName).toBe("Wallow Console");
    expect(body.logo).toBeInstanceOf(File);
    expect((body.logo as File).name).toBe("logo.png");
  });

  it("fires no branding request at all when the section is untouched", async () => {
    programHarness(OK_STATUS, {});
    renderWithWallow(<RegisterAppForm />, { harness });

    await fillRegistrationBasics();
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect.element(page.getByTestId("app-register-success")).toBeInTheDocument();
    expect(brandingCalls()).toHaveLength(0);
    expect(harness.calls).toHaveLength(1);
    expect(harness.calls[0]?.path).toBe(REGISTER_PATH);
  });

  it("fires no branding request when the section holds only whitespace", async () => {
    // These fields have been TYPED IN, so an implementation gating on "dirty"
    // rather than on the trimmed values passes the untouched case above and
    // fails here — posting the blank DisplayName the API rejects.
    programHarness(OK_STATUS, {});
    renderWithWallow(<RegisterAppForm />, { harness });

    await fillRegistrationBasics();
    await userEvent.type(page.getByTestId("app-branding-display-name"), "   ");
    await userEvent.type(page.getByTestId("app-branding-tagline"), "  ");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect.element(page.getByTestId("app-register-success")).toBeInTheDocument();
    expect(brandingCalls()).toHaveLength(0);
    expect(harness.calls).toHaveLength(1);
  });

  it("still reveals the one-time secret when the branding upsert fails", async () => {
    // The client secret is minted once and is never re-fetchable, so a failed
    // SECOND call must not cost the user the result of the first.
    programHarness(BAD_REQUEST_STATUS, BRANDING_PROBLEM);
    renderWithWallow(<RegisterAppForm />, { harness });

    await fillRegistrationBasics();
    await userEvent.type(page.getByTestId("app-branding-display-name"), "Wallow Console");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect.element(page.getByTestId("app-client-secret")).toHaveTextContent("secret-xyz");
    await expect.element(page.getByTestId("app-client-id")).toHaveTextContent("client-abc");
    await expect
      .element(page.getByTestId("app-branding-error"))
      .toHaveTextContent("The logo must be a PNG or JPEG image.");
  });

  it("retries only the branding upsert, never the registration", async () => {
    // A retry that re-ran the registration would mint a SECOND client secret and
    // discard the one already on screen, so calls are counted per path.
    programHarness(BAD_REQUEST_STATUS, BRANDING_PROBLEM);
    renderWithWallow(<RegisterAppForm />, { harness });

    await fillRegistrationBasics();
    await userEvent.type(page.getByTestId("app-branding-display-name"), "Wallow Console");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect.element(page.getByTestId("app-branding-error")).toBeInTheDocument();
    expect(callsTo(REGISTER_PATH)).toHaveLength(1);
    expect(brandingCalls()).toHaveLength(1);

    await userEvent.click(page.getByTestId("app-branding-retry"));

    await vi.waitFor(() => {
      expect(brandingCalls()).toHaveLength(2);
    });
    expect(brandingCalls()[1]?.path).toBe(BRANDING_PATH);
    expect(callsTo(REGISTER_PATH)).toHaveLength(1);
  });
});
