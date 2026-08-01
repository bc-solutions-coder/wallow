import {
  createSdkHarness,
  failsWith,
  neverSettles,
  routeHarness,
  type SdkCall,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { MemberList } from "./MemberList";

/**
 * The add-member form as built on `@bc-solutions-coder/forms`: the `AppForm`
 * shell, validation messages, in-flight disabling, and the failure surface.
 *
 * The catalog derives a field's testid from its NAME, so `userId` would render
 * `organization-member-add-user-id`; an explicit `testId` holds it on
 * `organization-member-userid`, and dropping it fails silently.
 */

/** The members endpoint this section reads and writes. */
const MEMBERS_PATH = "/v1/identity/organizations/o1/members";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/** Answer the roster read, and the add POST with `addResponse`. */
function seedRoster(addResponse: unknown = {}): void {
  routeHarness(
    harness,
    {
      [`GET ${MEMBERS_PATH}`]: [],
      [`POST ${MEMBERS_PATH}`]: addResponse,
    },
    { fallback: [] },
  );
}

/** Wait for the form to paint — the roster read is what puts the section on screen. */
async function awaitLoaded(): Promise<void> {
  await expect.element(page.getByTestId("organization-member-userid")).toBeInTheDocument();
}

/**
 * Only the add POSTs. `harness.calls` also holds the roster read and every
 * post-success refetch, so "the endpoint was not reached" has to be said about
 * this operation rather than about the transport as a whole.
 */
function addCalls(): readonly SdkCall[] {
  return harness.calls.filter(
    (call: SdkCall) => call.method === "POST" && call.path.endsWith(MEMBERS_PATH),
  );
}

function formElement(): HTMLFormElement {
  return page.getByTestId("organization-member-add-form").element() as HTMLFormElement;
}

function userIdInput(): HTMLInputElement {
  return page.getByTestId("organization-member-userid").element() as HTMLInputElement;
}

function submitButton(): HTMLButtonElement {
  return page.getByTestId("organization-member-add-submit").element() as HTMLButtonElement;
}

/** The text of whatever names `control` — the `aria-labelledby` chain, else `label[for]`. */
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

/** The ids `control` points its `aria-describedby` at. */
function describedByIds(control: HTMLElement): readonly string[] {
  return (control.getAttribute("aria-describedby") ?? "")
    .split(" ")
    .filter((id: string) => id !== "");
}

describe("MemberList add-member form on @bc-solutions-coder/forms", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("renders through the forms shell, which leaves validation to the schema", async () => {
    seedRoster();

    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    const element: HTMLFormElement = formElement();
    expect(element.tagName).toBe("FORM");
    expect(element.noValidate).toBe(true);
  });

  it("keeps the control and the submit under that one shell, and labels the control", async () => {
    seedRoster();

    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    const element: HTMLFormElement = formElement();
    expect(userIdInput().closest("form")).toBe(element);
    expect(submitButton().closest("form")).toBe(element);
    expect(accessibleName(userIdInput())).toBe("User ID");
  });

  it("says why an empty submit did nothing instead of swallowing it", async () => {
    seedRoster();

    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    const message = page.getByTestId("organization-member-userid-error");
    await expect.element(message).toBeInTheDocument();
    await expect.element(message).toHaveTextContent("User ID is required");

    const messageId: string = message.element().id;
    expect(messageId).not.toBe("");
    expect(describedByIds(userIdInput())).toContain(messageId);
    expect(userIdInput().getAttribute("aria-invalid")).toBe("true");
    expect(addCalls()).toHaveLength(0);
  });

  it("rejects a whitespace-only user id without reaching the endpoint", async () => {
    // `.trim()` in the schema is what makes `"   "` fail the `min(1)`; a bare
    // `min(1)` would let three spaces through.
    seedRoster();

    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("organization-member-userid"), "   ");
    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await expect
      .element(page.getByTestId("organization-member-userid-error"))
      .toHaveTextContent("User ID is required");
    expect(addCalls()).toHaveLength(0);
  });

  it("disables the control and the submit while the add is in flight", async () => {
    // Scoped to the POST so the roster read still settles and the form paints.
    seedRoster(neverSettles());

    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("organization-member-userid"), "u9");
    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await vi.waitFor(() => {
      expect(addCalls()).toHaveLength(1);
    });
    await expect.poll(() => userIdInput().disabled).toBe(true);
    expect(submitButton().disabled).toBe(true);
  });

  it("surfaces a failed add instead of failing silently", async () => {
    seedRoster(
      failsWith(
        {
          type: "https://httpstatuses.io/403",
          title: "Forbidden",
          status: 403,
          detail: "You cannot add members to this organization.",
        },
        403,
      ),
    );

    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("organization-member-userid"), "u9");
    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await expect
      .element(page.getByTestId("organization-member-add-error"))
      .toHaveTextContent("You cannot add members to this organization.");
  });

  it("shows a validation failure's per-property message on the field, not the banner", async () => {
    seedRoster(
      failsWith(
        {
          type: "https://httpstatuses.io/400",
          title: "One or more validation errors occurred.",
          status: 400,
          errors: { UserId: ["No user with that id exists."] },
        },
        400,
      ),
    );

    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("organization-member-userid"), "u9");
    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await expect
      .element(page.getByTestId("organization-member-userid-error"))
      .toHaveTextContent("No user with that id exists.");
    // Everything landed on a field, so the banner would only repeat it.
    expect(page.getByTestId("organization-member-add-error").elements()).toHaveLength(0);
  });

  it("clears a server field error on the next submit rather than wedging the form", async () => {
    seedRoster(
      failsWith(
        {
          type: "https://httpstatuses.io/400",
          title: "One or more validation errors occurred.",
          status: 400,
          errors: { UserId: ["No user with that id exists."] },
        },
        400,
      ),
    );

    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("organization-member-userid"), "u9");
    await userEvent.click(page.getByTestId("organization-member-add-submit"));
    await expect.element(page.getByTestId("organization-member-userid-error")).toBeInTheDocument();

    seedRoster();
    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await vi.waitFor(() => {
      expect(addCalls()).toHaveLength(2);
    });
  });

  it("still posts the user id and still clears the input after a successful add", async () => {
    seedRoster();

    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("organization-member-userid"), "u9");
    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await vi.waitFor(() => {
      expect(addCalls()).toHaveLength(1);
    });
    expect(addCalls()[0]?.body).toEqual({ userId: "u9", role: "user" });
    await expect.poll(() => userIdInput().value).toBe("");
  });

  it("keeps the user-id control on organization-member-userid, not the derived id", async () => {
    // The trap is silent: a form that dropped the `testId` override still
    // renders and still submits, and simply stops being findable.
    seedRoster();

    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    expect(page.getByTestId("organization-member-add-user-id").elements()).toHaveLength(0);

    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await expect.element(page.getByTestId("organization-member-userid-error")).toBeInTheDocument();
    expect(page.getByTestId("organization-member-add-user-id-error").elements()).toHaveLength(0);
  });
});
