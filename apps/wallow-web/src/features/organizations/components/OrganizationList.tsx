/**
 * Organizations list component (Wallow-8w1h.4.2) — the CANONICAL list-page
 * component every later vertical (Apps/Settings/MFA/Inquiries, Phases 4-6)
 * copies. It drives `useQuery(organizationsGetAllOptions({ client }))` and renders
 * four states: loading, errored, empty, and a list of `organization-item` rows.
 */
import { useQuery } from "@bc-solutions-coder/query";
import type { OrganizationDto } from "@bc-solutions-coder/sdk";
import {
  Badge,
  EmptyState,
  ErrorBanner,
  ListCard,
  ListRow,
  MutedText,
  Text,
} from "@bc-solutions-coder/ui";
import { Link, useRouteContext } from "@tanstack/react-router";

import { errorText } from "@shared/lib/error-text";
import { organizationsGetAllOptions } from "../api";

/**
 * A single organization row (extracted to keep the list's JSX nesting shallow).
 *
 * `ListRow`'s `render` SUBSTITUTES the `li` rather than wrapping it, so the row
 * IS the `Link` — the whole row navigates, and the shipped `organization-item`
 * test id (derived from `name`) rides along onto the anchor.
 */
function OrganizationRow({ org }: { org: OrganizationDto }) {
  return (
    <ListRow
      name="organization"
      render={<Link to="/dashboard/organizations/$orgId" params={{ orgId: org.id }} />}
    >
      <Text
        as="span"
        variant="bodySm"
        color="onCard"
        weight="medium"
        data-testid="organization-item-name"
      >
        {org.name}
      </Text>
      {org.domain === null ? null : (
        <Text
          as="span"
          variant="bodySm"
          color="muted"
          className="font-mono"
          data-testid="organization-item-domain"
        >
          {org.domain}
        </Text>
      )}
      <Badge data-testid="organization-item-members">{org.memberCount}</Badge>
    </ListRow>
  );
}

/**
 * The no-organizations card. It keeps the list's original sentence, "No
 * organizations yet.", as the card heading rather than rewriting it — a restyle
 * adds chrome, it never drops a text node.
 */
function OrganizationsEmptyState() {
  return (
    <EmptyState
      data-testid="organizations-empty-state"
      icon="🏢"
      message="No organizations yet."
      description="Nothing belongs here yet. Get started by creating your first organization."
    />
  );
}

export function OrganizationList() {
  const { sdk } = useRouteContext({ from: "__root__" });
  const { data, isPending, isError, error } = useQuery(
    organizationsGetAllOptions({ client: sdk.client }),
  );

  if (isPending) {
    return (
      <MutedText data-testid="organizations-loading" className="text-center py-12">
        Loading organizations…
      </MutedText>
    );
  }

  // React Query retains the last resolved list across a failed background
  // refetch, so an error only takes over the screen when there is NO data to
  // fall back on — otherwise `data ?? []` would report a 500 as "none yet".
  if (isError && data === undefined) {
    return (
      <ErrorBanner data-testid="organizations-error">
        {errorText(error, "Could not load organizations.")}
      </ErrorBanner>
    );
  }

  // No narrowing cast: the generated operation resolves the response BODY under
  // `responseStyle: "data"`, so `data` is already `OrganizationDto[]`.
  const orgs: readonly OrganizationDto[] = data ?? [];

  if (orgs.length === 0) {
    return <OrganizationsEmptyState />;
  }

  // `ListCard`, not the ui `Card`: Card's fixed padding fights row cells that
  // have to bleed to the card edge. `organizations-table` is derived from `name`.
  return (
    <ListCard name="organizations">
      {orgs.map((org) => (
        <OrganizationRow key={org.id} org={org} />
      ))}
    </ListCard>
  );
}
