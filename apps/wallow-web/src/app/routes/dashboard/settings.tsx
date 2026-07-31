import { PageHeader } from "@bc-solutions-coder/ui";
import { createFileRoute } from "@tanstack/react-router";

import { mfaGetStatusOptions, MfaSettingsSection } from "@features/mfa";
import { ProfileSection, usersGetCurrentUserOptions } from "@features/settings";
import { PAGE_CONTAINER } from "@shared/lib/page-container";

/**
 * Settings route (Wallow-8w1h.6.5) — composes the profile section and the MFA
 * status card into a single page under `data-testid="dashboard-settings"`.
 *
 * The route `loader` prefetches both queries via `ensureQueryData` so the
 * composed sections render content (not loading state) on first paint.
 *
 * Authored file-route style (`createFileRoute('/dashboard/settings')`), so its
 * `id`/`path`/parent are left unset — `src/router.tsx` binds it under the root
 * via `.update({ id, path, getParentRoute })` (there is no dashboard layout
 * route yet; that lands in Phase 7).
 */
/**
 * The title block is the catalog `PageHeader` (Wallow-lrlm.5.1) and the width is
 * the shared `PAGE_CONTAINER` rule — this page used to run narrower than the
 * list pages; F5.T1 collapses that split onto one container. The `mb-8` the
 * hand-rolled heading carried is the header row's own rhythm now.
 */
function SettingsPage() {
  return (
    <div data-testid="dashboard-settings" className={PAGE_CONTAINER}>
      <PageHeader data-testid="settings-header" title="Settings" />
      <ProfileSection />
      <MfaSettingsSection />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/settings")({
  loader: ({ context }) =>
    Promise.all([
      context.queryClient.ensureQueryData(
        usersGetCurrentUserOptions({ client: context.sdk.client }),
      ),
      context.queryClient.ensureQueryData(mfaGetStatusOptions({ client: context.sdk.client })),
    ]),
  component: SettingsPage,
});
