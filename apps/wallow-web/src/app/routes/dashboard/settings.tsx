import { createFileRoute } from "@tanstack/react-router";

import { mfaGetStatusOptions, MfaSettingsSection } from "@features/mfa";
import { ProfileSection, usersGetCurrentUserOptions } from "@features/settings";

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
function SettingsPage() {
  return (
    <div data-testid="dashboard-settings" className="max-w-2xl mx-auto">
      <h1 data-testid="settings-heading" className="text-3xl font-bold text-foreground mb-8">
        Settings
      </h1>
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
