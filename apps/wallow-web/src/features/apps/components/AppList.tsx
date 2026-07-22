/**
 * Apps list component (Wallow-8w1h.5.2) — copies the CANONICAL Organizations
 * `OrganizationList` template (Wallow-8w1h.4.2). Drives
 * `useQuery(appsQueries.list())` and renders three states: loading, empty, and a
 * list of `app-item` rows.
 */
import { Card, MutedText } from "@bc-solutions-coder/ui";
import { useQuery } from "@tanstack/react-query";

import { appsQueries } from "../api";
import type { App } from "../types";

/** A single app row (extracted to keep the list's JSX nesting shallow). */
function AppRow({ app }: { app: App }) {
  return (
    <li data-testid="app-item">
      <span>{app.displayName}</span>
      <span>{app.clientType}</span>
    </li>
  );
}

export function AppList() {
  const { data, isPending } = useQuery(appsQueries.list());

  if (isPending) {
    return <MutedText data-testid="apps-loading">Loading apps…</MutedText>;
  }

  // The facade returns the list as `unknown`; narrow to the feature view-model
  // at the render boundary (the sanctioned pattern copied from OrganizationList).
  const apps = (data ?? []) as App[];

  if (apps.length === 0) {
    return <MutedText data-testid="apps-empty-state">No apps yet.</MutedText>;
  }

  return (
    <Card>
      <ul data-testid="apps-table">
        {apps.map((app) => (
          <AppRow key={app.clientId} app={app} />
        ))}
      </ul>
    </Card>
  );
}
