/**
 * The organizations the signed-in caller belongs to (Wallow-yp3e.7), with a way
 * to switch into one and a way to leave one. Drives
 * `useQuery(meGetOrganizationsOptions({ client }))` — ambient caller, no orgId
 * prop, same shape as `InvitationList`'s ambient tenant.
 * `MeController.GetOrganizations` asks for no permission and answers an
 * org-less token, so this is the one organizations screen every signed-in
 * member can reach regardless of role — and it is the organization picker:
 * there is none on the auth host.
 *
 * Switching is a full-document link built by the SDK's `loginRedirect` with the
 * `organization` hint: `/bff/login` re-authorizes silently against the SSO
 * cookie and the IdP scopes the new session to that organization. A link, not
 * a handler — `/bff/login` is a BFF endpoint outside the route tree, and the
 * SDK's imperative `login()` is banned for exactly that reason.
 *
 * Leaving is destructive and irreversible from a member's side, so it goes
 * through `AlertDialog` rather than a bare button, mirroring the catalog's own
 * `DeleteAlert` example. `AlertDialog.Close` always closes the popup on click
 * regardless of what its `onClick` does asynchronously, so a failed leave
 * cannot keep the dialog open to show its own error: the sole-owner refusal
 * (422 `Identity.LastOwner`) renders as a page-level `ErrorBanner` once the
 * popup has already closed, and the membership stays in the list — never a
 * silent failure.
 */
import { errorText } from "@bc-solutions-coder/forms";
import { useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
import { loginRedirect, type MyOrganizationDto } from "@bc-solutions-coder/sdk";
import {
  AlertDialog,
  Badge,
  EmptyState,
  ErrorBanner,
  ListCard,
  ListRow,
  MutedText,
  QuietLink,
  Text,
} from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";

import {
  meGetOrganizationsOptions,
  meGetOrganizationsQueryKey,
  organizationsLeaveMutation,
  queriesForOperation,
} from "../api";

/** Where a switch lands: the dashboard, whose gate re-reads the new session's user. */
const SWITCH_RETURN_TO = "/dashboard";

/**
 * The confirm/cancel footer, extracted so the popup body below stays under
 * `jsx-max-depth`. `onLeave` fires from the confirm `Close`'s `onClick`, which
 * Base UI runs before it unmounts the popup — the mutation is in flight the
 * instant the dialog is gone, not after.
 */
function LeaveOrganizationActions(props: { onLeave: () => void }) {
  const { onLeave } = props;
  return (
    <div className="mt-6 flex justify-end gap-2">
      <AlertDialog.Close data-testid="my-organization-leave-cancel">Cancel</AlertDialog.Close>
      <AlertDialog.Close
        data-testid="my-organization-leave-confirm"
        variant="destructive"
        onClick={onLeave}
      >
        Leave organization
      </AlertDialog.Close>
    </div>
  );
}

/** The alert's title, description and footer — its own component for the same reason. */
function LeaveOrganizationPopup(props: { org: MyOrganizationDto; onLeave: () => void }) {
  const { org, onLeave } = props;
  return (
    <AlertDialog.Popup>
      <AlertDialog.Title data-testid="my-organization-leave-title">
        Leave {org.name}?
      </AlertDialog.Title>
      <AlertDialog.Description data-testid="my-organization-leave-description">
        You will lose access to {org.name} and its resources. This cannot be undone.
      </AlertDialog.Description>
      <LeaveOrganizationActions onLeave={onLeave} />
    </AlertDialog.Popup>
  );
}

/** The per-row leave control: a trigger plus its confirmation alert. */
function LeaveOrganizationAlert(props: { org: MyOrganizationDto; onLeave: () => void }) {
  const { org, onLeave } = props;
  return (
    <AlertDialog.Root>
      <AlertDialog.Trigger data-testid="my-organization-leave">Leave</AlertDialog.Trigger>
      <AlertDialog.Portal>
        <AlertDialog.Backdrop />
        <LeaveOrganizationPopup org={org} onLeave={onLeave} />
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}

/** A single membership row (extracted to keep the list's JSX nesting shallow). */
function MyOrganizationRow(props: { org: MyOrganizationDto; onLeave: (orgId: string) => void }) {
  const { org, onLeave } = props;
  return (
    <ListRow name="my-organization">
      <Text
        as="span"
        variant="bodySm"
        color="onCard"
        weight="medium"
        data-testid="my-organization-item-name"
      >
        {org.name}
      </Text>
      {org.isOwner ? <Badge data-testid="my-organization-item-owner">Owner</Badge> : null}
      <QuietLink
        data-testid="my-organization-switch"
        href={loginRedirect(SWITCH_RETURN_TO, { organization: org.organizationId }).href}
      >
        Switch
      </QuietLink>
      <LeaveOrganizationAlert
        org={org}
        onLeave={() => {
          onLeave(org.organizationId);
        }}
      />
    </ListRow>
  );
}

/** The no-memberships card. */
function MyOrganizationsEmptyState() {
  return (
    <EmptyState
      data-testid="my-organizations-empty"
      icon="🏢"
      message="You don't belong to any organizations yet."
    />
  );
}

export function MyOrganizations() {
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const { data, isPending, isError, error } = useQuery(
    meGetOrganizationsOptions({ client: sdk.client }),
  );

  const leave = useMutation({
    ...organizationsLeaveMutation({ client: sdk.client }),
    onSuccess: (): void => {
      void queryClient.invalidateQueries(queriesForOperation(meGetOrganizationsQueryKey()));
    },
  });

  if (isPending) {
    return (
      <MutedText data-testid="my-organizations-loading" className="text-center py-12">
        Loading your organizations…
      </MutedText>
    );
  }

  // React Query retains the last resolved list across a failed background
  // refetch, so an error only takes over the screen when there is NO data to
  // fall back on — otherwise `data ?? []` would report a 500 as "none yet".
  if (isError && data === undefined) {
    return (
      <ErrorBanner data-testid="my-organizations-error">
        {errorText(error, "Could not load your organizations.")}
      </ErrorBanner>
    );
  }

  const orgs: readonly MyOrganizationDto[] = data ?? [];

  return (
    <div>
      {leave.error === undefined || leave.error === null ? null : (
        <ErrorBanner data-testid="my-organizations-leave-error" className="mb-4">
          {errorText(leave.error, "Could not leave the organization.")}
        </ErrorBanner>
      )}

      {orgs.length === 0 ? (
        <MyOrganizationsEmptyState />
      ) : (
        <ListCard name="my-organizations">
          {orgs.map((org) => (
            <MyOrganizationRow
              key={org.organizationId}
              org={org}
              onLeave={(orgId) => {
                leave.mutate({ path: { id: orgId } });
              }}
            />
          ))}
        </ListCard>
      )}
    </div>
  );
}
