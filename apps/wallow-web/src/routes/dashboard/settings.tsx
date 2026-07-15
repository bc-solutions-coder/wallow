import { createFileRoute } from "@tanstack/react-router";

import { mfaQueries } from "../../features/mfa/api";
import { MfaSettingsSection } from "../../features/mfa/components/MfaSettingsSection";
import { settingsQueries } from "../../features/settings/api";
import { ProfileSection } from "../../features/settings/components/ProfileSection";

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
    <div data-testid="dashboard-settings">
      <ProfileSection />
      <MfaSettingsSection />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/settings")({
  loader: ({ context }) =>
    Promise.all([
      context.queryClient.ensureQueryData(settingsQueries.profile()),
      context.queryClient.ensureQueryData(mfaQueries.status()),
    ]),
  component: SettingsPage,
});
