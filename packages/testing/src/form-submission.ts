/**
 * Capture and cancel the next native form submission before it navigates away.
 * Call captureFormSubmission() before clicking the submit button, then await its result.
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
 * Capture and cancel the next document submit event.
 *
 * Install this listener before triggering the submit and await the returned promise
 * afterwards. The result includes the resolved action, method, fields, and submitter
 * value. If no submit event arrives, the promise remains pending.
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
