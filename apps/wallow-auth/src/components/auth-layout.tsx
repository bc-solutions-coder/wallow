import type { ReactNode } from "react";

import { CenteredCardLayout, ForkAttribution } from "@bc-solutions-coder/ui";

import {
  appIconUrl,
  forkBranding,
  forkResolvedBranding,
  type ResolvedBranding,
} from "../lib/branding";

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
        iconUrl={appIconUrl}
        repositoryUrl={forkBranding.repositoryUrl}
      />
    </div>
  );
}

/** The resolved logo, rendered only when there is one to show. */
function BrandingLogo({ src, alt }: { readonly src: string; readonly alt: string }) {
  return (
    <img
      src={src}
      alt={alt}
      className="size-30 mx-auto block"
      style={{ shapeRendering: "geometricPrecision" }}
    />
  );
}

/** Logo, name, and tagline for whoever the page is branded as. */
function BrandingHeader({ branding }: { readonly branding: ResolvedBranding }) {
  return (
    <div className="text-center mb-8">
      {branding.logoUrl !== null && <BrandingLogo src={branding.logoUrl} alt={branding.name} />}
      <h1 data-focus-target tabIndex={-1} className="text-2xl font-bold text-foreground">
        {branding.name}
      </h1>
      {branding.tagline !== null && (
        <p className="text-sm text-muted-foreground mt-1">{branding.tagline}</p>
      )}
    </div>
  );
}

export interface AuthLayoutProps {
  /**
   * Branding to render — normally {@link mergeClientBranding}'s output for the
   * request's `client_id`. Defaults to the fork's own branding, which is also
   * the fallback whenever no client is identified or its branding cannot be
   * fetched.
   */
  readonly branding?: ResolvedBranding;
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
export function AuthLayout({ branding, children }: AuthLayoutProps): ReactNode {
  const resolved: ResolvedBranding = branding ?? forkResolvedBranding;

  return (
    <CenteredCardLayout>
      <BrandingHeader branding={resolved} />
      {children}
      <ForkFooter />
    </CenteredCardLayout>
  );
}
