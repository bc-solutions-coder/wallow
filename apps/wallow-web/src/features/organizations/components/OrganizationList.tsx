/**
 * Organizations list component (Wallow-8w1h.4.2) — the CANONICAL list-page
 * component every later vertical (Apps/Settings/MFA/Inquiries, Phases 4-6)
 * copies. It drives `useQuery(organizationsQueries.list())` and renders three
 * states: loading, empty, and a list of `organization-item` rows.
 */
import { useQuery } from "@tanstack/react-query";

import { organizationsQueries } from "../api";
import type { Organization } from "../types";

export function OrganizationList() {
  const { data, isPending } = useQuery(organizationsQueries.list());

  if (isPending) {
    return <div data-testid="organizations-loading">Loading organizations…</div>;
  }

  // The facade returns the list as `unknown`; narrow to the feature view-model
  // at the render boundary (the sanctioned pattern later verticals copy).
  const orgs = (data ?? []) as Organization[];

  if (orgs.length === 0) {
    return <div data-testid="organizations-empty-state">No organizations yet.</div>;
  }

  return (
    <ul data-testid="organizations-table">
      {orgs.map((org) => (
        <li key={org.id} data-testid="organization-item">
          <span>{org.name}</span>
          {org.domain === null ? null : <span>{org.domain}</span>}
          <span>{org.memberCount}</span>
        </li>
      ))}
    </ul>
  );
}
