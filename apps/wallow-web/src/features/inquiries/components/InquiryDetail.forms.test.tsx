import {
  createSdkHarness,
  type SdkCall,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { failsWith, neverSettles, routeHarness } from "@shared/testing/harness-routes";
import { InquiryDetail } from "./InquiryDetail";

/**
 * The ADD-COMMENT form ON `@bc-solutions-coder/forms` (Wallow-lrlm.5.5).
 *
 * WHY A NEW FILE. The four specs already beside this one are the screen's frozen
 * oracles and the migration's acceptance criterion is that all four pass
 * UNCHANGED, so none of them is edited here: `InquiryDetail.test.tsx` pins both
 * comment payloads, the comments-operation sweep and the RFC 7807 `detail`
 * banner, `InquiryDetail.catalog.test.tsx` pins the internal flag as a catalog
 * `Checkbox` that reports its own state, `InquiryDetail.a11y.test.tsx` pins the
 * textarea's accessible name, and `InquiryDetail.restyle.test.tsx` pins the
 * form's rhythm and the control's frame. What none of them can say, because all
 * four predate the package, is anything about the shell the form is built ON.
 *
 * DELIBERATELY NOT RESTATED HERE: the two POST bodies, the sweep and the
 * form-level `detail` banner. All four are pinned by `InquiryDetail.test.tsx`,
 * and a second copy would only create something to keep in sync. The banner in
 * particular changes MECHANISM in this migration — `mutation.isError` +
 * `errorText` becomes `FormError` + `splitServerError` — but not OUTCOME, and the
 * oracle asserts the outcome.
 *
 * THE ONE TESTID THAT DOES NOT DERIVE. Under `testIdPrefix="inquiry-comment"` the
 * catalog derives a field's id from its NAME, so a boolean field called
 * `isInternal` would render `inquiry-comment-is-internal` — not the
 * `inquiry-comment-internal` that `InquiryDetail.test.tsx` clicks and
 * `InquiryDetail.catalog.test.tsx` inspects. The last case below is the guard on
 * exactly that, because the trap is silent: a form that forgot the override still
 * renders, still submits, and simply stops being findable.
 *
 * WHAT THE MIGRATION ADDS (these fail against the hand-rolled form):
 *
 *   1. The `<form>` is the package's `AppForm`, so it is `noValidate`.
 *   2. There is a required rule on the comment at all. Today an empty submit
 *      POSTs `{ content: "", isInternal: false }` and lets the API reject it —
 *      the round trip the oracle's own "Comment must not be empty." fixture is a
 *      recording of.
 *   3. That message is genuinely ASSOCIATED with the textarea (`aria-invalid` +
 *      `aria-describedby`), which the sibling `ErrorBanner` never was.
 *   4. The textarea, the internal flag and the submit disable themselves while
 *      the add is in flight, so a second click cannot post the comment twice into
 *      a thread that has no way to undo it.
 *   5. A validation failure's per-property message lands NEXT TO the control.
 *      Today `errors` is dropped on the floor and only `detail` reaches the
 *      screen, so an API that named the offending property says nothing about it.
 *   6. A server field error must not WEDGE the form.
 *
 * WHAT THE MIGRATION MUST NOT DROP (regression guards):
 *
 *   7. Both controls reset after a successful add. That is a per-call `mutate`
 *      `onSuccess` today and becomes the hook's `onSuccess` + `form.reset()`, and
 *      no oracle asserts it — a thread would otherwise keep the last comment in
 *      the box and the internal flag stuck on, which is how a private note gets
 *      posted publicly next time.
 *   8. The internal flag keeps `inquiry-comment-internal`, per the derivation
 *      note above.
 *
 * Same seam as the oracles: the REAL SDK with only its `fetch` faked, real router
 * context via `renderWithWallow`, real headless Chromium. The screen fires the
 * detail and comments reads together, so `routeHarness` answers each by URL.
 */

const inquiry = {
  id: "i1",
  name: "Ada Lovelace",
  email: "ada@example.com",
  company: "Analytical Engines",
  projectType: "web-app",
  status: "New",
  createdAt: "2026-07-15T00:00:00Z",
};

/** The comments endpoint this form reads and writes. */
const COMMENTS_PATH = "/v1/inquiries/i1/comments";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/** Answer the detail + comments reads with a loaded inquiry, and the add POST with `addResponse`. */
function seedLoaded(addResponse: unknown = {}): void {
  routeHarness(
    harness,
    {
      "GET /v1/inquiries/i1": inquiry,
      [`GET ${COMMENTS_PATH}`]: [],
      [`POST ${COMMENTS_PATH}`]: addResponse,
    },
    { fallback: [] },
  );
}

/**
 * Wait for the detail read to paint before driving a control. The screen's
 * controls do not exist at first paint: the reads go over the wire rather than
 * out of a pre-seeded cache.
 */
async function awaitLoaded(): Promise<void> {
  await expect.element(page.getByTestId("inquiry-detail-heading")).toBeInTheDocument();
  await expect.element(page.getByTestId("inquiry-comment-content")).toBeInTheDocument();
}

/**
 * Only the add POSTs. `harness.calls` also holds the two reads and every
 * post-success refetch, so "the endpoint was not reached" has to be said about
 * this operation rather than about the transport as a whole.
 */
function addCalls(): readonly SdkCall[] {
  return harness.calls.filter(
    (call: SdkCall) => call.method === "POST" && call.path.endsWith(COMMENTS_PATH),
  );
}

function formElement(): HTMLFormElement {
  return page.getByTestId("inquiry-comment-form").element() as HTMLFormElement;
}

function contentTextarea(): HTMLTextAreaElement {
  return page.getByTestId("inquiry-comment-content").element() as HTMLTextAreaElement;
}

/**
 * The internal flag is Base UI's `Checkbox.Root`, which is NOT a native button —
 * so it is typed as a plain `HTMLElement` and its disabled state is read off
 * `aria-disabled` / `data-disabled` rather than the `.disabled` property, which
 * exists only on the sibling hidden `<input>`.
 */
function internalFlag(): HTMLElement {
  return page.getByTestId("inquiry-comment-internal").element() as HTMLElement;
}

function submitButton(): HTMLButtonElement {
  return page.getByTestId("inquiry-comment-submit").element() as HTMLButtonElement;
}

/** The ids `control` points its `aria-describedby` at. */
function describedByIds(control: HTMLElement): readonly string[] {
  return (control.getAttribute("aria-describedby") ?? "")
    .split(" ")
    .filter((id: string) => id !== "");
}

describe("InquiryDetail add-comment form on @bc-solutions-coder/forms", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("renders through the forms shell, which leaves validation to the schema", async () => {
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    const element: HTMLFormElement = formElement();
    expect(element.tagName).toBe("FORM");
    expect(element.noValidate).toBe(true);
  });

  it("keeps the textarea, the internal flag and the submit under that one shell", async () => {
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    const element: HTMLFormElement = formElement();
    for (const testId of [
      "inquiry-comment-content",
      "inquiry-comment-internal",
      "inquiry-comment-submit",
    ]) {
      const control: HTMLElement = page.getByTestId(testId).element() as HTMLElement;
      expect(control.closest("form"), `${testId} must live under the form shell`).toBe(element);
    }
  });

  it("keeps the comment control a real textarea", async () => {
    // A catalog field that resolved to an `<input>` would turn a multi-paragraph
    // comment box into a single-line one, and no oracle would notice: the a11y
    // spec asserts the NAME and the restyle spec asserts the frame.
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    const content: HTMLTextAreaElement = contentTextarea();
    expect(content.tagName).toBe("TEXTAREA");
    // `tagName` alone is satisfied by a hand-rolled `<textarea>` too, and
    // `InquiryDetail.restyle.test.tsx`'s class pin was narrowed to the
    // input/textarea OVERLAP in this same task — so these two utilities, which
    // ONLY `textareaRecipe` adds, are what still says "the CATALOG control".
    for (const utility of ["min-h-20", "resize-y"]) {
      expect(content.classList.contains(utility), utility).toBe(true);
    }
  });

  it("associates a required-comment message with the textarea instead of posting an empty body", async () => {
    // Today an empty submit posts `{ content: "" }` and waits for the API to say
    // no — the round trip the oracle's "Comment must not be empty." fixture is a
    // recording of.
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    await userEvent.click(page.getByTestId("inquiry-comment-submit"));

    const message = page.getByTestId("inquiry-comment-content-error");
    await expect.element(message).toBeInTheDocument();
    await expect.element(message).toHaveTextContent("Comment is required");

    const messageId: string = message.element().id;
    expect(messageId).not.toBe("");
    expect(describedByIds(contentTextarea())).toContain(messageId);
    expect(contentTextarea().getAttribute("aria-invalid")).toBe("true");
    expect(addCalls()).toHaveLength(0);
  });

  it("rejects a whitespace-only comment without reaching the endpoint", async () => {
    // `.trim()` in the schema is what makes `"   "` fail the `min(1)`; a bare
    // `min(1)` would post three spaces into the thread.
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("inquiry-comment-content"), "   ");
    await userEvent.click(page.getByTestId("inquiry-comment-submit"));

    await expect
      .element(page.getByTestId("inquiry-comment-content-error"))
      .toHaveTextContent("Comment is required");
    expect(addCalls()).toHaveLength(0);
  });

  it("disables the textarea, the internal flag and the submit while the add is in flight", async () => {
    // Scoped to the POST so the two reads still settle and the form paints.
    seedLoaded(neverSettles());

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("inquiry-comment-content"), "Following up");
    await userEvent.click(page.getByTestId("inquiry-comment-submit"));

    await vi.waitFor(() => {
      expect(addCalls()).toHaveLength(1);
    });
    await expect.poll(() => contentTextarea().disabled).toBe(true);
    // A second click would post the same comment twice into a thread with no undo.
    // The flag is a non-native button, so this is where its disabled state lives.
    expect(internalFlag().getAttribute("aria-disabled")).toBe("true");
    expect(Object.hasOwn(internalFlag().dataset, "disabled")).toBe(true);
    expect(submitButton().disabled).toBe(true);
  });

  it("shows a validation failure's per-property message on the field, not the banner", async () => {
    seedLoaded(
      failsWith(
        {
          type: "https://httpstatuses.io/400",
          title: "One or more validation errors occurred.",
          status: 400,
          errors: { Content: ["A comment may not exceed 4000 characters."] },
        },
        400,
      ),
    );

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("inquiry-comment-content"), "Following up");
    await userEvent.click(page.getByTestId("inquiry-comment-submit"));

    await expect
      .element(page.getByTestId("inquiry-comment-content-error"))
      .toHaveTextContent("A comment may not exceed 4000 characters.");
    // Everything landed on a field, so the banner would only repeat it.
    expect(page.getByTestId("inquiry-comment-error").elements()).toHaveLength(0);
  });

  it("clears a server field error on the next submit rather than wedging the form", async () => {
    seedLoaded(
      failsWith(
        {
          type: "https://httpstatuses.io/400",
          title: "One or more validation errors occurred.",
          status: 400,
          errors: { Content: ["A comment may not exceed 4000 characters."] },
        },
        400,
      ),
    );

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("inquiry-comment-content"), "Following up");
    await userEvent.click(page.getByTestId("inquiry-comment-submit"));
    await expect.element(page.getByTestId("inquiry-comment-content-error")).toBeInTheDocument();

    seedLoaded();
    await userEvent.click(page.getByTestId("inquiry-comment-submit"));

    await vi.waitFor(() => {
      expect(addCalls()).toHaveLength(2);
    });
  });

  it("still resets both the comment and the internal flag after a successful add", async () => {
    // REGRESSION GUARD, and one no oracle makes. Leaving the flag stuck on is how
    // the NEXT comment gets posted internal by accident — or, the other way
    // round, how a private note ends up public.
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("inquiry-comment-content"), "Private note");
    await userEvent.click(page.getByTestId("inquiry-comment-internal"));
    await userEvent.click(page.getByTestId("inquiry-comment-submit"));

    await vi.waitFor(() => {
      expect(addCalls()).toHaveLength(1);
    });
    await expect.poll(() => contentTextarea().value).toBe("");
    await expect.poll(() => internalFlag().getAttribute("aria-checked")).toBe("false");
  });

  it("keeps the internal flag on inquiry-comment-internal, not the derived id", async () => {
    // REGRESSION GUARD on the derivation trap. A boolean field named `isInternal`
    // under `testIdPrefix="inquiry-comment"` derives `inquiry-comment-is-internal`;
    // two closed specs drive the flag by `inquiry-comment-internal`, so the field
    // must carry the explicit override.
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    expect(page.getByTestId("inquiry-comment-is-internal").elements()).toHaveLength(0);
    expect(internalFlag().getAttribute("role")).toBe("checkbox");
  });
});
