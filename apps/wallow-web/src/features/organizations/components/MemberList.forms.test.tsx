import {
  createSdkHarness,
  type SdkCall,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { failsWith, neverSettles, routeHarness } from "@shared/testing/harness-routes";
import { MemberList } from "./MemberList";

/**
 * The ADD-MEMBER form ON `@bc-solutions-coder/forms` (Wallow-lrlm.5.5).
 *
 * WHY A NEW FILE. The three specs already beside this one are the section's
 * frozen oracles and the migration's acceptance criterion is that all three pass
 * UNCHANGED, so none of them is edited here: `MemberList.test.tsx` pins the
 * roster, the add/remove payloads and the members-operation sweep,
 * `MemberList.error-state.test.tsx` pins the READ's failure surface, and
 * `MemberList.restyle.test.tsx` pins the form row's `flex items-end gap-3 mb-4`
 * and the submit's `w-auto` override. What none of them can say, because all
 * three predate the package, is anything about the shell the form is built ON.
 *
 * THE ONE TESTID THAT DOES NOT DERIVE. Under `testIdPrefix="organization-member-add"`
 * the catalog derives a field's id from its NAME, so a field called `userId`
 * would render `organization-member-add-user-id` — not the
 * `organization-member-userid` the oracle, the a11y spec and the restyle spec
 * all select by. The field therefore carries an explicit `testId`, which the
 * catalog also suffixes for its message (`organization-member-userid-error`).
 * The last case below is the guard on exactly that, because the derivation trap
 * is silent: a form that forgot the override still renders, still submits, and
 * simply stops being findable.
 *
 * WHAT THE MIGRATION ADDS (these fail against the hand-rolled form):
 *
 *   1. The `<form>` is the package's `AppForm`, so it is `noValidate`.
 *   2. An empty submit SAYS something. Today the handler's `if (userId.trim() ===
 *      "") return;` swallows it: the button appears dead, with no message and no
 *      request, and nothing on screen explains why.
 *   3. That message is genuinely ASSOCIATED with the input (`aria-invalid` +
 *      `aria-describedby`).
 *   4. The control and the submit disable themselves while the add is in flight.
 *   5. A FAILED ADD IS VISIBLE AT ALL. This is the sharpest gap on the screen:
 *      the form renders no error surface whatsoever, so a rejected add — a bad
 *      id, a duplicate member, a 403 — is completely silent. The user sees an
 *      unchanged roster and an input that did not clear, and has no way to tell
 *      that from a slow refetch.
 *   6. A validation failure's per-property message lands next to the control:
 *      `UserId` is the only property this one-field body can be rejected on, so
 *      the split has exactly one interesting case and it is the common one.
 *   7. A server field error must not WEDGE the form.
 *
 * WHAT THE MIGRATION MUST NOT DROP (regression guards):
 *
 *   8. The POST body and the input clearing after a success. The oracle pins the
 *      body; the CLEAR moves house — it is a `mutate` per-call `onSuccess` today
 *      and becomes the hook's `onSuccess` + `form.reset()` — so it is restated
 *      here.
 *   9. The `organization-member-userid` testid, per the derivation note above.
 *
 * Same seam as the oracles: the REAL SDK with only its `fetch` faked, real
 * router context via `renderWithWallow`, real headless Chromium.
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
    // Today the handler returns early on an empty value: no message, no request,
    // and a button that looks broken.
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
    // The early return already trimmed; a zod schema only keeps that behaviour if
    // the trim is part of the schema — but the MESSAGE is new either way.
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
    // The form has NO error surface today, so this is the whole of what the user
    // currently learns about a rejected add: nothing.
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
    // REGRESSION GUARD. The oracle pins the body; the CLEAR moves house — it is a
    // per-call `mutate` `onSuccess` today and becomes the hook's `onSuccess` plus
    // `form.reset()` — so it is restated here rather than assumed.
    seedRoster();

    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("organization-member-userid"), "u9");
    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await vi.waitFor(() => {
      expect(addCalls()).toHaveLength(1);
    });
    expect(addCalls()[0]?.body).toEqual({ userId: "u9" });
    await expect.poll(() => userIdInput().value).toBe("");
  });

  it("keeps the user-id control on organization-member-userid, not the derived id", async () => {
    // REGRESSION GUARD on the derivation trap. A field named `userId` under
    // `testIdPrefix="organization-member-add"` derives
    // `organization-member-add-user-id`; three closed specs select the control by
    // `organization-member-userid`, so the field must carry the explicit
    // override — and the message must follow it.
    seedRoster();

    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    expect(page.getByTestId("organization-member-add-user-id").elements()).toHaveLength(0);

    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await expect.element(page.getByTestId("organization-member-userid-error")).toBeInTheDocument();
    expect(page.getByTestId("organization-member-add-user-id-error").elements()).toHaveLength(0);
  });
});
