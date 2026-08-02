/**
 * Member role management for an organization: renders the roster
 * (`member-roles-table` / `member-role-item`) with each active member's
 * roles, a per-role remove action, and a per-row assign-role form.
 *
 * OWN-ORG GATE: `UsersController.AssignRole`/`RemoveRole` write to the
 * CALLER'S AMBIENT tenant, ignoring the `{userId}`'s actual organization — so
 * an operator viewing a foreign org's roster who assigns or removes a role
 * would silently write it into their OWN organization instead, with the
 * backend returning 204 as though it worked. Until that is fixed server-side,
 * this screen refuses to render ANY mutation control unless
 * `organizationsGetAllOptions` — already scoped by the backend to the
 * caller's own memberships — lists `orgId` among them; otherwise it shows
 * `member-roles-foreign-org` and stays read-only.
 *
 * Both mutations sweep `organizationsGetMembersQueryKey` for `orgId`.
 * Removing a role is a plain top-level mutation (parameterless per click,
 * like `MemberList`'s remove); assigning one goes through `useAppForm` +
 * `AppForm` (`testIdPrefix="organization-member-role"`), ONE INSTANCE PER
 * ROW — so `organization-member-role-role-name` is not unique across a
 * multi-member roster, and a spec driving it needs a single-member fixture.
 */
