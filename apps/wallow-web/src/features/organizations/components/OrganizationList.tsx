/**
 * Organizations list component (Wallow-8w1h.4.2) — the CANONICAL list-page
 * component every later vertical (Apps/Settings/MFA/Inquiries, Phases 4-6)
 * copies. It drives `useQuery(organizationsQueries.list())` and renders three
 * states: loading, empty, and a list of `organization-item` rows.
 */
import { Card, MutedText } from "@bc-solutions-coder/ui";
import { useQuery } from "@tanstack/react-query";

import { organizationsQueries } from "../api";
import type { Organization } from "../types";

/** A single organization row (extracted to keep the list's JSX nesting shallow). */
function OrganizationRow({ org }: { org: Organization }) {
  return (
    <li data-testid="organization-item">
      <span>{org.name}</span>
      {org.domain === null ? null : <span>{org.domain}</span>}
      <span>{org.memberCount}</span>
    </li>
  );
}

export function OrganizationList() {
  const { data, isPending } = useQuery(organizationsQueries.list());

  if (isPending) {
    return <MutedText data-testid="organizations-loading">Loading organizations…</MutedText>;
  }

  // The facade returns the list as `unknown`; narrow to the feature view-model
  // at the render boundary (the sanctioned pattern later verticals copy).
  const orgs = (data ?? []) as Organization[];

  if (orgs.length === 0) {
    return <MutedText data-testid="organizations-empty-state">No organizations yet.</MutedText>;
  }

  return (
    <Card>
      <ul data-testid="organizations-table">
        {orgs.map((org) => (
          <OrganizationRow key={org.id} org={org} />
        ))}
      </ul>
    </Card>
  );
}
