import { PageContainer, PageHeader } from "@bc-solutions-coder/ui";
import { createFileRoute } from "@tanstack/react-router";

import { mfaGetStatusOptions, MfaSettingsSection } from "@features/mfa";
import {
  ConnectedAppsSection,
  meAuthorizationsListConnectedApplicationsOptions,
  ProfileSection,
  usersGetCurrentUserOptions,
} from "@features/settings";

/**
 * Settings route (Wallow-8w1h.6.5) — composes the profile section, the MFA
 * status card and the connected-applications card into a single page under
 * `data-testid="dashboard-settings"`.
 *
 * The route `loader` prefetches each section's read via `ensureQueryData` so
 * the composed sections render content (not loading state) on first paint.
 *
 * Authored file-route style (`createFileRoute('/dashboard/settings')`), so its
 * `id`/`path`/parent are left unset — `src/router.tsx` binds it under the root
 * via `.update({ id, path, getParentRoute })` (there is no dashboard layout
 * route yet; that lands in Phase 7).
 */
/**
 * The title block is the catalog `PageHeader` and the width is the catalog
 * `PageContainer`, so the settings column matches the list pages. The `mb-8`
 * rhythm under the heading is the header row's own.
 */
function SettingsPage() {
  return (
    <PageContainer data-testid="dashboard-settings">
      <PageHeader data-testid="settings-header" title="Settings" />
      <ProfileSection />
      <MfaSettingsSection />
      <ConnectedAppsSection />
    </PageContainer>
  );
}

export const Route = createFileRoute("/dashboard/settings")({
  loader: ({ context }) =>
    Promise.all([
      context.queryClient.ensureQueryData(
        usersGetCurrentUserOptions({ client: context.sdk.client }),
      ),
      context.queryClient.ensureQueryData(mfaGetStatusOptions({ client: context.sdk.client })),
      context.queryClient.ensureQueryData(
        meAuthorizationsListConnectedApplicationsOptions({ client: context.sdk.client }),
      ),
    ]),
  component: SettingsPage,
});
