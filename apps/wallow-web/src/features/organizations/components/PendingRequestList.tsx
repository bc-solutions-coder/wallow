/**
 * Pending membership requests for an organization: renders the roster
 * (`organization-pending-requests-table` / `organization-pending-request-item`)
 * and a per-row approve/deny action backed by the generated mutations.
 *
 * `organizationsGetPendingMembersOptions` types its response `unknown` — the
 * backend's `[ProducesResponseType(404)]`-only attribute on `GetPendingMembers`
 * suppresses OpenAPI's automatic 200 inference, so the generated client has no
 * schema for the success body. {@link PendingMembershipDto} is a local mirror
 * of the API's `PendingMembershipDto` record (camelCase over the wire) and the
 * query result is cast to it rather than regenerating the SDK for this screen.
 *
 * Approve changes the ACTIVE roster too, so its `onSuccess` sweeps both the
 * pending operation and `organizationsGetMembersQueryKey`; deny sweeps pending
 * only. Both are plain `useMutation`s over the whole generated factory — no
 * `useAppForm`, since approve/deny are parameterless POSTs with no fields.
 *
 * A failed mutation renders `organization-pending-requests-error` alongside
 * the still-mounted list, same as the canonical `isError && data ===
 * undefined` query-error branch: a 422 doesn't blank the roster.
 */
import { errorText } from "@bc-solutions-coder/forms";
import { useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
import type { WallowSdk } from "@bc-solutions-coder/sdk";
import {
  Button,
  EmptyState,
  ErrorBanner,
  ListCard,
  ListRow,
  MutedText,
  Text,
} from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import type { ReactNode } from "react";

import {
  organizationsApproveMemberMutation,
  organizationsDenyMemberMutation,
  organizationsGetMembersQueryKey,
  organizationsGetPendingMembersOptions,
  organizationsGetPendingMembersQueryKey,
  queriesForOperation,
} from "../api";

/** The API's `PendingMembershipDto` — see the module doc for why this is local. */
interface PendingMembershipDto {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  requestedAt: string | null;
}

/**
 * The filter both mutations invalidate the pending list through: every cached
 * read of the pending operation, whichever organization it was called for.
 */
function pendingOfOperation(client: WallowSdk["client"], orgId: string) {
  return queriesForOperation(
    organizationsGetPendingMembersQueryKey({ client, path: { id: orgId } }),
  );
}

/** The filter approve additionally invalidates: the active members roster. */
function membersOfOperation(client: WallowSdk["client"], orgId: string) {
  return queriesForOperation(organizationsGetMembersQueryKey({ client, path: { id: orgId } }));
}

export function PendingRequestList(props: { orgId: string }) {
  const { orgId } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const { data, isPending, isError, error } = useQuery({
    ...organizationsGetPendingMembersOptions({ client: sdk.client, path: { id: orgId } }),
    select: (raw) => raw as readonly PendingMembershipDto[],
  });

  const approve = useMutation({
    ...organizationsApproveMemberMutation({ client: sdk.client }),
    onSuccess: (): void => {
      void queryClient.invalidateQueries(pendingOfOperation(sdk.client, orgId));
      void queryClient.invalidateQueries(membersOfOperation(sdk.client, orgId));
    },
  });
  const deny = useMutation({
    ...organizationsDenyMemberMutation({ client: sdk.client }),
    onSuccess: (): void => {
      void queryClient.invalidateQueries(pendingOfOperation(sdk.client, orgId));
    },
  });

  return (
    <div>
      <Text
        as="h2"
        variant="subheading"
        className="mb-4"
        data-testid="organization-pending-requests-heading"
      >
        Pending requests
      </Text>

      {/* PROTOTYPE (#168): approve/deny failures now toast via the global hook. */}

      <RequestsRegion
        requests={data}
        isPending={isPending}
        isError={isError}
        error={error}
        onApprove={(userId) => {
          approve.mutate({ path: { id: orgId, userId } });
        }}
        onDeny={(userId) => {
          deny.mutate({ path: { id: orgId, userId } });
        }}
      />
    </div>
  );
}

/**
 * The query-backed half of the section — loading, errored, or the loaded
 * table. Its own component so the three states read as statements rather than
 * as a ternary chain wedged into the section's JSX.
 */
function RequestsRegion(props: {
  requests: readonly PendingMembershipDto[] | undefined;
  isPending: boolean;
  isError: boolean;
  error: unknown;
  onApprove: (userId: string) => void;
  onDeny: (userId: string) => void;
}): ReactNode {
  const { requests, isPending, isError, error, onApprove, onDeny } = props;

  if (isPending) {
    return (
      <MutedText data-testid="organization-pending-requests-loading" className="text-center py-8">
        Loading pending requests…
      </MutedText>
    );
  }

  // Only when there is no cached roster to fall back on: otherwise a failed
  // background refetch would replace a real roster with a banner.
  if (isError && requests === undefined) {
    return (
      <ErrorBanner data-testid="organization-pending-requests-error">
        {errorText(error, "Could not load the pending requests.")}
      </ErrorBanner>
    );
  }

  return <RequestTable requests={requests ?? []} onApprove={onApprove} onDeny={onDeny} />;
}

/** The loaded pending-requests table: empty state or the row list. */
function RequestTable(props: {
  requests: readonly PendingMembershipDto[];
  onApprove: (userId: string) => void;
  onDeny: (userId: string) => void;
}) {
  const { requests, onApprove, onDeny } = props;

  if (requests.length === 0) {
    return (
      <EmptyState
        data-testid="organization-pending-requests-empty"
        message="No pending requests."
      />
    );
  }

  return (
    <ListCard name="organization-pending-requests">
      {requests.map((request) => (
        <RequestRow key={request.userId} request={request} onApprove={onApprove} onDeny={onDeny} />
      ))}
    </ListCard>
  );
}

/** A `Date` that renders "requested at an unknown time" rather than "Invalid Date". */
function requestedAtText(requestedAt: string | null): string {
  if (requestedAt === null) {
    return "Requested at an unknown time";
  }
  return `Requested ${new Date(requestedAt).toLocaleString()}`;
}

/**
 * A single pending-request row with approve/deny actions. `ListRow` derives
 * the row's test id from `name` as `organization-pending-request-item`.
 */
function RequestRow(props: {
  request: PendingMembershipDto;
  onApprove: (userId: string) => void;
  onDeny: (userId: string) => void;
}) {
  const { request, onApprove, onDeny } = props;
  return (
    <ListRow name="organization-pending-request">
      <div>
        <Text as="span" variant="bodySm" color="onCard" weight="medium">
          {request.firstName} {request.lastName}
        </Text>
        <MutedText className="block">{request.email}</MutedText>
        <MutedText className="block">{requestedAtText(request.requestedAt)}</MutedText>
      </div>
      <div className="flex gap-2">
        <Button
          type="button"
          className="w-auto"
          data-testid="organization-pending-request-approve"
          onClick={() => {
            onApprove(request.userId);
          }}
        >
          Approve
        </Button>
        <Button
          type="button"
          variant="destructive"
          className="w-auto"
          data-testid="organization-pending-request-deny"
          onClick={() => {
            onDeny(request.userId);
          }}
        >
          Deny
        </Button>
      </div>
    </ListRow>
  );
}
