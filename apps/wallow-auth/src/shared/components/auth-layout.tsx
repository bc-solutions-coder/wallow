import type { ReactNode } from "react";

import { forkBranding, type ResolvedBranding } from "@bc-solutions-coder/styles";
import {
  BrandedHeader,
  CenteredCardLayout,
  ForkAttribution,
  ThemeToggle,
} from "@bc-solutions-coder/ui";

import { appIconUrl, forkResolvedBranding } from "../lib/branding";
import { forkLinks } from "../lib/fork-links";

/**
 * The footer rule plus the fork attribution beneath the page body. The
 * attribution itself is `@bc-solutions-coder/ui`'s {@link ForkAttribution}, fed
 * the fork's own branding as props — the ui primitive owns the link/plain
 * branches and the "A {fork} App" markup that this layout used to inline.
 */
function ForkFooter() {
  return (
    <div className="mt-8 pt-4 border-t border-border">
      <ForkAttribution
        appName={forkBranding.appName}
        data-testid="fork-attribution"
        iconUrl={appIconUrl}
        repositoryUrl={forkLinks().repositoryUrl}
      />
    </div>
  );
}

/**
 * The theme control, on its own row ABOVE the branded heading. wallow-auth has
 * no nav, so this layout is the app's only shared home for it — and the `<h1>`
 * below is `FocusOnNavigate`'s route-change focus target, so a control placed
 * inside it would be announced as part of every screen's name.
 */
function ThemeControl() {
  return (
    <div className="flex justify-end mb-2">
      <ThemeToggle data-testid="theme-toggle" />
    </div>
  );
}

export interface AuthLayoutProps {
  /**
   * Branding to render — normally the resolved transaction branding from
   * `useTransactionBranding()`. Defaults to the fork's own branding, which is
   * also the fallback whenever no client is identified or its context cannot
   * be fetched.
   */
  readonly branding?: ResolvedBranding;
  /**
   * The organization the branded client belongs to, rendered as a "by <name>"
   * line beneath the header. Only ever set alongside a third-party client's
   * `branding` — the fork's own header and first-party clients carry no
   * attribution line.
   */
  readonly organizationName?: string | null;
  readonly children?: ReactNode;
}

/**
 * The chrome every auth page (login/register/reset) renders inside
 * (Wallow-vec7.1.5) — the React port of `Components/Layout/AuthLayout.razor`:
 * a centred column with the branded logo/name/tagline above the page body and an
 * "A {fork} App" footer below it.
 *
 * The two branding layers are deliberately different: the *heading* shows the
 * requesting client's branding (or the fork's
 * when no client is identified), while the *footer* always attributes the fork.
 * That is the point of the footer — on a page branded "Acme", it is what still
 * says the login is served by Wallow.
 *
 * The theme CSS variables this markup consumes (`bg-background`,
 * `text-muted-foreground`, ...) are emitted into the document head by the root
 * route from the same {@link ResolvedBranding}; see `routes/__root.tsx`.
 */
export function AuthLayout({ branding, organizationName, children }: AuthLayoutProps): ReactNode {
  const resolved: ResolvedBranding = branding ?? forkResolvedBranding;

  return (
    <CenteredCardLayout>
      <ThemeControl />
      <BrandedHeader
        name={resolved.name}
        tagline={resolved.tagline}
        logoUrl={resolved.logoUrl}
        organizationName={organizationName}
        data-testid="auth-header"
      />
      {children}
      <ForkFooter />
    </CenteredCardLayout>
  );
}
