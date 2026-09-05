/**
 * Connected applications card — the consent ledger. Lists the applications the
 * user has durably authorized (`meAuthorizationsListConnectedApplications`) and
 * lets them withdraw one, which revokes the authorization and every token
 * chained to it server-side.
 *
 * Withdraw sweeps the list OPERATION (`queriesForOperation`) built from the
 * same query key the read used, so the row disappears through a refetch rather
 * than an optimistic splice.
 *
 * Testids: connected-app-item / -name / -scopes / -withdraw per row, plus
 * connected-apps-loading / -error / -empty states.
 */
import { useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
import type { ConnectedApplicationDto, WallowSdk } from "@bc-solutions-coder/sdk";
import {
  Button,
  Card,
  CardTitle,
  EmptyState,
  FailureBanner,
  ListCard,
  ListRow,
  MutedText,
  Text,
} from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import type { ReactNode } from "react";

import {
  meAuthorizationsListConnectedApplicationsOptions,
  meAuthorizationsListConnectedApplicationsQueryKey,
  meAuthorizationsWithdrawConsentMutation,
  queriesForOperation,
} from "../api";

/** The filter withdraw invalidates through: every cached read of the list operation. */
function connectedAppsOfOperation(client: WallowSdk["client"]) {
  return queriesForOperation(meAuthorizationsListConnectedApplicationsQueryKey({ client }));
}

/** One connected application row: name, consented scopes, withdraw. */
function ConnectedAppRow(props: {
  app: ConnectedApplicationDto;
  onWithdraw: (authorizationId: string) => void;
}) {
  const { app, onWithdraw } = props;
  return (
    <ListRow name="connected-app">
      <Text
        as="span"
        variant="bodySm"
        color="onCard"
        weight="medium"
        data-testid="connected-app-name"
      >
        {app.displayName ?? app.clientId}
      </Text>
      <Text as="span" variant="bodySm" color="muted" data-testid="connected-app-scopes">
        {app.scopes.join(", ")}
      </Text>
      <Button
        type="button"
        variant="destructive"
        className="w-auto"
        data-testid="connected-app-withdraw"
        onClick={() => {
          onWithdraw(app.id);
        }}
      >
        Withdraw
      </Button>
    </ListRow>
  );
}

export function ConnectedAppsSection(): ReactNode {
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const { data, isPending, isError, error, refetch } = useQuery(
    meAuthorizationsListConnectedApplicationsOptions({ client: sdk.client }),
  );
  const withdraw = useMutation({
    ...meAuthorizationsWithdrawConsentMutation({ client: sdk.client }),
    onSuccess: (): void => {
      void queryClient.invalidateQueries(connectedAppsOfOperation(sdk.client));
    },
  });

  return (
    <Card>
      <CardTitle>Connected applications</CardTitle>
      <MutedText>
        Applications you have granted access to your account. Withdrawing one signs it out
        everywhere and it must ask for your consent again.
      </MutedText>
      <ConnectedAppsRegion
        apps={data}
        isPending={isPending}
        isError={isError}
        error={error}
        onRetry={refetch}
        onWithdraw={(authorizationId) => {
          withdraw.mutate({ path: { authorizationId } });
        }}
      />
    </Card>
  );
}

/**
 * The query-backed region — loading, errored, or the list. Its own component so
 * the three states read as statements rather than a ternary chain in the card.
 */
function ConnectedAppsRegion(props: {
  apps: readonly ConnectedApplicationDto[] | undefined;
  isPending: boolean;
  isError: boolean;
  error: unknown;
  onRetry: () => void;
  onWithdraw: (authorizationId: string) => void;
}): ReactNode {
  const { apps, isPending, isError, error, onRetry, onWithdraw } = props;

  if (isPending) {
    return (
      <MutedText data-testid="connected-apps-loading" className="text-center py-8">
        Loading connected applications…
      </MutedText>
    );
  }

  // Only when there is no cached list to fall back on: a failed background
  // refetch (e.g. the post-withdraw sweep) must not blank an already-rendered
  // list.
  if (isError && apps === undefined) {
    return <FailureBanner data-testid="connected-apps-error" error={error} onRetry={onRetry} />;
  }

  const connected = apps ?? [];
  if (connected.length === 0) {
    return <EmptyState data-testid="connected-apps-empty" message="No connected applications." />;
  }

  return (
    <ListCard name="connected-apps">
      {connected.map((app) => (
        <ConnectedAppRow key={app.id} app={app} onWithdraw={onWithdraw} />
      ))}
    </ListCard>
  );
}
