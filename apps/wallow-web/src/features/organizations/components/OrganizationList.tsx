/**
 * Organizations list component (Wallow-8w1h.4.2) — the CANONICAL list-page
 * component every later vertical (Apps/Settings/MFA/Inquiries, Phases 4-6)
 * copies. It drives `useQuery(organizationsGetAllOptions({ client }))` and renders
 * three states: loading, empty, and a list of `organization-item` rows.
 */
import type { OrganizationDto } from "@bc-solutions-coder/sdk";
import { MutedText } from "@bc-solutions-coder/ui";
import { useQuery } from "@tanstack/react-query";
import { useRouteContext } from "@tanstack/react-router";

import { organizationsGetAllOptions } from "../api";

/** A single organization row (extracted to keep the list's JSX nesting shallow). */
function OrganizationRow({ org }: { org: OrganizationDto }) {
  return (
    <li
      data-testid="organization-item"
      className="flex items-center justify-between px-6 py-4 hover:bg-background/50"
    >
      <span
        data-testid="organization-item-name"
        className="text-sm font-medium text-card-foreground"
      >
        {org.name}
      </span>
      {org.domain === null ? null : (
        <span
          data-testid="organization-item-domain"
          className="text-sm text-foreground/70 font-mono"
        >
          {org.domain}
        </span>
      )}
      <span
        data-testid="organization-item-members"
        className="inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full"
      >
        {org.memberCount}
      </span>
    </li>
  );
}

/**
 * The no-organizations card. It keeps the list's original sentence, "No
 * organizations yet.", as the card heading rather than rewriting it — a restyle
 * adds chrome, it never drops a text node.
 */
function OrganizationsEmptyState() {
  return (
    <div
      data-testid="organizations-empty-state"
      className="bg-card rounded-lg shadow-sm border border-border p-12 text-center"
    >
      <div className="text-[80px] leading-none mb-4">🏢</div>
      <h2 className="text-xl font-semibold text-foreground mb-2">No organizations yet.</h2>
      <p className="text-foreground/60">
        Nothing belongs here yet. Get started by creating your first organization.
      </p>
    </div>
  );
}

export function OrganizationList() {
  const { sdk } = useRouteContext({ from: "__root__" });
  const { data, isPending } = useQuery(organizationsGetAllOptions({ client: sdk.client }));

  if (isPending) {
    return (
      <MutedText data-testid="organizations-loading" className="text-center py-12">
        Loading organizations…
      </MutedText>
    );
  }

  // No narrowing cast: the generated operation resolves the response BODY under
  // `responseStyle: "data"`, so `data` is already `OrganizationDto[]`.
  const orgs: readonly OrganizationDto[] = data ?? [];

  if (orgs.length === 0) {
    return <OrganizationsEmptyState />;
  }

  // A raw div, not the ui `Card`: Card's fixed `p-6 space-y-6` fights the
  // recipe's `px-6 py-4` row cells, which need to bleed to the card edge.
  return (
    <div className="bg-card rounded-lg shadow-sm border border-border overflow-hidden">
      <ul data-testid="organizations-table" className="divide-y divide-border">
        {orgs.map((org) => (
          <OrganizationRow key={org.id} org={org} />
        ))}
      </ul>
    </div>
  );
}
