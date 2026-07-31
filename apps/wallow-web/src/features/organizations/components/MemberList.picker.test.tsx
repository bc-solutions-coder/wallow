import {
  createSdkHarness,
  type SdkCall,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { routeHarness } from "@shared/testing/harness-routes";
import { MemberList } from "./MemberList";

/**
 * The add-member USER PICKER (Wallow-lrlm.6.1).
 *
 * The form migrated onto `@bc-solutions-coder/forms` in Wallow-lrlm.5.5, but the
 * control it renders is still a bare text box: the only way to add someone is to
 * already know their user id and type it in by hand. This spec is the acceptance
 * criterion for replacing it with the catalog's searchable picker
 * (`Combobox`/`Autocomplete` from `@bc-solutions-coder/ui`) fed by the users
 * operation, so a person can be found by typing part of their email.
 *
 * WHY A NEW FILE. Four specs already sit beside this one and all four are frozen
 * oracles of closed tasks — `MemberList.test.tsx` (roster, payloads, sweep),
 * `MemberList.forms.test.tsx` (the forms-shell migration),
 * `MemberList.a11y.test.tsx` (the control's accessible name) and
 * `MemberList.restyle.test.tsx` (the inline row). None of them is edited here.
 *
 * WHAT IS ASSERTED, AND WHY IT IS ASSERTED THIS WAY. Every case below drives the
 * rendered widget — the ARIA the control publishes, the options that survive a
 * query, and the request that leaves the app. None of them reads a class string:
 * a `className` assertion cannot tell a real combobox from an `<input>` that was
 * handed a combobox's utilities, and this epic has already shipped one defect
 * past a green class-string spec.
 *
 *   1. The control is a combobox widget: `role="combobox"`, collapsed until the
 *      user types, then owning a real `listbox`.
 *   2. Its options come from the users OPERATION, not from a hand-rolled list.
 *   3. Typing FILTERS those options — the headline acceptance criterion.
 *   4. Choosing a person posts THAT PERSON'S ID, which is the whole point: the
 *      user searched by email and the wire still carries `userId`.
 *
 * WHAT MUST NOT REGRESS (5 below, restated on purpose). The closed oracles all
 * type a bare id into `organization-member-userid` and expect it posted verbatim.
 * A picker with pure item-commit semantics (Base UI's `Combobox.Root` holds the
 * SELECTED ITEM, not the input text) silently drops that text and every one of
 * those specs breaks at once. Restating the guard here puts the constraint next
 * to the change that threatens it rather than in a file this task must not edit.
 *
 * Same seam as the oracles: the REAL SDK with only its `fetch` faked, real router
 * context via `renderWithWallow`, real headless Chromium, no `vi.mock` of the
 * catalog.
 */

/** The members endpoint this section reads and writes. */
const MEMBERS_PATH = "/v1/identity/organizations/o1/members";

/** The operation the picker's options have to come from. */
const USERS_PATH = "/v1/identity/users";

/**
 * The directory the picker searches. Ids are opaque on purpose: none of them
 * contains `acme`, `globex` or `carol`, so a filter that matched the ID rather
 * than the email could not pass the cases below.
 */
const USERS = [
  {
    id: "11111111-1111-1111-1111-111111111111",
    email: "ada@acme.io",
    firstName: "Ada",
    lastName: "Lovelace",
    enabled: true,
    roles: ["Owner"],
  },
  {
    id: "22222222-2222-2222-2222-222222222222",
    email: "bob@acme.io",
    firstName: "Bob",
    lastName: "Rivers",
    enabled: true,
    roles: ["Member"],
  },
  {
    id: "33333333-3333-3333-3333-333333333333",
    email: "carol@globex.io",
    firstName: "Carol",
    lastName: "Nguyen",
    enabled: true,
    roles: ["Member"],
  },
];

/** Carol's id — what the wire must carry after Carol is picked by email. */
const CAROL_ID = "33333333-3333-3333-3333-333333333333";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Answer the roster read with an empty organization, the directory read with
 * {@link USERS}, and the add POST with `addResponse`.
 *
 * The roster is deliberately EMPTY: every seeded user is addable, so no case
 * below depends on how the picker treats someone who is already a member.
 */
function seedDirectory(addResponse: unknown = {}): void {
  routeHarness(harness, {
    [`GET ${MEMBERS_PATH}`]: [],
    [`GET ${USERS_PATH}`]: { items: USERS, totalCount: USERS.length, page: 1, pageSize: 20 },
    [`POST ${MEMBERS_PATH}`]: addResponse,
  });
}

/** Wait for the form to paint — the section is on screen once the control is. */
async function awaitLoaded(): Promise<void> {
  await expect.element(page.getByTestId("organization-member-userid")).toBeInTheDocument();
}

/** The picker's control. Still an `<input>`, still on the id four closed specs select. */
function pickerInput(): HTMLInputElement {
  return page.getByTestId("organization-member-userid").element() as HTMLInputElement;
}

/** The visible text of every option currently offered by the picker. */
function optionTexts(): readonly string[] {
  return page
    .getByTestId("organization-member-userid-option")
    .elements()
    .map((option: Element) => option.textContent?.trim() ?? "");
}

/** Whether some option on screen names `email`. */
function listsUser(email: string): boolean {
  return optionTexts().some((text: string) => text.includes(email));
}

/** Only the add POSTs — `harness.calls` also holds the roster and directory reads. */
function addCalls(): readonly SdkCall[] {
  return harness.calls.filter(
    (call: SdkCall) => call.method === "POST" && call.path.endsWith(MEMBERS_PATH),
  );
}

describe("MemberList add-member user picker", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    seedDirectory();
  });

  it("renders the user picker as a combobox, not a plain text box", async () => {
    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    // The widget contract, read off the accessibility tree rather than off a
    // class string: a plain `Input` publishes neither of these.
    expect(pickerInput().getAttribute("role")).toBe("combobox");
    expect(pickerInput().getAttribute("aria-expanded")).toBe("false");
  });

  it("expands into a real listbox once the user starts typing", async () => {
    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("organization-member-userid"), "acme");

    await expect.poll(() => pickerInput().getAttribute("aria-expanded")).toBe("true");

    // `CSS.escape` because React `useId` values contain `:`, which is not a
    // valid id selector unescaped — the same guard `MemberList.forms.test.tsx`
    // uses when it walks the control's `aria-describedby` chain.
    const listId: string = pickerInput().getAttribute("aria-controls") ?? "";
    expect(listId).not.toBe("");
    expect(document.querySelector(`#${CSS.escape(listId)}`)?.getAttribute("role")).toBe("listbox");
  });

  it("sources its options from the users operation rather than a hand-rolled list", async () => {
    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    await vi.waitFor(() => {
      const read: SdkCall | undefined = harness.calls.find(
        (call: SdkCall) => call.method === "GET" && call.path.endsWith(USERS_PATH),
      );
      expect(read).toBeDefined();
    });
  });

  it("narrows the options to the people whose email matches what was typed", async () => {
    // THE HEADLINE CRITERION. `acme` is in two of the three seeded emails and in
    // none of the ids, so a picker that filtered on the id — or did not filter at
    // all — cannot pass this.
    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("organization-member-userid"), "acme");

    await expect.poll(() => listsUser("ada@acme.io")).toBe(true);
    expect(listsUser("bob@acme.io")).toBe(true);
    expect(listsUser("carol@globex.io")).toBe(false);
    expect(optionTexts()).toHaveLength(2);
  });

  it("narrows to a single person as the query gets more specific", async () => {
    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("organization-member-userid"), "carol");

    await expect.poll(() => optionTexts()).toHaveLength(1);
    expect(listsUser("carol@globex.io")).toBe(true);
  });

  it("posts the chosen person's user id after they are picked by email", async () => {
    // The point of the whole task: the operator searched by email and never saw a
    // uuid, but `userId` is what the endpoint takes.
    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("organization-member-userid"), "carol");
    await expect.poll(() => optionTexts()).toHaveLength(1);
    await userEvent.click(page.getByTestId("organization-member-userid-option").first());

    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await vi.waitFor(() => {
      expect(addCalls()).toHaveLength(1);
    });
    expect(addCalls()[0]?.body).toEqual({ userId: CAROL_ID });
  });

  it("still posts a user id typed in by hand, with nothing picked from the list", async () => {
    // REGRESSION GUARD, restated from the closed oracles because this task is
    // exactly what threatens it. Base UI's `Combobox.Root` commits the SELECTED
    // ITEM and holds the typed text separately, so a picker built on it without
    // wiring the input value back into form state silently posts nothing here —
    // and takes `MemberList.test.tsx` and five cases in `MemberList.forms.test.tsx`
    // down with it.
    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("organization-member-userid"), "u9");
    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await vi.waitFor(() => {
      expect(addCalls()).toHaveLength(1);
    });
    expect(addCalls()[0]?.body).toEqual({ userId: "u9" });
  });
});
