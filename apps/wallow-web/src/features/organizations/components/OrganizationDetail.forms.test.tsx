import {
  createSdkHarness,
  type SdkCall,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { failsWith, neverSettles, routeHarness } from "@shared/testing/harness-routes";
import { OrganizationDetail } from "./OrganizationDetail";

/**
 * The org-detail REGISTER-CLIENT form ON `@bc-solutions-coder/forms`
 * (Wallow-lrlm.5.5).
 *
 * WHY A NEW FILE. The four specs already beside this one are the screen's frozen
 * oracles and the migration's acceptance criterion is that all four pass
 * UNCHANGED, so none of them is edited here: `OrganizationDetail.test.tsx` pins
 * the org heading + lifecycle actions, `OrganizationDetail.clients.test.tsx`
 * pins that the bound-clients table and the four register controls are REACHABLE
 * from the detail page, `OrganizationDetail.catalog.test.tsx` pins the
 * client-type `Select`, and `OrganizationDetail.restyle.test.tsx` pins the card
 * surface and the form's own rhythm. What none of them can say, because all four
 * predate the package, is anything about the shell the form is built ON — nor
 * about the register POST itself, which today has NO oracle at all. This file
 * says both.
 *
 * WHAT THE MIGRATION ADDS (these fail against the hand-rolled form):
 *
 *   1. The `<form>` is the package's `AppForm`, so it is `noValidate` — the zod
 *      schema owns validation and the browser must not double-validate and pop a
 *      native bubble over the field message. The hand-rolled form has no such
 *      attribute.
 *   2. There is a required rule on the display name at all. Today the form has
 *      NO validation whatsoever: submitting an empty form posts `{ name: "" }`
 *      and lets the API reject it, so the user pays a round trip to learn what
 *      the form already knew.
 *   3. That message is genuinely ASSOCIATED with the input (`aria-invalid` +
 *      `aria-describedby`), which is only reachable through the catalog field.
 *   4. The controls and the submit disable themselves while the registration is
 *      in flight. Today nothing disables — and this is a form where a double
 *      submit costs real money: each success mints a client secret that is shown
 *      exactly once, so the second registration's secret is simply lost.
 *   5. A validation failure's per-property messages land NEXT TO the control
 *      they belong to. Today `ClientsSection` renders a HARDCODED "Failed to
 *      register client." for every failure, so the API's own sentence — which
 *      says what was actually wrong — never reaches the screen.
 *   6. A property the form has no field for still reaches the banner rather than
 *      being dropped. The form's field is `displayName`; the wire name is
 *      `name`, so `Name` is exactly that case.
 *   7. A server field error must not WEDGE the form: the shell clears it on the
 *      way into the next submit, or every later submit would fail the validity
 *      gate silently and never reach the endpoint again.
 *
 * WHAT THE MIGRATION MUST NOT DROP (these pass today — regression guards aimed
 * squarely at the parts that move house). The register POST and the one-time
 * reveal are BOTH restated here rather than left to an oracle, because there is
 * no oracle: `OrganizationDetail.clients.test.tsx` asserts the controls exist and
 * stops there.
 *
 *   8. The submitted body still remaps `displayName` to `name`, still splits the
 *      redirect URIs on newlines with blanks dropped, still sends an empty
 *      `postLogoutRedirectUris`, and still carries the org as `tenantId`.
 *   9. `clientType` still never reaches the wire. The endpoint infers public vs
 *      confidential from the secret it issues, so the field is the form's own
 *      switch — and `useAppForm`'s DEFAULT `toVariables` would post the whole
 *      values object, quietly inventing a wire field.
 *  10. The one-time client-id/secret reveal survives. The gate moves off
 *      `register.isSuccess` (the raw mutation is owned by `useAppForm` after the
 *      migration) onto state captured in the hook's `onSuccess`.
 *
 * Same seam as the oracles: the REAL SDK with only its `fetch` faked
 * (`@bc-solutions-coder/testing/sdk-harness`), real router context via
 * `renderWithWallow`, real headless Chromium. The screen fires three reads at
 * once, so `routeHarness` answers each by URL rather than in call order.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

/** The register response the harness answers a successful submit with. */
const OK_RESPONSE = {
  id: "c1",
  clientId: "client-abc",
  clientSecret: "secret-xyz",
  name: "Dashboard",
};

/** The POST path the register-client flow issues. */
const REGISTER_ROUTE = "POST /v1/identity/clients";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Answer the org / members / clients reads with a loaded org, and the register
 * POST with `registerResponse`.
 *
 * The screen's controls do not exist at first paint — the reads go over the wire
 * rather than out of a pre-seeded cache — so every case here settles the detail
 * query before driving anything.
 */
function seedLoadedOrg(registerResponse: unknown = OK_RESPONSE): void {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": [],
      "GET /v1/identity/clients/by-tenant/o1": [],
      [REGISTER_ROUTE]: registerResponse,
    },
    { fallback: [] },
  );
}

