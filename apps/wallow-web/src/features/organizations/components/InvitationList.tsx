/**
 * Outstanding invitations list + revoke (Wallow-yp3e.3.3). Drives
 * `useQuery(invitationsGetByTenantOptions(...))` against the endpoint's AMBIENT
 * tenant — it takes no organization id and cannot be scoped to one, so neither
 * this component nor the route that mounts it ever passes an `orgId`.
 *
 * "Outstanding" means the invitation's status is still Pending: the endpoint
 * returns every invitation regardless of status, so the roster is filtered
 * client-side down to Pending before it renders.
 *
 * Revoke sweeps the invitations OPERATION (`queriesForOperation`) built from
 * the SAME query object the list read used — a different one matches nothing.
 *
 * The error branch is `isError && data === undefined` so a failed background
 * refetch (e.g. after a revoke) does not blank an already-rendered list.
 */
import { useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
import type { InvitationResponse, WallowSdk } from "@bc-solutions-coder/sdk";
import {
  Button,
  EmptyState,
  ErrorBanner,
  ListCard,
  ListRow,
  MutedText,
  Text,
} from "@bc-solutions-coder/ui";
import { errorText } from "@bc-solutions-coder/forms";
import { formatLongDate } from "@bc-solutions-coder/utils/format";
import { useRouteContext } from "@tanstack/react-router";
import type { ReactNode } from "react";

import {
  invitationsGetByTenantOptions,
  invitationsGetByTenantQueryKey,
  invitationsRevokeMutation,
  queriesForOperation,
} from "../api";

/**
 * Explicit so the query key carries it (the DESIGN's own instruction): the
 * endpoint is paged, and a fixed page with no pager is acceptable for the
 * first cut, but the query object must be spelled out rather than omitted.
 */
export const INVITATIONS_QUERY = { skip: 0, take: 50 };

/**
 * The filter revoke invalidates through: every cached read of the invitations
 * operation for this same query object.
 */
function invitationsOfOperation(client: WallowSdk["client"]) {
  return queriesForOperation(invitationsGetByTenantQueryKey({ client, query: INVITATIONS_QUERY }));
}

/** A single invitation row with a revoke action. `ListRow` derives `invitation-item`. */
function InvitationRow(props: { invitation: InvitationResponse; onRevoke: (id: string) => void }) {
  const { invitation, onRevoke } = props;
  return (
    <ListRow name="invitation">
      <Text
        as="span"
        variant="bodySm"
        color="onCard"
        weight="medium"
        data-testid="invitation-email"
      >
        {invitation.email}
      </Text>
      <Text as="span" variant="bodySm" color="muted" data-testid="invitation-status">
        {invitation.status}
      </Text>
      <Text as="span" variant="bodySm" color="muted" data-testid="invitation-expires">
        {formatLongDate(invitation.expiresAt)}
      </Text>
      <Button
        type="button"
        variant="destructive"
        className="w-auto"
        data-testid="invitation-revoke"
        onClick={() => {
          onRevoke(invitation.id);
        }}
      >
        Revoke
      </Button>
    </ListRow>
  );
}

export function InvitationList(): ReactNode {
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const { data, isPending, isError, error } = useQuery(
    invitationsGetByTenantOptions({ client: sdk.client, query: INVITATIONS_QUERY }),
  );
  const revoke = useMutation({
    ...invitationsRevokeMutation({ client: sdk.client }),
    onSuccess: (): void => {
      void queryClient.invalidateQueries(invitationsOfOperation(sdk.client));
    },
  });

  return (
    <InvitationsRegion
      invitations={data}
      isPending={isPending}
      isError={isError}
      error={error}
      onRevoke={(id) => {
        revoke.mutate({ path: { id } });
      }}
    />
  );
}

/**
 * The query-backed section — loading, errored, or the filtered table. Its own
 * component so the three states read as statements rather than as a ternary
 * chain wedged into the screen's JSX.
 */
function InvitationsRegion(props: {
  invitations: readonly InvitationResponse[] | undefined;
  isPending: boolean;
  isError: boolean;
  error: unknown;
  onRevoke: (id: string) => void;
}): ReactNode {
  const { invitations, isPending, isError, error, onRevoke } = props;

  if (isPending) {
    return (
      <MutedText data-testid="invitations-loading" className="text-center py-8">
        Loading invitations…
      </MutedText>
    );
  }

  // Only when there is no cached roster to fall back on: otherwise a failed
  // background refetch (e.g. the post-revoke sweep) would replace a real list
  // with a banner.
  if (isError && invitations === undefined) {
    return (
      <ErrorBanner data-testid="invitations-error">
        {errorText(error, "Could not load the invitations.")}
      </ErrorBanner>
    );
  }

  const outstanding = (invitations ?? []).filter((invitation) => invitation.status === "Pending");

  return <InvitationTable invitations={outstanding} onRevoke={onRevoke} />;
}

/** The loaded, already-filtered invitations table: empty state or the row list. */
function InvitationTable(props: {
  invitations: readonly InvitationResponse[];
  onRevoke: (id: string) => void;
}) {
  const { invitations, onRevoke } = props;

  if (invitations.length === 0) {
    return <EmptyState data-testid="invitations-empty" message="No outstanding invitations." />;
  }

  return (
    <ListCard name="invitations">
      {invitations.map((invitation) => (
        <InvitationRow key={invitation.id} invitation={invitation} onRevoke={onRevoke} />
      ))}
    </ListCard>
  );
}
