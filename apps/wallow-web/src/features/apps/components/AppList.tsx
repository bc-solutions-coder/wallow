/**
 * Apps list component (Wallow-8w1h.5.2) — copies the CANONICAL Organizations
 * `OrganizationList` template (Wallow-8w1h.4.2). Drives
 * `useQuery(appsGetUserAppsOptions({ client }))` and renders four states:
 * loading, errored, empty, and a list of `app-item` rows.
 */
import { useQuery } from "@bc-solutions-coder/query";
import type { DeveloperAppResponse } from "@bc-solutions-coder/sdk";
import { ErrorBanner, MutedText } from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";

import { errorText } from "@shared/lib/error-text";
import { appsGetUserAppsOptions } from "../api";

/** A single app row (extracted to keep the list's JSX nesting shallow). */
function AppRow({ app }: { app: DeveloperAppResponse }) {
  return (
    <li
      data-testid="app-item"
      className="flex items-center justify-between px-6 py-4 hover:bg-background/50"
    >
      <span data-testid="app-item-name" className="text-sm font-medium text-card-foreground">
        {app.displayName}
      </span>
      <span
        data-testid="app-item-type"
        className="inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full"
      >
        {app.clientType}
      </span>
    </li>
  );
}

/**
 * The no-apps card. It keeps the list's original sentence, "No apps yet.", as
 * the card heading rather than rewriting it — a restyle adds chrome, it never
 * drops a text node.
 */
function AppsEmptyState() {
  return (
    <div
      data-testid="apps-empty-state"
      className="bg-card rounded-lg shadow-sm border border-border p-12 text-center"
    >
      <div className="text-[80px] leading-none mb-4">🐷</div>
      <h2 className="text-xl font-semibold text-foreground mb-2">No apps yet.</h2>
      <p className="text-foreground/60">
        Nothing has been registered here. Get started by creating your first application.
      </p>
    </div>
  );
}

export function AppList() {
  const { sdk } = useRouteContext({ from: "__root__" });
  const { data, isPending, isError, error } = useQuery(
    appsGetUserAppsOptions({ client: sdk.client }),
  );

  if (isPending) {
    return (
      <MutedText data-testid="apps-loading" className="text-center py-12">
        Loading apps…
      </MutedText>
    );
  }

  // As in `OrganizationList`: the error only takes over when there is no cached
  // list to fall back on, so a failed background refetch keeps the rows up.
  if (isError && data === undefined) {
    return (
      <ErrorBanner data-testid="apps-error">{errorText(error, "Could not load apps.")}</ErrorBanner>
    );
  }

  // No narrowing cast: under `responseStyle: "data"` the generated operation
  // resolves the body, so `data` is already `DeveloperAppResponse[]`.
  const apps: readonly DeveloperAppResponse[] = data ?? [];

  if (apps.length === 0) {
    return <AppsEmptyState />;
  }

  // A raw div, not the ui `Card`: Card's fixed `p-6 space-y-6` fights the
  // recipe's `px-6 py-4` row cells, which need to bleed to the card edge.
  return (
    <div className="bg-card rounded-lg shadow-sm border border-border overflow-hidden">
      <ul data-testid="apps-table" className="divide-y divide-border">
        {apps.map((app) => (
          <AppRow key={app.clientId} app={app} />
        ))}
      </ul>
    </div>
  );
}
