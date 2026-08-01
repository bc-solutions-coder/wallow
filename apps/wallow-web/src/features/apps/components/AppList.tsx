/**
 * Apps list component (Wallow-8w1h.5.2) — copies the CANONICAL Organizations
 * `OrganizationList` template (Wallow-8w1h.4.2). Drives
 * `useQuery(appsGetUserAppsOptions({ client }))` and renders four states:
 * loading, errored, empty, and a list of `app-item` rows.
 */
import { errorText } from "@bc-solutions-coder/forms";
import { useQuery } from "@bc-solutions-coder/query";
import type { DeveloperAppResponse } from "@bc-solutions-coder/sdk";
import {
  Badge,
  EmptyState,
  ErrorBanner,
  ListCard,
  ListRow,
  MutedText,
  Text,
} from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";

import { appsGetUserAppsOptions } from "../api";

/**
 * A single app row (extracted to keep the list's JSX nesting shallow).
 *
 * Unlike the organization and inquiry rows this one composes no `render` prop:
 * there is no app-detail route to navigate to, so the row stays `ListRow`'s
 * default element. The shipped `app-item` test id is derived from `name`.
 */
function AppRow({ app }: { app: DeveloperAppResponse }) {
  return (
    <ListRow name="app">
      <Text as="span" variant="bodySm" color="onCard" weight="medium" data-testid="app-item-name">
        {app.displayName}
      </Text>
      <Badge data-testid="app-item-type">{app.clientType}</Badge>
    </ListRow>
  );
}

/**
 * The no-apps card. It keeps the list's original sentence, "No apps yet.", as
 * the card heading rather than rewriting it — a restyle adds chrome, it never
 * drops a text node.
 */
function AppsEmptyState() {
  return (
    <EmptyState
      data-testid="apps-empty-state"
      icon="🐷"
      message="No apps yet."
      description="Nothing has been registered here. Get started by creating your first application."
    />
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

  // `ListCard`, not the ui `Card`: Card's fixed padding fights row cells that
  // have to bleed to the card edge. The shipped `apps-table` id is derived.
  return (
    <ListCard name="apps">
      {apps.map((app) => (
        <AppRow key={app.clientId} app={app} />
      ))}
    </ListCard>
  );
}
