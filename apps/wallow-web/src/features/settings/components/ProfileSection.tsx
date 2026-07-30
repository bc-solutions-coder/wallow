/**
 * Settings profile section (Wallow-8w1h.6.2) — read-only profile card.
 *
 * Drives `useQuery(usersGetCurrentUserOptions(...))` and renders the current
 * user's name/email/roles off the authenticated principal, display-only, no
 * mutation. Testids mirror the C# page object `SettingsProfileSection`:
 *   settings-profile-name, settings-profile-email,
 *   settings-profile-roles (container) + settings-profile-role (per role) OR
 *   settings-profile-no-roles (mutually exclusive), plus a loading state.
 *
 * The generated read already resolves `CurrentUserResponse`, so there is no
 * local view-model to narrow to at the render boundary — every member is
 * optional in the OpenAPI document, which is what the "Not set" fallbacks below
 * cover.
 */
import { useQuery } from "@bc-solutions-coder/query";
import { Card, CardTitle, MutedText } from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import type { ReactNode } from "react";

import { usersGetCurrentUserOptions } from "../api";

/** The uppercase caption above each read-only value. */
const FIELD_LABEL = "block text-xs font-semibold text-foreground/70 uppercase tracking-wider mb-1";

/** A read-only field value. */
const FIELD_VALUE = "text-sm text-foreground";

/** The shared status/type pill from the dashboard recipe. */
const CHIP =
  "inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full";

/** A captioned read-only field row (extracted to keep the card's JSX shallow). */
function ProfileField(props: { label: string; children: ReactNode }) {
  return (
    <div>
      <span className={FIELD_LABEL}>{props.label}</span>
      {props.children}
    </div>
  );
}

/** The role chips (extracted so the roles field stays within the nesting budget). */
function RoleChips(props: { roles: readonly string[] }) {
  return (
    <div data-testid="settings-profile-roles" className="flex flex-wrap gap-2">
      {props.roles.map((role) => (
        <span key={role} data-testid="settings-profile-role" className={CHIP}>
          {role}
        </span>
      ))}
    </div>
  );
}

export function ProfileSection() {
  const { sdk } = useRouteContext({ from: "__root__" });
  const { data, isPending } = useQuery(usersGetCurrentUserOptions({ client: sdk.client }));

  if (isPending) {
    return (
      <MutedText data-testid="settings-profile-loading" className="text-center py-12">
        Loading profile…
      </MutedText>
    );
  }

  const profile = data ?? {};
  const name = [profile.firstName, profile.lastName].filter(Boolean).join(" ") || "Not set";
  const email = profile.email ?? "Not set";
  const roles = profile.roles ?? [];

  return (
    <Card>
      <CardTitle>Profile</CardTitle>
      <ProfileField label="Name">
        <div data-testid="settings-profile-name" className={FIELD_VALUE}>
          {name}
        </div>
      </ProfileField>
      <ProfileField label="Email">
        <div data-testid="settings-profile-email" className={FIELD_VALUE}>
          {email}
        </div>
      </ProfileField>
      <ProfileField label="Roles">
        {roles.length > 0 ? (
          <RoleChips roles={roles} />
        ) : (
          <MutedText data-testid="settings-profile-no-roles">No roles assigned.</MutedText>
        )}
      </ProfileField>
    </Card>
  );
}
