/**
 * Capture a native form submission instead of letting it leave the runner.
 *
 * A screen that delivers an answer as a full-page `<form method="post">` cannot
 * be asserted through the navigation guard: the guard sees only the action URL,
 * and the body — the part a POST exists to carry — is gone with the document.
 * `captureFormSubmission()` listens for the next `submit` event on the document,
 * cancels it before the browser navigates, and hands back what WOULD have been
 * sent: the resolved action, the method, and the entries the browser itself
 * assembled from the form and the button that submitted it.
 *
 * Call it BEFORE the click, and await it after:
 *
 * ```ts
 * const submission = captureFormSubmission();
 * await user.click(page.getByTestId("consent-approve"));
 * expect((await submission).fields).toContainEqual(["consent_decision", "granted"]);
 * ```
 *
 * The submit is cancelled, so no navigation escape is recorded and the guard's
 * `afterEach` stays quiet. Only one submission is captured per call.
 */

/** A form submission as the browser assembled it, before it was cancelled. */
export interface CapturedFormSubmission {
  /** The resolved (absolute) URL the form was about to post to. */
  readonly action: string;
  /** The HTTP method, lower-cased as the DOM reports it (`post`, `get`). */
  readonly method: string;
  /**
   * Every entry the browser would have sent, in document order — the form's
   * fields followed by the submitter's own name/value when it carries one.
   * File entries are reported by their file name.
   */
  readonly fields: readonly (readonly [name: string, value: string])[];
}

function entryValue(value: FormDataEntryValue): string {
  return typeof value === "string" ? value : value.name;
}

/**
 * Resolve with the next form submission on the document, cancelled so the page
 * stays put. A submission that never comes leaves the promise pending, which
 * the test's own timeout reports.
 */
export function captureFormSubmission(): Promise<CapturedFormSubmission> {
  return new Promise<CapturedFormSubmission>((resolve: (value: CapturedFormSubmission) => void) => {
    document.addEventListener(
      "submit",
      (event: SubmitEvent) => {
        event.preventDefault();

        const form = event.target;
        if (!(form instanceof HTMLFormElement)) {
          throw new TypeError("a submit event fired on something that is not a form");
        }

        const data = new FormData(form, event.submitter);
        const fields: (readonly [string, string])[] = [];
        for (const [name, value] of data) {
          fields.push([name, entryValue(value)]);
        }

        resolve({ action: form.action, method: form.method, fields });
      },
      { once: true, capture: true },
    );
  });
}
