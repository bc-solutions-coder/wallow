/**
 * Organization member list + management (Wallow-8w1h.4.4). Drives
 * `useQuery(organizationsQueries.members(orgId))` and renders the members table
 * (mirroring the Blazor oracle `organization-detail-members-table` /
 * `organization-detail-member-row`), a per-row remove button, and an add-member
 * form backed by `addMemberMutation` / `removeMemberMutation`.
 *
 * Testids follow `{page}-{element}` kebab-case: `organization-members-loading`
 * and `organization-members-empty` (query states), `organization-member-userid`
 * + `organization-member-add-submit` (add form), `organization-member-remove`
 * (per-row remove).
 */
import { Button, Card, Field, Input, MutedText } from "@bc-solutions-coder/ui";
import { useMutation, useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { addMemberMutation, organizationsQueries, removeMemberMutation } from "../api";
import type { OrganizationMember } from "../types";

/** A single member row with a remove action. */
function MemberRow(props: { member: OrganizationMember; onRemove: (userId: string) => void }) {
  const { member, onRemove } = props;
  return (
    <li data-testid="organization-detail-member-row">
      <span>{member.email}</span>
      <Button
        type="button"
        variant="destructive"
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

export function MemberList(props: { orgId: string }) {
  const { orgId } = props;
  const queryClient = useQueryClient();
  const { data, isPending } = useQuery(organizationsQueries.members(orgId));
  const removeMember = useMutation(removeMemberMutation(queryClient, orgId));

  return (
    <Card>
      <AddMemberForm queryClient={queryClient} orgId={orgId} />

      {isPending ? (
        <MutedText data-testid="organization-members-loading">Loading members…</MutedText>
      ) : (
        <MemberTable
          members={(data ?? []) as OrganizationMember[]}
          onRemove={(id) => {
            removeMember.mutate(id);
          }}
        />
      )}
    </Card>
  );
}

/** Add-member form, backed by `addMemberMutation`. */
function AddMemberForm(props: { queryClient: QueryClient; orgId: string }) {
  const { queryClient, orgId } = props;
  const addMember = useMutation(addMemberMutation(queryClient, orgId));
  const [userId, setUserId] = useState("");

  return (
    <form
      data-testid="organization-member-add-form"
      onSubmit={(e) => {
        e.preventDefault();
        e.stopPropagation();
        if (userId.trim() === "") {
          return;
        }
        addMember.mutate(
          { userId },
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
      <Button type="submit" data-testid="organization-member-add-submit">
        Add member
      </Button>
    </form>
  );
}

/** The loaded members table: empty state or the row list. */
function MemberTable(props: { members: OrganizationMember[]; onRemove: (userId: string) => void }) {
  const { members, onRemove } = props;

  if (members.length === 0) {
    return <MutedText data-testid="organization-members-empty">No members yet.</MutedText>;
  }

  return (
    <ul data-testid="organization-detail-members-table">
      {members.map((member) => (
        <MemberRow key={member.id} member={member} onRemove={onRemove} />
      ))}
    </ul>
  );
}
