import type { ReactNode } from "react";

import { forkBranding, forkResolvedBranding, type ResolvedBranding } from "../lib/branding";

/** The fork's icon, shown at the size the attribution footer uses. */
function ForkIcon() {
  return <img src={forkBranding.appIcon} alt={forkBranding.appName} className="size-8" />;
}

/** "A {fork} App" — the fork's name emphasised within the sentence. */
function ForkAttributionText() {
  return (
    <span className="text-xs text-muted-foreground">
      A <span className="font-semibold text-muted-foreground">{forkBranding.appName}</span> App
    </span>
  );
}

/**
 * The fork attribution, linked to the fork's repository when it publishes one.
 * Mirrors the two branches of the Blazor layout's footer.
 */
function ForkAttribution() {
  const repositoryUrl: string | undefined =
    forkBranding.repositoryUrl === "" ? undefined : forkBranding.repositoryUrl;

  if (repositoryUrl === undefined) {
    return (
      <div className="flex items-center justify-center gap-1.5">
        <ForkIcon />
        <ForkAttributionText />
      </div>
    );
  }

  return (
    <a
      href={repositoryUrl}
      target="_blank"
      rel="noopener noreferrer"
      className="flex items-center justify-center gap-1.5 hover:opacity-80 transition-opacity"
    >
      <ForkIcon />
      <ForkAttributionText />
    </a>
  );
}

/** The footer rule plus the fork attribution beneath the page body. */
function ForkFooter() {
  return (
    <div className="mt-8 pt-4 border-t border-border">
      <ForkAttribution />
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
      <h1 className="text-2xl font-bold text-foreground">{branding.name}</h1>
      {branding.tagline !== null && (
        <p className="text-sm text-muted-foreground mt-1">{branding.tagline}</p>
      )}
    </div>
  );
}

/** The fixed-width column: branding above the page body, fork attribution below. */
function AuthCard({
  branding,
  children,
}: {
  readonly branding: ResolvedBranding;
  readonly children?: ReactNode;
}) {
  return (
    <div className="w-full max-w-[420px]">
      <BrandingHeader branding={branding} />
      {children}
      <ForkFooter />
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
 * The two branding layers are deliberately different, matching the Blazor
 * original: the *heading* shows the requesting client's branding (or the fork's
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
    <div className="min-h-screen bg-background flex flex-col items-center justify-center px-4">
      <AuthCard branding={resolved}>{children}</AuthCard>
    </div>
  );
}
