/**
 * Organization member list + management (Wallow-8w1h.4.4). Drives
 * `useQuery(organizationsGetMembersOptions(...))` and renders the members table
 * (rendering `organization-detail-members-table` /
 * `organization-detail-member-row`), a per-row remove button, and an add-member
 * form backed by the generated add/remove member mutations.
 *
 * Both writes sweep the members OPERATION rather than a key prefix — generated
 * keys are flat, so `queriesForOperation(organizationsGetMembersQueryKey(...))`
 * is what re-reads the list this component just changed.
 *
 * Testids follow `{page}-{element}` kebab-case: `organization-members-loading`
 * and `organization-members-empty` (query states), `organization-member-userid`
 * + `organization-member-add-submit` (add form), `organization-member-remove`
 * (per-row remove).
 */
import { useMutation, useQuery, useQueryClient, type QueryClient } from "@bc-solutions-coder/query";
import type { UserDto, WallowSdk } from "@bc-solutions-coder/sdk";
import { Button, Field, Input, MutedText } from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import { useState } from "react";

import {
  organizationsAddMemberMutation,
  organizationsGetMembersOptions,
  organizationsGetMembersQueryKey,
  organizationsRemoveMemberMutation,
  queriesForOperation,
} from "../api";

/** A single member row with a remove action. */
function MemberRow(props: { member: UserDto; onRemove: (userId: string) => void }) {
  const { member, onRemove } = props;
  return (
    <li
      data-testid="organization-detail-member-row"
      className="flex items-center justify-between px-6 py-4 hover:bg-background/50"
    >
      <span
        data-testid="organization-member-email"
        className="text-sm font-medium text-card-foreground"
      >
        {member.email}
      </span>
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
    </li>
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
  const { data, isPending } = useQuery(
    organizationsGetMembersOptions({ client: sdk.client, path: { id: orgId } }),
  );
  const removeMember = useMutation({
    ...organizationsRemoveMemberMutation({ client: sdk.client }),
    onSuccess: (): void => {
      void queryClient.invalidateQueries(membersOfOperation(sdk.client, orgId));
    },
  });

  // No ui `Card` around the section: the table's `px-6 py-4` cells must bleed to
  // their own card's edge, which Card's fixed `p-6 space-y-6` prevents.
  return (
    <div>
      <h2
        data-testid="organization-members-heading"
        className="text-xl font-semibold text-foreground mb-4"
      >
        Members
      </h2>

      <AddMemberForm client={sdk.client} queryClient={queryClient} orgId={orgId} />

      {isPending ? (
        <MutedText data-testid="organization-members-loading" className="text-center py-8">
          Loading members…
        </MutedText>
      ) : (
        <MemberTable
          members={data ?? []}
          onRemove={(userId) => {
            removeMember.mutate({ path: { id: orgId, userId } });
          }}
        />
      )}
    </div>
  );
}

/** Add-member form, backed by the generated add-member mutation. */
function AddMemberForm(props: {
  client: WallowSdk["client"];
  queryClient: QueryClient;
  orgId: string;
}) {
  const { client, queryClient, orgId } = props;
  const addMember = useMutation({
    ...organizationsAddMemberMutation({ client }),
    onSuccess: (): void => {
      void queryClient.invalidateQueries(membersOfOperation(client, orgId));
    },
  });
  const [userId, setUserId] = useState("");

  return (
    <form
      data-testid="organization-member-add-form"
      className="flex items-end gap-3 mb-4"
      onSubmit={(e) => {
        e.preventDefault();
        e.stopPropagation();
        if (userId.trim() === "") {
          return;
        }
        addMember.mutate(
          { path: { id: orgId }, body: { userId } },
          {
            onSuccess: () => {
              setUserId("");
            },
          },
        );
      }}
    >
      <Field>
        <Input
          data-testid="organization-member-userid"
          value={userId}
          onChange={(e) => {
            setUserId(e.target.value);
          }}
        />
      </Field>
      <Button type="submit" className="w-auto" data-testid="organization-member-add-submit">
        Add member
      </Button>
    </form>
  );
}

/** The loaded members table: empty state or the row list. */
function MemberTable(props: { members: readonly UserDto[]; onRemove: (userId: string) => void }) {
  const { members, onRemove } = props;

  if (members.length === 0) {
    // This empty state stays a MutedText: unlike the list/detail ones it has no
    // block children, so a `<p>` is still legal markup.
    return (
      <MutedText
        data-testid="organization-members-empty"
        className="bg-card rounded-lg shadow-sm border border-border p-8 text-center"
      >
        No members yet.
      </MutedText>
    );
  }

  return (
    <div className="bg-card rounded-lg shadow-sm border border-border overflow-hidden">
      <ul data-testid="organization-detail-members-table" className="divide-y divide-border">
        {members.map((member) => (
          <MemberRow key={member.id} member={member} onRemove={onRemove} />
        ))}
      </ul>
    </div>
  );
}