import {
  AppForm,
  errorText,
  FormError,
  type SelectFieldOption,
  SubmitButton,
  useAppForm,
} from "@bc-solutions-coder/forms";
import { useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
import type { OrganizationDto, RoleResponse, UserDto, WallowSdk } from "@bc-solutions-coder/sdk";
import {
  Badge,
  Button,
  EmptyState,
  ErrorBanner,
  ListCard,
  ListRow,
  MutedText,
  Text,
} from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { z } from "zod";

import {
  organizationsGetAllOptions,
  organizationsGetMembersOptions,
  organizationsGetMembersQueryKey,
  queriesForOperation,
  rolesGetRolesOptions,
  usersAssignRoleMutation,
  usersRemoveRoleMutation,
} from "../api";

/**
 * Whether `orgId` is among the organizations `organizationsGetAllOptions`
 * returned for the signed-in operator — which the backend already scopes to
 * the caller's own memberships. See the module doc for why this gates every
 * mutation control on the page.
 */
function isCallersOwnOrg(orgs: readonly OrganizationDto[] | undefined, orgId: string): boolean {
  return (orgs ?? []).some((org) => org.id === orgId);
}

/** The roles catalog as `SelectField` options, dropping any nameless entry. */
function toRoleOptions(roles: readonly RoleResponse[] | undefined): readonly SelectFieldOption[] {
  return (roles ?? [])
    .map((role) => role.name)
    .filter((name): name is string => name !== null && name !== "")
    .map((name) => ({ value: name, label: name }));
}

/** The filter both mutations invalidate through: every cached read of the members operation. */
function membersOfOperation(client: WallowSdk["client"], orgId: string) {
  return queriesForOperation(organizationsGetMembersQueryKey({ client, path: { id: orgId } }));
}

const assignRoleSchema = z.object({
  roleName: z.string().trim().min(1, "Role is required"),
});

/**
 * One member row's assign-role form. Its own top-level component so its
 * `AppForm` tree starts at JSX depth 0, same trick `OrganizationDetail`'s
 * `RegisterClientForm` split uses to stay within `react/jsx-max-depth: 2`.
 */
function AssignRoleForm(props: {
  client: WallowSdk["client"];
  roleOptions: readonly SelectFieldOption[];
  userId: string;
  onAssigned: () => void;
}): ReactNode {
  const { client, roleOptions, userId, onAssigned } = props;

  const form = useAppForm({
    schema: assignRoleSchema,
    defaultValues: { roleName: "" },
    mutation: usersAssignRoleMutation({ client }),
    toVariables: (values) => ({
      path: { userId },
      body: { roleName: values.roleName },
    }),
    onSuccess: () => {
      onAssigned();
      form.reset();
    },
    fallbackError: "Could not assign the role.",
  });

  return (
    <AppForm form={form} testIdPrefix="organization-member-role" className="flex items-end gap-3">
      <form.AppField name="roleName">
        {(field) => (
          <field.SelectField label="Role" options={roleOptions} placeholder="Choose a role" />
        )}
      </form.AppField>

      <SubmitButton className="w-auto">Assign</SubmitButton>

      <FormError />
    </AppForm>
  );
}

/** One role badge, with an optional remove action (absent for a foreign org). */
function RoleChip(props: {
  roleName: string;
  onRemove: ((roleName: string) => void) | undefined;
}): ReactNode {
  const { roleName, onRemove } = props;
  return (
    <div className="inline-flex items-center gap-1">
      <Badge>{roleName}</Badge>
      {onRemove === undefined ? null : (
        <Button
          type="button"
          variant="destructive"
          className="w-auto"
          data-testid="member-role-remove"
          onClick={() => {
            onRemove(roleName);
          }}
        >
          Remove
        </Button>
      )}
    </div>
  );
}

/** A member's roles, or a note that they have none. */
function MemberRoleChips(props: {
  roles: readonly string[];
  onRemove: ((roleName: string) => void) | undefined;
}): ReactNode {
  const { roles, onRemove } = props;

  if (roles.length === 0) {
    return <MutedText className="block mt-1">No roles</MutedText>;
  }

  return (
    <div className="flex flex-wrap gap-2 mt-1">
      {roles.map((roleName) => (
        <RoleChip key={roleName} roleName={roleName} onRemove={onRemove} />
      ))}
    </div>
  );
}

/**
 * A single member row. `ListRow` derives the row's test id from `name` as
 * `member-role-item`. The assign form and the per-role remove action render
 * only when `isOwnOrg` — the own-org gate.
 */
function MemberRolesRow(props: {
  member: UserDto;
  isOwnOrg: boolean;
  roleOptions: readonly SelectFieldOption[];
  client: WallowSdk["client"];
  onAssigned: () => void;
  onRemove: (userId: string, roleName: string) => void;
}): ReactNode {
  const { member, isOwnOrg, roleOptions, client, onAssigned, onRemove } = props;

  return (
    <ListRow name="member-role">
      <div>
        <Text as="span" variant="bodySm" color="onCard" weight="medium">
          {member.firstName} {member.lastName}
        </Text>
        <MutedText className="block">{member.email}</MutedText>
        <MemberRoleChips
          roles={member.roles}
          onRemove={
            isOwnOrg
              ? (roleName) => {
                  onRemove(member.id, roleName);
                }
              : undefined
          }
        />
      </div>
      {isOwnOrg ? (
        <AssignRoleForm
          client={client}
          roleOptions={roleOptions}
          userId={member.id}
          onAssigned={onAssigned}
        />
      ) : null}
    </ListRow>
  );
}

/** The loaded roster: empty state or the row list. */
function MemberRolesTable(props: {
  members: readonly UserDto[];
  isOwnOrg: boolean;
  roleOptions: readonly SelectFieldOption[];
  client: WallowSdk["client"];
  onAssigned: () => void;
  onRemove: (userId: string, roleName: string) => void;
}): ReactNode {
  const { members, isOwnOrg, roleOptions, client, onAssigned, onRemove } = props;

  if (members.length === 0) {
    return <EmptyState data-testid="member-roles-empty" message="No members yet." />;
  }

  return (
    <ListCard name="member-roles">
      {members.map((member) => (
        <MemberRolesRow
          key={member.id}
          member={member}
          isOwnOrg={isOwnOrg}
          roleOptions={roleOptions}
          client={client}
          onAssigned={onAssigned}
          onRemove={onRemove}
        />
      ))}
    </ListCard>
  );
}

/**
 * The query-backed half of the section — loading, errored, or the loaded
 * table. Its own component so the three states read as statements rather
 * than as a ternary chain wedged into the section's JSX.
 */
function RosterRegion(props: {
  members: readonly UserDto[] | undefined;
  isPending: boolean;
  isError: boolean;
  error: unknown;
  isOwnOrg: boolean;
  roleOptions: readonly SelectFieldOption[];
  client: WallowSdk["client"];
  onAssigned: () => void;
  onRemove: (userId: string, roleName: string) => void;
}): ReactNode {
  const {
    members,
    isPending,
    isError,
    error,
    isOwnOrg,
    roleOptions,
    client,
    onAssigned,
    onRemove,
  } = props;

  if (isPending) {
    return (
      <MutedText data-testid="member-roles-loading" className="text-center py-8">
        Loading members…
      </MutedText>
    );
  }

  // Only when there is no cached roster to fall back on: otherwise a failed
  // background refetch would replace a real roster with a banner.
  if (isError && members === undefined) {
    return (
      <ErrorBanner data-testid="member-roles-error">
        {errorText(error, "Could not load the members.")}
      </ErrorBanner>
    );
  }

  return (
    <MemberRolesTable
      members={members ?? []}
      isOwnOrg={isOwnOrg}
      roleOptions={roleOptions}
      client={client}
      onAssigned={onAssigned}
      onRemove={onRemove}
    />
  );
}

export function MemberRoles(props: { orgId: string }): ReactNode {
  const { orgId } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();

  const orgsQuery = useQuery(organizationsGetAllOptions({ client: sdk.client }));
  const membersQuery = useQuery(
    organizationsGetMembersOptions({ client: sdk.client, path: { id: orgId } }),
  );
  const rolesQuery = useQuery(rolesGetRolesOptions({ client: sdk.client }));

  const isOwnOrg: boolean = isCallersOwnOrg(orgsQuery.data, orgId);
  const roleOptions: readonly SelectFieldOption[] = toRoleOptions(rolesQuery.data);

  const removeRole = useMutation({
    ...usersRemoveRoleMutation({ client: sdk.client }),
    onSuccess: (): void => {
      void queryClient.invalidateQueries(membersOfOperation(sdk.client, orgId));
    },
  });

  return (
    <div>
      <Text as="h2" variant="subheading" className="mb-4" data-testid="member-roles-heading">
        Member roles
      </Text>

      {!orgsQuery.isPending && !isOwnOrg ? (
        <MutedText data-testid="member-roles-foreign-org" className="block mb-4">
          You can only manage roles for your own organization.
        </MutedText>
      ) : null}

      <RosterRegion
        members={membersQuery.data}
        isPending={membersQuery.isPending}
        isError={membersQuery.isError}
        error={membersQuery.error}
        isOwnOrg={isOwnOrg}
        roleOptions={roleOptions}
        client={sdk.client}
        onAssigned={() => {
          void queryClient.invalidateQueries(membersOfOperation(sdk.client, orgId));
        }}
        onRemove={(userId, roleName) => {
          removeRole.mutate({ path: { userId, roleName } });
        }}
      />
    </div>
  );
}
