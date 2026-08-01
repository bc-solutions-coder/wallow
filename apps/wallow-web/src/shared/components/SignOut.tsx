import { navRowClassName } from "@bc-solutions-coder/navigation";
import { logout } from "@bc-solutions-coder/sdk";
// The per-component subpath, NOT the root barrel: the barrel also pulls in
// `FocusOnNavigate`, which imports `useRouterState`, and the specs around this
// component stub `@tanstack/react-router` down to `Link` alone. Bundlers
// tree-shake that away; a dev/test module graph does not, so the barrel would
// fail to link here.
import { ErrorBanner } from "@bc-solutions-coder/ui/error-banner";
import { LogOut } from "lucide-react";
import { useState } from "react";

/**
 * Sign Out — the dashboard's nav footer, handed to `AppShell`'s `footer` slot.
 *
 * It lives in the app rather than in `@bc-solutions-coder/navigation` because it
 * is the one nav row that talks to the backend: the package must stay free of an
 * `@bc-solutions-coder/sdk` edge, so what a fork inherits is the SLOT, not this
 * button.
 *
 * A `<button>` rather than a `Link`, since it calls the BFF logout and does not
 * route. That also means it gets none of the catalog's `NavigationMenu.Link` row
 * treatment, so it states the rail's rest/hover pair itself to sit flush with the
 * destinations above it; the geometry comes from the package's `navRowClassName`
 * so the two cannot drift.
 */

/** The rail's rest/hover colours, which no recipe supplies for a bare button. */
const signOutRowClass = "text-sidebar-foreground hover:bg-sidebar-accent";

const SIGN_OUT_LABEL = "Sign Out";

/**
 * @param showLabel The mode the slot was handed: `false` is the collapsed icon
 *   rail, where the label moves to `aria-label` rather than being clipped.
 */
export function SignOut(props: { showLabel: boolean }) {
  const [error, setError] = useState<string | null>(null);

  // `logout()` POSTs to the CSRF-gated `/bff/logout` and navigates on the
  // redirect it answers with, so it can reject (403 CSRF, 405) and leave the
  // session live. Saying so beats a silent no-op button and an unhandled
  // rejection.
  async function signOut(): Promise<void> {
    setError(null);
    try {
      await logout();
    } catch {
      setError("Sign out failed. You are still signed in — please try again.");
    }
  }

  return (
    <div className="px-4 py-4 border-t border-sidebar-accent">
      <button
        type="button"
        data-testid="dashboard-logout-link"
        aria-label={SIGN_OUT_LABEL}
        className={`${navRowClassName(props.showLabel)} ${signOutRowClass} w-full text-left`}
        onClick={() => {
          void signOut();
        }}
      >
        <LogOut aria-hidden="true" className="size-5 shrink-0" />
        {props.showLabel ? SIGN_OUT_LABEL : null}
      </button>
      {error === null ? null : (
        <ErrorBanner surface="sidebar" data-testid="dashboard-logout-error">
          {error}
        </ErrorBanner>
      )}
    </div>
  );
}
