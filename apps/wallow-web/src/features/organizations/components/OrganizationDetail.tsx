/**
 * Organization detail (Wallow-8w1h.4.4). Drives
 * `useQuery(organizationsQueries.detail(orgId))` and renders the org heading +
 * info (mirroring the Blazor oracle `organization-detail-heading` /
 * `organization-detail-back-link` / `organization-detail-not-found`),
 * archive/reactivate actions, and the `MemberList` for `orgId`.
 *
 * The back link is a plain anchor (not a router `Link`) so the component renders
 * standalone under a `QueryClientProvider` without a router context. The new
 * lifecycle actions carry `organization-detail-archive` /
 * `organization-detail-reactivate` (`{page}-{element}` kebab-case).
 */
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  archiveOrganizationMutation,
  organizationsQueries,
  reactivateOrganizationMutation,
} from "../api";
import type { Organization } from "../types";
import { MemberList } from "./MemberList";

export function OrganizationDetail(props: { orgId: string }) {
  const { orgId } = props;
  const queryClient = useQueryClient();
  const { data, isPending } = useQuery(organizationsQueries.detail(orgId));
  const archive = useMutation(archiveOrganizationMutation(queryClient, orgId));
  const reactivate = useMutation(reactivateOrganizationMutation(queryClient, orgId));

  if (isPending) {
    return <div data-testid="organization-detail-loading">Loading organization…</div>;
  }

  // The facade returns the detail as `unknown`; narrow to the feature view-model
  // at the render boundary. A missing org surfaces as `null`/`undefined`.
  const org = (data ?? null) as Organization | null;

  if (org === null) {
    return (
      <div>
        <a href="/dashboard/organizations" data-testid="organization-detail-back-link">
          Back to organizations
        </a>
        <div data-testid="organization-detail-not-found">Organization not found.</div>
      </div>
    );
  }

  return (
    <div>
      <a href="/dashboard/organizations" data-testid="organization-detail-back-link">
        Back to organizations
      </a>
      <h1 data-testid="organization-detail-heading">{org.name}</h1>

      <div>
        <button
          type="button"
          data-testid="organization-detail-archive"
          onClick={() => {
            archive.mutate();
          }}
        >
          Archive
        </button>
        <button
          type="button"
          data-testid="organization-detail-reactivate"
          onClick={() => {
            reactivate.mutate();
          }}
        >
          Reactivate
        </button>
      </div>

      <MemberList orgId={orgId} />
    </div>
  );
}
