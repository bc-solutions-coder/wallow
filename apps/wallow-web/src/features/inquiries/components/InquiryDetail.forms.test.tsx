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
 * The add-comment form as built on `@bc-solutions-coder/forms`: the shell, the
 * required rule, in-flight disabling, server-error routing, and the
 * post-success reset.
 *
 * Runs the real SDK over a faked fetch (sdk-harness). The screen fires the
 * detail and comments reads together, so `routeHarness` answers each by URL,
 * and `addCalls()` filters the POSTs out of a `harness.calls` that also holds
 * those reads and every post-success refetch.
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
 * Wait for the detail read to paint: the reads go over the wire rather than out
 * of a pre-seeded cache, so the controls do not exist at first paint.
 */
async function awaitLoaded(): Promise<void> {
  await expect.element(page.getByTestId("inquiry-detail-heading")).toBeInTheDocument();
  await expect.element(page.getByTestId("inquiry-comment-content")).toBeInTheDocument();
}

/** Only the add POSTs, so "the endpoint was not reached" is said about them. */
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
 * The internal flag is Base UI's `Checkbox.Root`, NOT a native button — so its
 * disabled state is read off `aria-disabled` / `data-disabled` rather than the
 * `.disabled` property, which exists only on the sibling hidden `<input>`.
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
    // comment box into a single-line one.
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    const content: HTMLTextAreaElement = contentTextarea();
    expect(content.tagName).toBe("TEXTAREA");
    // `tagName` alone is satisfied by a hand-rolled `<textarea>` too. These two
    // utilities are the only ones `textareaRecipe` adds, so they are what says
    // "the CATALOG control".
    for (const utility of ["min-h-20", "resize-y"]) {
      expect(content.classList.contains(utility), utility).toBe(true);
    }
  });

  it("associates a required-comment message with the textarea instead of posting an empty body", async () => {
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
    // `min(1)` posts three spaces into the thread.
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
    // A second click would post the same comment twice into a thread with no
    // undo. The flag is a non-native button, so this is where its state lives.
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
    // Leaving the flag stuck on is how the NEXT comment gets posted internal by
    // accident — or, the other way round, how a private note ends up public.
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
    // The derivation trap, and it is silent: a boolean field named `isInternal`
    // under `testIdPrefix="inquiry-comment"` derives
    // `inquiry-comment-is-internal`, so a form missing the explicit override
    // still renders, still submits, and simply stops being findable.
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    expect(page.getByTestId("inquiry-comment-is-internal").elements()).toHaveLength(0);
    expect(internalFlag().getAttribute("role")).toBe("checkbox");
  });
});
