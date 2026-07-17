import type { ReactNode } from "react";

import { appIconUrl, forkBranding } from "../lib/branding";

/**
 * PublicLayout (Wallow-ffpq.3.6) — the React port of Blazor
 * `Components/Layout/PublicLayout.razor`: the chrome wrapped around the public
 * marketing page. It renders a top navbar (logo/app-name linking "/",
 * Features/Docs/GitHub links, and a "Get Started" CTA into the BFF login) and a
 * footer ("MIT Licensed", GitHub + Docs links) around its `children`.
 *
 * All links are plain anchors (not TanStack `Link`s) so the chrome renders
 * standalone under `react-dom/server`/jsdom without a router context — the same
 * constraint the SSR home test (`routes/index.test.tsx`) and `OrganizationDetail`'s
 * back link already observe. The nav/footer are split into subcomponents to keep
 * each component's JSX nesting within the repo's `jsx-max-depth` budget.
 */

// GitHub/Docs targets: prefer the fork's configured repository, else the
// upstream default (mirrors the Blazor oracle's hardcoded links).
const repositoryUrl: string = forkBranding.repositoryUrl ?? "https://github.com/wallowapp/wallow";
const docsUrl = `${repositoryUrl}/tree/main/docs`;

// The BFF login CTA lands the user on the dashboard after authenticating.
const getStartedHref = "/bff/login?returnTo=/dashboard/apps";

const navLinkClass = "text-foreground hover:text-primary no-underline text-sm font-medium";

/** Logo + app-name link back to the landing page. */
function HomeLink() {
  return (
    <a
      href="/"
      data-testid="public-nav-home"
      className="flex items-center gap-2 text-foreground font-bold text-xl no-underline"
    >
      <img
        src={appIconUrl}
        alt={forkBranding.appName}
        className="size-8"
        style={{ shapeRendering: "geometricPrecision" }}
      />
      {forkBranding.appName}
    </a>
  );
}

/** Features / Docs / GitHub nav links. */
function NavLinks() {
  return (
    <div className="flex items-center gap-6">
      <a href="/features" data-testid="public-nav-features" className={navLinkClass}>
        Features
      </a>
      <a
        href={docsUrl}
        target="_blank"
        rel="noreferrer"
        data-testid="public-nav-docs"
        className={navLinkClass}
      >
        Docs
      </a>
      <a
        href={repositoryUrl}
        target="_blank"
        rel="noreferrer"
        data-testid="public-nav-github"
        className={navLinkClass}
      >
        GitHub
      </a>
    </div>
  );
}

/** The "Get Started" CTA into the BFF login flow. */
function GetStartedCta() {
  return (
    <a
      href={getStartedHref}
      data-testid="public-nav-get-started"
      className="bg-primary text-primary-foreground text-sm font-medium px-5 py-2 rounded-full hover:opacity-90 no-underline"
    >
      Get Started
    </a>
  );
}

/** Footer license notice + GitHub/Docs links. */
function FooterLinks() {
  return (
    <div className="flex items-center gap-6">
      <a
        href={repositoryUrl}
        target="_blank"
        rel="noreferrer"
        data-testid="public-footer-github"
        className="text-background hover:text-primary no-underline"
      >
        GitHub
      </a>
      <a
        href={docsUrl}
        target="_blank"
        rel="noreferrer"
        data-testid="public-footer-docs"
        className="text-background hover:text-primary no-underline"
      >
        Docs
      </a>
    </div>
  );
}

/** Footer inner row: license notice on the left, links on the right. */
function FooterContent() {
  return (
    <div className="max-w-6xl mx-auto flex items-center justify-between text-sm">
      <span>MIT Licensed</span>
      <FooterLinks />
    </div>
  );
}

export function PublicLayout(props: { children?: ReactNode }) {
  return (
    <div data-testid="public-layout" className="min-h-screen flex flex-col bg-background">
      <nav className="w-full px-6 py-4 flex items-center justify-between bg-background border-b border-border">
        <HomeLink />
        <NavLinks />
        <GetStartedCta />
      </nav>
      <main className="flex-1">{props.children}</main>
      <footer data-testid="public-footer" className="bg-foreground text-background px-6 py-8">
        <FooterContent />
      </footer>
    </div>
  );
}
