import {
  Card,
  CardTitle,
  CenteredCardLayout,
  ForkAttribution,
  MutedText,
} from "@bc-solutions-coder/ui";
import { appIconUrl, forkResolvedBranding } from "@bc-solutions-coder/styles";
import type { ReactElement } from "react";

/**
 * The one screen this minimal reference app renders — a centred card composed
 * entirely from `@bc-solutions-coder/ui` primitives, themed by the brand tokens
 * `@bc-solutions-coder/styles` emits. It renders no live data, so the app boots
 * with no backend; the SDK wiring it demonstrates lives in `src/lib/sdk.ts`.
 */
export function HelloCard(): ReactElement {
  return (
    <CenteredCardLayout data-testid="hello-layout">
      <Card>
        <CardTitle data-testid="hello-heading">Hello from {forkResolvedBranding.name}</CardTitle>
        <MutedText data-testid="hello-body">
          This minimal app wires all five shared packages — sdk, styles, ui, testing, and web-shell
          — through the web-shell host factory. See the README for the golden path.
        </MutedText>
        <ForkAttribution
          appName={forkResolvedBranding.name}
          iconUrl={appIconUrl}
          data-testid="hello-attribution"
        />
      </Card>
    </CenteredCardLayout>
  );
}
