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
import { asString } from "@bc-solutions-coder/utils/guards";
import { AppForm, FormError, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import { useMutation, useQuery, useQueryClient, type QueryClient } from "@bc-solutions-coder/query";
import type { UserDto, WallowSdk } from "@bc-solutions-coder/sdk";
import {
  Autocomplete,
  Button,
  EmptyState,
  FailureBanner,
  Field,
  ListCard,
  ListRow,
  MutedText,
  Text,
} from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { z } from "zod";

import {
  organizationsAddMemberMutation,
  organizationsGetMembersOptions,
  organizationsGetMembersQueryKey,
  organizationsRemoveMemberMutation,
  queriesForOperation,
} from "../api";
import { useUserPicker } from "../hooks/use-user-picker";

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
  const { data, isPending, isError, error, refetch } = useQuery(
    organizationsGetMembersOptions({ client: sdk.client, path: { id: orgId } }),
  );
  // A refused removal is the toast's to show; the table carries no surface for it.
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
        onRetry={refetch}
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
  onRetry: () => void;
  onRemove: (userId: string) => void;
}): ReactNode {
  const { members, isPending, isError, error, onRetry, onRemove } = props;

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
      <FailureBanner data-testid="organization-members-error" error={error} onRetry={onRetry} />
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
 * `organization-member-userid` — so {@link UserIdPicker} spells that id (and its
 * `-error` suffix) out literally, exactly as the catalog's `testId` override did.
 *
 * `organization-member-add-error` is a NEW surface: before the migration a
 * rejected add — a bad id, a duplicate member, a 403 — was completely silent.
 *
 * The control itself stopped being a text box in Wallow-lrlm.6.1: it is now a
 * {@link UserIdPicker} searching the users operation by EMAIL, so nobody has to
 * know a uuid by heart.
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
    // would post to the wrong URL. `role` names what this organization grants the
    // member; the endpoint has no default, and this screen grants the baseline
    // `user` role — choosing a role belongs to the member-role management screen.
    toVariables: (values) => ({
      path: { id: orgId },
      body: { userId: values.userId, role: "user" },
    }),
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
        {(field) => (
          <UserIdPicker
            value={field.state.value}
            error={fieldErrorMessage(field.state.meta.errors)}
            disabled={form.wallow.pending}
            onChange={(next) => {
              field.handleChange(next);
            }}
            onBlur={field.handleBlur}
          />
        )}
      </form.AppField>

      {/* The shared Button is `w-full` by default; inline beside the input it
          sizes to its label instead of stretching. */}
      <SubmitButton className="w-auto">Add member</SubmitButton>

      <FormError />
    </AppForm>
  );
}

/**
 * The message TanStack Form currently publishes for a field, as a string.
 *
 * A local copy of what `useCatalogField` does for the five catalog fields:
 * `firstErrorMessage` is INTERNAL to `@bc-solutions-coder/forms` (its barrel spec
 * asserts it absent), and this control is built on the `AppField` render-prop
 * escape hatch rather than on a catalog field, so it cannot borrow the helper.
 * Same shape either way — a zod issue object, or a bare string from the server
 * error split.
 */
function fieldErrorMessage(errors: readonly unknown[]): string | undefined {
  const first: unknown = errors[0];

  if (typeof first === "string") {
    return first;
  }
  if (typeof first !== "object" || first === null || !("message" in first)) {
    return undefined;
  }

  return asString(first.message);
}

/**
 * One suggestion. The row shows the person's EMAIL — the whole point of the
 * picker — while `itemToStringValue` on the root decides that what the input
 * COMMITS when the row is pressed is their id.
 */
function UserOption(props: { user: UserDto }): ReactNode {
  const { user } = props;
  return (
    <Autocomplete.Item value={user} data-testid="organization-member-userid-option">
      {user.email}
    </Autocomplete.Item>
  );
}

/** The listbox: one row per person still matching what was typed. */
function UserOptionRows(): ReactNode {
  return (
    <Autocomplete.List>
      {(user: UserDto) => <UserOption key={user.id} user={user} />}
    </Autocomplete.List>
  );
}

/** The popup card the rows sit on. */
function UserOptionCard(): ReactNode {
  return (
    <Autocomplete.Popup>
      <UserOptionRows />
    </Autocomplete.Popup>
  );
}

/**
 * The portalled half of the picker. Nothing below this is in the DOM while the
 * list is closed, so a spec reaches it through `page` rather than the render
 * container.
 *
 * Split into one component per nesting level for the same reason the forms
 * catalog's `SelectField` is: spelled out inline the tree blows the repo's
 * `react/jsx-max-depth` budget.
 */
function UserOptionList(): ReactNode {
  return (
    <Autocomplete.Portal>
      <Autocomplete.Positioner>
        <UserOptionCard />
      </Autocomplete.Positioner>
    </Autocomplete.Portal>
  );
}

/** The control the operator types into. */
function UserPickerInput(props: { onBlur: () => void }): ReactNode {
  const { onBlur } = props;
  return (
    <Autocomplete.InputGroup>
      <Autocomplete.Input data-testid="organization-member-userid" onBlur={onBlur} />
    </Autocomplete.InputGroup>
  );
}

/**
 * The add-member control: search the directory by email, post a user id
 * (Wallow-lrlm.6.1).
 *
 * AUTOCOMPLETE, NOT COMBOBOX, and the distinction is the whole design.
 * `Combobox.Root` holds the SELECTED ITEM and keeps the typed text beside it, so
 * a value nobody picked off the list is not the form's value — which would break
 * every closed spec that types a bare id and expects it posted verbatim.
 * `Autocomplete.Root`'s value IS the input text (`selectionMode: "none"`), so
 * hand-typed ids keep working untouched; pressing a suggestion merely WRITES
 * into that text, and `itemToStringValue` makes what it writes the person's id
 * rather than their email.
 *
 * The directory, the narrowing and the open flag all come from
 * {@link useUserPicker}, which is also where the reasons for each are written
 * down. `filter={null}` is the half that has to be spelled here: it is what tells
 * Base UI's collator to stand aside and take `items` pre-narrowed.
 *
 * Hand-rolled from `Field` + `Autocomplete` rather than added to the forms
 * catalog: the parts a catalog field is built from (`useCatalogField`,
 * `CatalogFieldLabel`, `CatalogFieldError`) are internal to the package. Base UI
 * does the association work regardless — `Field.Root` publishes the label id,
 * the description ids and the invalid flag through context, and
 * `Autocomplete.Input` reads all three, so the accessible name, the
 * `aria-describedby` chain and `aria-invalid` land exactly where `TextField` put
 * them.
 */
function UserIdPicker(props: {
  value: string;
  error: string | undefined;
  disabled: boolean;
  onChange: (next: string) => void;
  onBlur: () => void;
}): ReactNode {
  const { value, error, disabled, onChange, onBlur } = props;
  const { matches, open, onOpenChange } = useUserPicker(value);

  return (
    <Field invalid={error !== undefined}>
      <Field.Label>User ID</Field.Label>
      <Autocomplete.Root
        items={matches}
        filter={null}
        itemToStringValue={(user: UserDto) => user.id}
        value={value}
        onValueChange={onChange}
        open={open}
        onOpenChange={onOpenChange}
        disabled={disabled}
      >
        <UserPickerInput onBlur={onBlur} />
        <UserOptionList />
      </Autocomplete.Root>
      {error === undefined ? null : (
        <Field.Error match data-testid="organization-member-userid-error">
          {error}
        </Field.Error>
      )}
    </Field>
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
