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
import { Badge, Card, CardTitle, ErrorBanner, MutedText, Text } from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import type { ReactNode } from "react";

import { errorText } from "@shared/lib/error-text";
import { usersGetCurrentUserOptions } from "../api";

/** A captioned read-only field row (extracted to keep the card's JSX shallow). */
function ProfileField(props: { label: string; children: ReactNode }) {
  return (
    <div>
      {/* `overline` IS the uppercase caption scale; only the layout stays local. */}
      <Text as="span" variant="overline" color="muted" className="block mb-1">
        {props.label}
      </Text>
      {props.children}
    </div>
  );
}

/**
 * The role chips (extracted so the roles field stays within the nesting budget).
 * They stay `neutral`: a role is an identity, not a state, so tinting one would
 * invent a judgement this card does not make.
 */
function RoleChips(props: { roles: readonly string[] }) {
  return (
    <div data-testid="settings-profile-roles" className="flex flex-wrap gap-2">
      {props.roles.map((role) => (
        <Badge key={role} data-testid="settings-profile-role">
          {role}
        </Badge>
      ))}
    </div>
  );
}

export function ProfileSection() {
  const { sdk } = useRouteContext({ from: "__root__" });
  const { data, isPending, isError, error } = useQuery(
    usersGetCurrentUserOptions({ client: sdk.client }),
  );

  if (isPending) {
    return (
      <MutedText data-testid="settings-profile-loading" className="text-center py-12">
        Loading profile…
      </MutedText>
    );
  }

  // Without this branch `data ?? {}` renders a complete-looking card of "Not
  // set" values, presenting a failed read as a fact about the account. A cached
  // profile still wins, so a failed background refetch keeps the real values.
  if (isError && data === undefined) {
    return (
      <ErrorBanner data-testid="settings-profile-error">
        {errorText(error, "Could not load your profile.")}
      </ErrorBanner>
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
        <Text as="div" variant="bodySm" data-testid="settings-profile-name">
          {name}
        </Text>
      </ProfileField>
      <ProfileField label="Email">
        <Text as="div" variant="bodySm" data-testid="settings-profile-email">
          {email}
        </Text>
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
