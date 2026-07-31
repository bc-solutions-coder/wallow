/**
 * Organization member list + management (Wallow-8w1h.4.4). Drives
 * `useQuery(organizationsGetMembersOptions(...))` and renders the members table
 * (rendering `organization-detail-members-table` /
 * `organization-detail-member-item`), a per-row remove button, and an add-member
 * form backed by the generated add/remove member mutations.
 *
 * Both writes sweep the members OPERATION rather than a key prefix — generated
 * keys are flat, so `queriesForOperation(organizationsGetMembersQueryKey(...))`
 * is what re-reads the list this component just changed.
 *
 * A failed members read replaces the TABLE region only (`organization-members-
 * error`): the heading and the add-member form are not query-backed, so they
 * stay reachable and the user can still act on the organization.
 *
 * Testids follow `{page}-{element}` kebab-case: `organization-members-loading`,
 * `organization-members-error` and `organization-members-empty` (query states),
 * `organization-member-userid` + `organization-member-add-submit` (add form),
 * `organization-member-remove` (per-row remove).
 */
import { AppForm, FormError, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import { useMutation, useQuery, useQueryClient, type QueryClient } from "@bc-solutions-coder/query";
import type { UserDto, WallowSdk } from "@bc-solutions-coder/sdk";
import {
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

import { errorText } from "@shared/lib/error-text";
import {
  organizationsAddMemberMutation,
  organizationsGetMembersOptions,
  organizationsGetMembersQueryKey,
  organizationsRemoveMemberMutation,
  queriesForOperation,
} from "../api";

/**
 * A single member row with a remove action. `ListRow` derives the row's test id
 * from `name` as `organization-detail-member-item`.
 */
function MemberRow(props: { member: UserDto; onRemove: (userId: string) => void }) {
  const { member, onRemove } = props;
  return (
    <ListRow name="organization-detail-member">
      <Text
        as="span"
        variant="bodySm"
        color="onCard"
        weight="medium"
        data-testid="organization-member-email"
      >
        {member.email}
      </Text>
      <Button
        type="button"
        variant="destructive"
        className="w-auto"
        data-testid="organization-member-remove"
        onClick={() => {
          onRemove(member.id);
        }}
      >
        Remove
      </Button>
    </ListRow>
  );
}

/**
 * The filter both writes invalidate through: every cached read of the members
 * operation, whichever organization it was called for.
 */
function membersOfOperation(client: WallowSdk["client"], orgId: string) {
  return queriesForOperation(organizationsGetMembersQueryKey({ client, path: { id: orgId } }));
}

export function MemberList(props: { orgId: string }) {
  const { orgId } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const { data, isPending, isError, error } = useQuery(
    organizationsGetMembersOptions({ client: sdk.client, path: { id: orgId } }),
  );
  const removeMember = useMutation({
    ...organizationsRemoveMemberMutation({ client: sdk.client }),
    onSuccess: (): void => {
      void queryClient.invalidateQueries(membersOfOperation(sdk.client, orgId));
    },
  });

  // No ui `Card` around the section: the table's own `ListCard` is the surface,
  // and wrapping it in a second one would nest card inside card.
  return (
    <div>
      <Text
        as="h2"
        variant="subheading"
        className="mb-4"
        data-testid="organization-members-heading"
      >
        Members
      </Text>

      <AddMemberForm client={sdk.client} queryClient={queryClient} orgId={orgId} />

      <MembersRegion
        members={data}
        isPending={isPending}
        isError={isError}
        error={error}
        onRemove={(userId) => {
          removeMember.mutate({ path: { id: orgId, userId } });
        }}
      />
    </div>
  );
}

/**
 * The query-backed half of the section — loading, errored, or the loaded table.
 * Its own component so the three states read as statements rather than as a
 * ternary chain wedged into the section's JSX.
 */
function MembersRegion(props: {
  members: readonly UserDto[] | undefined;
  isPending: boolean;
  isError: boolean;
  error: unknown;
  onRemove: (userId: string) => void;
}): ReactNode {
  const { members, isPending, isError, error, onRemove } = props;

  if (isPending) {
    return (
      <MutedText data-testid="organization-members-loading" className="text-center py-8">
        Loading members…
      </MutedText>
    );
  }

  // Only when there is no cached roster to fall back on: otherwise a failed
  // background refetch would replace a real membership list with a banner.
  if (isError && members === undefined) {
    return (
      <ErrorBanner data-testid="organization-members-error">
        {errorText(error, "Could not load the members.")}
      </ErrorBanner>
    );
  }

  return <MemberTable members={members ?? []} onRemove={onRemove} />;
}

/**
 * The required-user-id rule. It replaces the handler's silent
 * `if (userId.trim() === "") return;` — same trim, but the user now learns why
 * nothing happened.
 *
 * `.trim()` is what makes `"   "` fail the `min(1)`. It does NOT trim the
 * submitted value: TanStack's standard-schema adapter reads only the issue list
 * off a validation result and discards the parsed output, so `form.state.values`
 * stays raw — which is also what the pre-migration form posted, so the body
 * `MemberList.test.tsx` pins is unchanged.
 */
const addMemberSchema = z.object({
  userId: z.string().trim().min(1, "User ID is required"),
});

/**
 * Add-member form (migrated to `@bc-solutions-coder/forms` in Wallow-lrlm.5.5) —
 * one `useAppForm` call holding the zod schema, the GENERATED
 * `organizationsAddMemberMutation({ client })` and the success work, rendered
 * through the shared `AppForm` shell (see `CreateOrganizationForm`, the canonical
 * template).
 *
 * `organization-member-add-form`, `-add-submit` and `-add-error` all DERIVE from
 * the shell's `testIdPrefix`. The field does not: `userId` would derive
 * `organization-member-add-user-id`, but three closed specs (the oracle, the
 * restyle spec and the a11y spec) select the control as
 * `organization-member-userid` — so it carries an explicit `testId`, which the
 * catalog also suffixes for its message (`organization-member-userid-error`).
 *
 * `organization-member-add-error` is a NEW surface: before the migration a
 * rejected add — a bad id, a duplicate member, a 403 — was completely silent.
 */
function AddMemberForm(props: {
  client: WallowSdk["client"];
  queryClient: QueryClient;
  orgId: string;
}) {
  const { client, queryClient, orgId } = props;

  const form = useAppForm({
    schema: addMemberSchema,
    defaultValues: { userId: "" },
    // The generated factory goes over WHOLE — `useAppForm` infers its `TError`,
    // so nothing here has to be destructured or cast.
    mutation: organizationsAddMemberMutation({ client }),
    // The operation carries a path parameter, so the default `{ body: values }`
    // would post to the wrong URL. The body itself stays `{ userId }`.
    toVariables: (values) => ({ path: { id: orgId }, body: { userId: values.userId } }),
    onSuccess: () => {
      void queryClient.invalidateQueries(membersOfOperation(client, orgId));
      // TanStack's own `reset` (the form's values), NOT `form.wallow.reset` (the
      // mutation's result state): clearing the input is what the pre-migration
      // per-call `mutate` `onSuccess` did. Closing over `form` is safe —
      // `onSuccess` only ever runs after a render.
      form.reset();
    },
    fallbackError: "Could not add the member.",
  });

  return (
    // `flex items-end gap-3 mb-4`, not the shell's default `space-y-5` — this
    // form is one inline row, pinned by `MemberList.restyle.test.tsx`.
    <AppForm
      form={form}
      testIdPrefix="organization-member-add"
      className="flex items-end gap-3 mb-4"
    >
      <form.AppField name="userId">
        {(field) => <field.TextField label="User ID" testId="organization-member-userid" />}
      </form.AppField>

      {/* The shared Button is `w-full` by default; inline beside the input it
          sizes to its label instead of stretching. */}
      <SubmitButton className="w-auto">Add member</SubmitButton>

      <FormError />
    </AppForm>
  );
}

/** The loaded members table: empty state or the row list. */
function MemberTable(props: { members: readonly UserDto[]; onRemove: (userId: string) => void }) {
  const { members, onRemove } = props;

  if (members.length === 0) {
    // The one empty state in this app that carries neither a glyph nor
    // supporting copy: `EmptyState` omits a slot it is not given, so the card is
    // the sentence alone.
    return <EmptyState data-testid="organization-members-empty" message="No members yet." />;
  }

  return (
    <ListCard name="organization-detail-members">
      {members.map((member) => (
        <MemberRow key={member.id} member={member} onRemove={onRemove} />
      ))}
    </ListCard>
  );
}
