/**
 * What the `AppForm` shell publishes to every component rendered inside it:
 * `FormError`, `SubmitButton`, and (from Wallow-ov6w.2.4) every catalog field.
 *
 * The prefix lives here rather than being threaded through each child's props
 * because every testid in a form derives from ONE value — see `core/test-id.ts`
 * — so a migrated form declares it once on the shell and its Playwright ids stay
 * byte-identical.
 */

import { createContext, useContext } from "react";

/** The shell's published state. `serverError` is normalized to `null`, never `undefined`. */
export interface AppFormContextValue {
  /** The form's testid prefix, e.g. `"inquiry"`; children derive their own ids from it. */
  readonly testIdPrefix: string;
  /** Whether the submit is in flight, so `SubmitButton` can disable and swap its label. */
  readonly pending: boolean;
  /** The form-level (non-field) server error `FormError` renders, or `null`. */
  readonly serverError: string | null;
}

export const AppFormContext = createContext<AppFormContextValue | null>(null);

/**
 * The shell's state, for a component rendered inside `<AppForm>`.
 *
 * Throws when there is no shell above the caller: there is no sensible fallback
 * prefix, and a silent default would stamp an `undefined-submit` testid that no
 * suite selects and nobody notices until Playwright goes red.
 */
export function useAppFormContext(): AppFormContextValue {
  const value = useContext(AppFormContext);

  if (value === null) {
    throw new Error("forms components must render inside <AppForm>");
  }

  return value;
}
