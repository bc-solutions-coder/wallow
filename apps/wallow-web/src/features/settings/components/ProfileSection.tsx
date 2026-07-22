/**
 * Settings profile section (Wallow-8w1h.6.2) — read-only profile card.
 *
 * Drives `useQuery(settingsQueries.profile())` and renders the current user's
 * name/email/roles, mirroring the Blazor oracle
 * `api/src/Wallow.Web/Components/Pages/Dashboard/Settings.razor` (which reads
 * name/email/roles off the authenticated principal, display-only, no mutation).
 * Testids mirror the C# page object `SettingsProfileSection`:
 *   settings-profile-name, settings-profile-email,
 *   settings-profile-roles (container) + settings-profile-role (per role) OR
 *   settings-profile-no-roles (mutually exclusive), plus a loading state.
 *
 * The facade returns the profile as `unknown`; narrow to the local view-model
 * at the render boundary (scout: `CurrentUserResponse`).
 */
import { Card, CardTitle, MutedText } from "@bc-solutions-coder/ui";
import { useQuery } from "@tanstack/react-query";

import { settingsQueries } from "../api";

interface ProfileView {
  id?: string | null;
  email?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  roles?: readonly string[] | null;
  permissions?: readonly string[] | null;
}

export function ProfileSection() {
  const { data, isPending } = useQuery(settingsQueries.profile());

  if (isPending) {
    return <MutedText data-testid="settings-profile-loading">Loading profile…</MutedText>;
  }

  const profile = (data ?? {}) as ProfileView;
  const name = [profile.firstName, profile.lastName].filter(Boolean).join(" ") || "Not set";
  const email = profile.email ?? "Not set";
  const roles = profile.roles ?? [];

  return (
    <Card>
      <CardTitle>Profile</CardTitle>
      <div data-testid="settings-profile-name">{name}</div>
      <div data-testid="settings-profile-email">{email}</div>

      {roles.length > 0 ? (
        <div data-testid="settings-profile-roles">
          {roles.map((role) => (
            <span key={role} data-testid="settings-profile-role">
              {role}
            </span>
          ))}
        </div>
      ) : (
        <MutedText data-testid="settings-profile-no-roles">No roles assigned.</MutedText>
      )}
    </Card>
  );
}
