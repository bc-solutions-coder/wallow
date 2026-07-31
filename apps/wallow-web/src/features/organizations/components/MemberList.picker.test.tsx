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
 * The add-member user picker: the combobox widget, its options from the users
 * operation, email filtering, and what the add POST carries.
 *
 * Assertions read the accessibility tree and the wire, never a class string.
 * Base UI's `Combobox.Root` holds the selected ITEM and the typed text
 * separately, so a hand-typed id must still post.
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

/** The picker's control — an `<input>`, on the id the sibling specs select. */
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

    // A plain `Input` publishes neither of these.
    expect(pickerInput().getAttribute("role")).toBe("combobox");
    expect(pickerInput().getAttribute("aria-expanded")).toBe("false");
  });

  it("expands into a real listbox once the user starts typing", async () => {
    renderWithWallow(<MemberList orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("organization-member-userid"), "acme");

    await expect.poll(() => pickerInput().getAttribute("aria-expanded")).toBe("true");

    // `CSS.escape` because React `useId` values contain `:`, which is not a
    // valid id selector unescaped.
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
    // A picker that does not wire its input value back into form state posts
    // nothing here, and takes the sibling specs down with it.
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
