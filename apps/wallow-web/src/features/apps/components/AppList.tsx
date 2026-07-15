/**
 * Apps list component (Wallow-8w1h.5.2) — copies the CANONICAL Organizations
 * `OrganizationList` template (Wallow-8w1h.4.2). Drives
 * `useQuery(appsQueries.list())` and renders three states: loading, empty, and a
 * list of `app-item` rows.
 */
import { useQuery } from "@tanstack/react-query";

import { appsQueries } from "../api";
import type { App } from "../types";

export function AppList() {
  const { data, isPending } = useQuery(appsQueries.list());

  if (isPending) {
    return <div data-testid="apps-loading">Loading apps…</div>;
  }

  // The facade returns the list as `unknown`; narrow to the feature view-model
  // at the render boundary (the sanctioned pattern copied from OrganizationList).
  const apps = (data ?? []) as App[];

  if (apps.length === 0) {
    return <div data-testid="apps-empty-state">No apps yet.</div>;
  }

  return (
    <ul data-testid="apps-table">
      {apps.map((app) => (
        <li key={app.clientId} data-testid="app-item">
          <span>{app.displayName}</span>
          <span>{app.clientType}</span>
        </li>
      ))}
    </ul>
  );
}
