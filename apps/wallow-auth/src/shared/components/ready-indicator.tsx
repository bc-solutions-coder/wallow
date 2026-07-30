import { ReadyIndicator as SharedReadyIndicator } from "@bc-solutions-coder/ui";
import type { ReactElement } from "react";

export { READY_ATTRIBUTE } from "@bc-solutions-coder/ui";

/** The `data-testid` the auth app's ready element carries, per the repo's E2E selector rules. */
export const READY_TEST_ID = "auth-ready";

/** Auth-app readiness signal — the shared {@link SharedReadyIndicator} bound to `auth-ready`. */
export function ReadyIndicator(): ReactElement {
  return <SharedReadyIndicator testId={READY_TEST_ID} />;
}