/** Wait for the org read to paint before driving a control. */
async function awaitLoaded(): Promise<void> {
  await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
  await expect
    .element(page.getByTestId("organization-detail-register-display-name"))
    .toBeInTheDocument();
}

/**
 * Only the register POSTs. `harness.calls` also holds the screen's three reads
 * and their refetches, so "the endpoint was not reached" has to be said about
 * this operation rather than about the transport as a whole.
 */
function registerCalls(): readonly SdkCall[] {
  return harness.calls.filter(
    (call: SdkCall) => call.method === "POST" && call.path.endsWith("/v1/identity/clients"),
  );
}

function formElement(): HTMLFormElement {
  return page.getByTestId("organization-detail-register-form").element() as HTMLFormElement;
}

function displayNameInput(): HTMLInputElement {
  return page
    .getByTestId("organization-detail-register-display-name")
    .element() as HTMLInputElement;
}

function redirectUrisTextarea(): HTMLTextAreaElement {
  return page
    .getByTestId("organization-detail-register-redirect-uris")
    .element() as HTMLTextAreaElement;
}

function submitButton(): HTMLButtonElement {
  return page.getByTestId("organization-detail-register-submit").element() as HTMLButtonElement;
}

/**
 * The text of whatever names `control` — the `aria-labelledby` chain when there
 * is one (which is how Base UI names a `Select` trigger, since a `<label for>`
 * cannot name a button), else the `<label for>` pointing at it.
 */
function accessibleName(control: HTMLElement): string {
  const labelledBy: string | null = control.getAttribute("aria-labelledby");

  if (labelledBy !== null && labelledBy !== "") {
    return labelledBy
      .split(" ")
      .map((id: string) => document.querySelector(`#${CSS.escape(id)}`)?.textContent?.trim() ?? "")
      .filter((text: string) => text !== "")
      .join(" ");
  }

  const label: Element | null =
    control.id === "" ? null : document.querySelector(`label[for="${CSS.escape(control.id)}"]`);

  return label?.textContent?.trim() ?? "";
}

/**
 * The ids `control` points its `aria-describedby` at. Split rather than compared
 * whole: Base UI appends the message to whatever else already describes the
 * control, so the claim is that it is AMONG them, not that it is alone.
 */
function describedByIds(control: HTMLElement): readonly string[] {
  return (control.getAttribute("aria-describedby") ?? "")
    .split(" ")
    .filter((id: string) => id !== "");
}

