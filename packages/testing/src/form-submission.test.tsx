import { page, userEvent } from "vitest/browser";
import { afterEach, describe, expect, it } from "vitest";

import { navigationEscapes } from "./navigation-escape";
import { captureFormSubmission, type CapturedFormSubmission } from "./form-submission";

/**
 * `captureFormSubmission()` hands a spec the body a full-page POST would have
 * carried. `.tsx` for the real document: the browser assembles the entries, and
 * only a real submit reaches the submitter's name/value.
 */

function mountForm(): void {
  document.body.innerHTML = `
    <form method="post" action="/connect/authorize">
      <input type="hidden" name="client_id" value="web" />
      <input type="hidden" name="scope" value="openid profile" />
      <input type="hidden" name="resource" value="a" />
      <input type="hidden" name="resource" value="b" />
      <button type="submit" name="decision" value="granted" data-testid="approve">Approve</button>
      <button type="submit" name="decision" value="denied" data-testid="deny">Deny</button>
    </form>`;
}

afterEach(() => {
  document.body.innerHTML = "";
});

describe("captureFormSubmission", () => {
  it("reports the action, method and the fields the browser would send", async () => {
    mountForm();
    const submission: Promise<CapturedFormSubmission> = captureFormSubmission();

    await userEvent.click(page.getByTestId("approve"));

    expect(await submission).toEqual({
      action: `${globalThis.location.origin}/connect/authorize`,
      method: "post",
      fields: [
        ["client_id", "web"],
        ["scope", "openid profile"],
        ["resource", "a"],
        ["resource", "b"],
        ["decision", "granted"],
      ],
    });
  });

  it("attributes the decision to the button that submitted", async () => {
    mountForm();
    const submission: Promise<CapturedFormSubmission> = captureFormSubmission();

    await userEvent.click(page.getByTestId("deny"));
    const { fields } = await submission;

    expect(fields).toContainEqual(["decision", "denied"]);
    expect(fields).not.toContainEqual(["decision", "granted"]);
  });

  it("cancels the submission so the page stays put", async () => {
    mountForm();
    const submission: Promise<CapturedFormSubmission> = captureFormSubmission();

    await userEvent.click(page.getByTestId("approve"));
    await submission;

    expect(navigationEscapes()).toEqual([]);
    expect(document.querySelector("form")).not.toBeNull();
  });
});
