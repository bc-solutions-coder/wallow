/**
 * Organization detail (Wallow-8w1h.4.4). Drives
 * `useQuery(organizationsGetByIdOptions(...))` and renders the org heading +
 * info (rendering `organization-detail-heading` /
 * `organization-detail-back-link` / `organization-detail-not-found` /
 * `organization-detail-error`), archive/reactivate actions, the `MemberList`
 * and the `OrganizationClients` ledgers for `orgId`.
 *
 * The back link is a plain anchor (not a router `Link`) so it needs no matched
 * route of its own; the SDK still comes off the router context, which every
 * mount site (app and spec alike) supplies. The lifecycle actions carry
 * `organization-detail-archive` / `organization-detail-reactivate`
 * (`{page}-{element}` kebab-case).
 */
import { errorText } from "@bc-solutions-coder/forms";
import { useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
import { Button, EmptyState, ErrorBanner, MutedText, Text } from "@bc-solutions-coder/ui";
import { Link, useRouteContext } from "@tanstack/react-router";

import {
  organizationsArchiveMutation,
  organizationsGetByIdOptions,
  organizationsReactivateMutation,
  queriesWithTag,
} from "../api";
import { DeleteOrganizationDialog } from "./DeleteOrganization";
import { MemberList } from "./MemberList";
import { OrganizationClients } from "./OrganizationClients";
import { OrganizationPlatformControls } from "./PlatformSuspension";

/**
 * Links to the two `$orgId`-scoped screens: pending requests and member roles.
 * Neither reaches the global rail (it has no `orgId` to fill into a
 * destination), so this page is their only way in.
 */
function ManageLinks(props: { orgId: string }) {
  const { orgId } = props;
  return (
    <div className="flex gap-3">
      <Button
        render={<Link to="/dashboard/organizations/$orgId/requests" params={{ orgId }} />}
        nativeButton={false}
        variant="secondary"
        className="w-auto no-underline"
        data-testid="organization-detail-requests-link"
      >
        Pending requests
      </Button>
      <Button
        render={<Link to="/dashboard/organizations/$orgId/members" params={{ orgId }} />}
        nativeButton={false}
        variant="secondary"
        className="w-auto no-underline"
        data-testid="organization-detail-members-link"
      >
        Manage roles
      </Button>
    </div>
  );
}

/**
 * A plain anchor, not a router `Link`, so the component renders standalone under
 * a `QueryClientProvider` without a router context.
 */
function BackLink() {
  return (
    <a
      href="/dashboard/organizations"
      data-testid="organization-detail-back-link"
      className="text-sm text-primary hover:opacity-80 no-underline inline-block"
    >
      Back to organizations
    </a>
  );
}

/**
 * The missing-org card. It keeps the original sentence, "Organization not
 * found.", as the card heading rather than rewriting it.
 *
 * A not-found branch rather than a list's empty branch, but the same card: it
 * hand-rolled the identical centered surface the empty states did, so it belongs
 * to `EmptyState` for the same reason they do. No icon — this card never carried
 * one, and `EmptyState` omits the slot it is not given.
 */
function NotFoundCard() {
  return (
    <EmptyState
      data-testid="organization-detail-not-found"
      message="Organization not found."
      description="It may have been archived, or the link may point somewhere that no longer exists."
    />
  );
}

export function OrganizationDetail(props: { orgId: string }) {
  const { orgId } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const { data, isPending, isError, error } = useQuery(
    organizationsGetByIdOptions({ client: sdk.client, path: { id: orgId } }),
  );
  // Archive and reactivate each flip a field the LIST also renders, so both
  // sweep the whole Organizations tag rather than just this org's detail.
  const invalidateOrganizations = (): void => {
    void queryClient.invalidateQueries(queriesWithTag("Organizations"));
  };
  const archive = useMutation({
    ...organizationsArchiveMutation({ client: sdk.client }),
    onSuccess: invalidateOrganizations,
  });
  const reactivate = useMutation({
    ...organizationsReactivateMutation({ client: sdk.client }),
    onSuccess: invalidateOrganizations,
  });

  if (isPending) {
    return <MutedText data-testid="organization-detail-loading">Loading organization…</MutedText>;
  }

  // The split `InquiryDetail` already ships: an errored read reaches `org ===
  // null` just as a resolved-empty one does, so without this branch a genuine
  // 500 claims the organization does not exist. `data === undefined` is what
  // separates them — a resolved-null body still means "not found", and a cached
  // org survives a failed background refetch.
  if (isError && data === undefined) {
    return (
      <div className="space-y-8">
        <BackLink />
        <ErrorBanner data-testid="organization-detail-error">
          {errorText(error, "Could not load the organization.")}
        </ErrorBanner>
      </div>
    );
  }

  const org = data ?? null;

  if (org === null) {
    return (
      <div className="space-y-8">
        <BackLink />
        <NotFoundCard />
      </div>
    );
  }

  // A plain column, not one giant card: each section below owns its own card
  // surface, so wrapping the page in a `Card` would nest card inside card.
  return (
    <div className="space-y-8">
      <BackLink />
      <Text as="h1" variant="title" data-testid="organization-detail-heading">
        {org.name}
      </Text>

      {typeof org.platformSuspendedAt === "string" ? (
        <ErrorBanner data-testid="organization-detail-platform-suspension">
          Suspended by the platform: {org.platformSuspensionReason}
        </ErrorBanner>
      ) : null}

      <div className="flex gap-3">
        <Button
          type="button"
          variant="destructive"
          className="w-auto"
          data-testid="organization-detail-archive"
          onClick={() => {
            archive.mutate({ path: { id: orgId } });
          }}
        >
          Archive
        </Button>
        <Button
          type="button"
          variant="secondary"
          className="w-auto"
          data-testid="organization-detail-reactivate"
          onClick={() => {
            reactivate.mutate({ path: { id: orgId } });
          }}
        >
          Reactivate
        </Button>
        <OrganizationPlatformControls
          orgId={orgId}
          suspended={typeof org.platformSuspendedAt === "string"}
        />
        <DeleteOrganizationDialog orgId={orgId} orgName={org.name} />
      </div>

      <ManageLinks orgId={orgId} />

      <MemberList orgId={orgId} />

      <OrganizationClients orgId={orgId} />
    </div>
  );
}