describe("OrganizationDetail register-client form on @bc-solutions-coder/forms", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("renders through the forms shell, which leaves validation to the schema", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    const element: HTMLFormElement = formElement();
    expect(element.tagName).toBe("FORM");
    // The shell's `noValidate`: the zod schema is the only validator, so the
    // browser must not also refuse the submit with a native bubble.
    expect(element.noValidate).toBe(true);
  });

  it("keeps every control under that one shell", async () => {
    // The client-type select is the one at risk: it is the only control the form
    // reaches through a nested component today, and a catalog field left outside
    // the shell would silently stop being part of the submitted values.
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    const element: HTMLFormElement = formElement();
    for (const testId of [
      "organization-detail-register-display-name",
      "organization-detail-register-client-type",
      "organization-detail-register-redirect-uris",
      "organization-detail-register-submit",
    ]) {
      const control: HTMLElement = page.getByTestId(testId).element() as HTMLElement;
      expect(control.closest("form"), `${testId} must live under the form shell`).toBe(element);
    }
  });

  it("labels the display-name input, the client-type select and the redirect-URI textarea", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    const clientType: HTMLElement = page
      .getByTestId("organization-detail-register-client-type")
      .element() as HTMLElement;
    expect(accessibleName(displayNameInput())).toBe("Display name");
    expect(accessibleName(clientType)).toBe("Client type");
    expect(accessibleName(redirectUrisTextarea())).toBe("Redirect URIs");
  });

  it("keeps the redirect-URI control a real textarea", async () => {
    // The newline-separated list only works if the control still accepts a
    // newline — a catalog field that resolved to an `<input>` would quietly turn
    // the multi-URI contract into a single-line one.
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    const redirectUris: HTMLTextAreaElement = redirectUrisTextarea();
    expect(redirectUris.tagName).toBe("TEXTAREA");
    // `tagName` alone is satisfied by a hand-rolled `<textarea>` too, and no spec
    // in this tree pins this control's classes at all — so these two utilities,
    // which ONLY `textareaRecipe` adds, are what says "the CATALOG control".
    for (const utility of ["min-h-20", "resize-y"]) {
      expect(redirectUris.classList.contains(utility), utility).toBe(true);
    }
  });

  it("associates a required-display-name message with the input instead of posting an empty name", async () => {
    // Today there is no validation at all: an empty submit posts `{ name: "" }`
    // and waits for the API to say no.
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    const message = page.getByTestId("organization-detail-register-display-name-error");
    await expect.element(message).toBeInTheDocument();
    await expect.element(message).toHaveTextContent("Display name is required");

    const messageId: string = message.element().id;
    expect(messageId).not.toBe("");
    expect(describedByIds(displayNameInput())).toContain(messageId);
    expect(displayNameInput().getAttribute("aria-invalid")).toBe("true");
    expect(registerCalls()).toHaveLength(0);
  });

  it("rejects a whitespace-only display name without reaching the endpoint", async () => {
    // `.trim()` in the schema is what makes `"   "` fail the `min(1)`; a bare
    // `min(1)` would let three spaces through and register a nameless client.
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("organization-detail-register-display-name"), "   ");
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-display-name-error"))
      .toHaveTextContent("Display name is required");
    expect(registerCalls()).toHaveLength(0);
  });

  it("disables the controls and the submit while the registration is in flight", async () => {
    // Scoped to the POST: the screen's three reads must still settle, or the
    // form would never paint to be driven in the first place.
    seedLoadedOrg(neverSettles());

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    // Wait for the request to REACH the transport first: the harness records a
    // call before its responder runs, so this is the earliest point at which
    // "in flight" is a fact rather than a race.
    await vi.waitFor(() => {
      expect(registerCalls()).toHaveLength(1);
    });
    await expect.poll(() => displayNameInput().disabled).toBe(true);
    expect(redirectUrisTextarea().disabled).toBe(true);
    // A second registration would mint a second client secret, and only one of
    // the two would ever be shown.
    expect(submitButton().disabled).toBe(true);
  });

  it("shows a validation failure's per-property message on the field, not the banner", async () => {
    seedLoadedOrg(
      failsWith(
        {
          type: "https://httpstatuses.io/400",
          title: "One or more validation errors occurred.",
          status: 400,
          errors: { RedirectUris: ["'nope' is not an absolute URI."] },
        },
        400,
      ),
    );

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.type(page.getByTestId("organization-detail-register-redirect-uris"), "nope");
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-redirect-uris-error"))
      .toHaveTextContent("'nope' is not an absolute URI.");
    // Everything landed on a field, so the banner would only repeat it.
    expect(page.getByTestId("organization-detail-register-error").elements()).toHaveLength(0);
  });

  it("keeps a message for a property the form has no field for in the banner", async () => {
    // The wire name is `name`; the form's field is `displayName`. The remap
    // means this message has nowhere to land — and today it is dropped on the
    // floor in favour of a hardcoded "Failed to register client.", leaving the
    // user staring at a generic sentence while the API had said exactly what was
    // wrong.
    seedLoadedOrg(
      failsWith(
        {
          type: "https://httpstatuses.io/400",
          title: "One or more validation errors occurred.",
          status: 400,
          errors: { Name: ["A client with that name is already registered."] },
        },
        400,
      ),
    );

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-error"))
      .toHaveTextContent("A client with that name is already registered.");
  });

  it("surfaces a form-level failure's own sentence rather than a hardcoded one", async () => {
    seedLoadedOrg(
      failsWith(
        {
          type: "https://httpstatuses.io/403",
          title: "Forbidden",
          status: 403,
          detail: "This organization has reached its client limit.",
        },
        403,
      ),
    );

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-error"))
      .toHaveTextContent("This organization has reached its client limit.");
  });

  it("clears a server field error on the next submit rather than wedging the form", async () => {
    // Nothing in the form framework clears an `onServer` error by itself, so the
    // very next submit would otherwise fail the validity gate silently and never
    // reach the endpoint again — with no message to explain why.
    seedLoadedOrg(
      failsWith(
        {
          type: "https://httpstatuses.io/400",
          title: "One or more validation errors occurred.",
          status: 400,
          errors: { RedirectUris: ["'nope' is not an absolute URI."] },
        },
        400,
      ),
    );

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.type(page.getByTestId("organization-detail-register-redirect-uris"), "nope");
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));
    await expect
      .element(page.getByTestId("organization-detail-register-redirect-uris-error"))
      .toBeInTheDocument();

    seedLoadedOrg();
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await vi.waitFor(() => {
      expect(registerCalls()).toHaveLength(2);
    });
  });

  it("still posts the remapped body, and still keeps clientType off the wire", async () => {
    // REGRESSION GUARD, and the only statement of the register contract anywhere:
    // `displayName` becomes `name`, the textarea becomes a trimmed newline-split
    // array with blanks dropped, `postLogoutRedirectUris` is sent empty, and the
    // org rides along as `tenantId`. `useAppForm`'s DEFAULT `toVariables` would
    // post the values object whole and invent a `clientType` wire field the
    // endpoint does not have.
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.fill(
      redirectUrisTextarea(),
      "https://a.example/cb\n\n  https://b.example/cb  \n",
    );
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await vi.waitFor(() => {
      expect(registerCalls()).toHaveLength(1);
    });
    expect(registerCalls()[0]?.body).toEqual({
      name: "Dashboard",
      redirectUris: ["https://a.example/cb", "https://b.example/cb"],
      postLogoutRedirectUris: [],
      tenantId: "o1",
    });
  });

  it("still reveals the one-time client id and secret after a successful registration", async () => {
    // REGRESSION GUARD. The gate moves off `register.isSuccess` — the raw
    // mutation belongs to `useAppForm` after the migration — onto state captured
    // in the hook's `onSuccess`. A gate that never fired would lose a secret that
    // is issued exactly once and can never be re-fetched.
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-success"))
      .toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-client-id"))
      .toHaveTextContent("client-abc");
    await expect
      .element(page.getByTestId("organization-detail-register-client-secret"))
      .toHaveTextContent("secret-xyz");
  });
});
