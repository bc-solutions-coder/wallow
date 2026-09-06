/**
 * Organization detail (Wallow-8w1h.4.4). Drives
 * `useQuery(organizationsGetByIdOptions(...))` and renders the org heading +
 * info (rendering `organization-detail-heading` /
 * `organization-detail-back-link` / `organization-detail-error`),
 * archive/reactivate actions, the `MemberList` and the `OrganizationClients`
 * ledgers for `orgId`.
 *
 * A missing org never reaches this component: the route loader turns the API's
 * 404 into the router's not-found path before the page renders. Archive and
 * reactivate carry no surface of their own — a refusal is the toast's to show.
 *
 * The back link is a plain anchor (not a router `Link`) so it needs no matched
 * route of its own; the SDK still comes off the router context, which every
 * mount site (app and spec alike) supplies. The lifecycle actions carry
 * `organization-detail-archive` / `organization-detail-reactivate`
 * (`{page}-{element}` kebab-case).
 */
import { useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
import { Button, ErrorBanner, FailureBanner, MutedText, Text } from "@bc-solutions-coder/ui";
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

export function OrganizationDetail(props: { orgId: string }) {
  const { orgId } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const { data, isPending, isError, error, refetch } = useQuery(
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

  // Only when there is nothing cached to show: a cached org survives a failed
  // background refetch, and the banner takes over only when it would otherwise
  // be an empty page.
  if (isError && data === undefined) {
    return (
      <div className="space-y-8">
        <BackLink />
        <FailureBanner data-testid="organization-detail-error" error={error} onRetry={refetch} />
      </div>
    );
  }

  const org = data;

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
