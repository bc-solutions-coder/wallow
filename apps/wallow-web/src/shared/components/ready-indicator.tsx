import { ReadyIndicator as SharedReadyIndicator } from "@bc-solutions-coder/ui";
import type { ReactElement } from "react";

/** The `data-testid` the web app's ready element carries, per the repo's E2E selector rules. */
const READY_TEST_ID = "web-ready";

/** Web-app readiness signal — the shared {@link SharedReadyIndicator} bound to `web-ready`. */
export function ReadyIndicator(): ReactElement {
  return <SharedReadyIndicator testId={READY_TEST_ID} />;
}
