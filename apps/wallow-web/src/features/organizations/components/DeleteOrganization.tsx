/**
 * Deleting an organization is the one action here that nothing undoes: every
 * bound client, membership, invitation, session, setting and API key goes with
 * it in a single transaction. So the confirm stays dead until the organization's
 * exact name has been typed back — the same guard the API enforces — and a
 * successful delete leaves the page for the organizations list, since the org
 * this screen renders no longer exists.
 */
import { hasPermission, isGlobalAdmin, useCurrentUser } from "@bc-solutions-coder/auth";
import { handledFailure, useMutation, useQueryClient } from "@bc-solutions-coder/query";
import { Button, Dialog, Input, MutedText, Text, useFailureMessage } from "@bc-solutions-coder/ui";
import { useState } from "react";
import { useNavigate, useRouteContext } from "@tanstack/react-router";

import { organizationsDeleteMutation, queriesWithTag } from "../api";

const NAME = "organization-detail";

/** The dialog's footer: cancel, and a confirm that stays dead until the name matches. */
function DeleteOrganizationActions(props: {
  armed: boolean;
  pending: boolean;
  onConfirm: () => void;
}) {
  const { armed, pending, onConfirm } = props;
  return (
    <div className="mt-6 flex justify-end gap-2">
      <Dialog.Close data-testid={`${NAME}-delete-cancel`} disabled={pending}>
        Cancel
      </Dialog.Close>
      <Button
        type="button"
        className="w-auto"
        variant="destructive"
        disabled={!armed || pending}
        onClick={onConfirm}
        data-testid={`${NAME}-delete-confirm`}
      >
        {pending ? "Deleting…" : "Delete organization"}
      </Button>
    </div>
  );
}

/** The popup body: what deletion takes with it, the type-the-name guard, any error, the footer. */
function DeleteOrganizationPopup(props: {
  orgName: string;
  typed: string;
  onTypedChange: (typed: string) => void;
  pending: boolean;
  error: string | null;
  onConfirm: () => void;
}) {
  const { orgName, typed, onTypedChange, pending, error, onConfirm } = props;
  return (
    <Dialog.Popup data-testid={`${NAME}-delete-popup`}>
      <Dialog.Title>Delete {orgName}?</Dialog.Title>
      <Dialog.Description>
        Every client, membership, invitation, session and API key this organization has goes with
        it, and nothing brings it back. Members keep their accounts. Type{" "}
        <Text as="span" variant="bodySm" color="onCard" className="font-mono">
          {orgName}
        </Text>{" "}
        to confirm.
      </Dialog.Description>
      <Input
        className="mt-4"
        value={typed}
        autoComplete="off"
        placeholder={orgName}
        aria-label="Organization name"
        onChange={(event) => {
          onTypedChange(event.target.value);
        }}
        data-testid={`${NAME}-delete-input`}
      />
      {error === null ? null : (
        <MutedText data-testid={`${NAME}-delete-error`} className="mt-4 text-destructive">
          {error}
        </MutedText>
      )}
      <DeleteOrganizationActions
        armed={typed === orgName}
        pending={pending}
        onConfirm={onConfirm}
      />
    </Dialog.Popup>
  );
}

/**
 * Whether the current user may delete this organization: the OrganizationsDelete
 * permission (the built-in admin role), or global admin — who deletes any org.
 */
function useMayDeleteOrganization(): boolean {
  const { sdk } = useRouteContext({ from: "__root__" });
  const me = useCurrentUser(sdk.client);
  return hasPermission(me.data, "OrganizationsDelete") || isGlobalAdmin(me.data);
}

/** The delete control: a trigger plus the dialog that makes the user type the name back. */
export function DeleteOrganizationDialog(props: { orgId: string; orgName: string }) {
  const { orgId, orgName } = props;
  const mayDelete = useMayDeleteOrganization();
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [typed, setTyped] = useState("");
  const remove = useMutation({
    ...organizationsDeleteMutation({ client: sdk.client }),
    // The dialog shows the refusal itself, so the toast stays out of it.
    meta: handledFailure(),
    onSuccess: (): void => {
      // The org is gone from the LIST too, so the whole tag is swept; then the
      // page leaves, because the detail it renders no longer exists to re-read.
      void queryClient.invalidateQueries(queriesWithTag("Organizations"));
      setOpen(false);
      void navigate({ to: "/dashboard/organizations" });
    },
  });
  const failure = useFailureMessage(remove.error);
  const onOpenChange = (next: boolean): void => {
    setOpen(next);
    if (!next) {
      setTyped("");
      remove.reset();
    }
  };
  // The API refuses the call anyway; hiding the trigger keeps the page honest
  // about what this user can actually do.
  if (!mayDelete) {
    return null;
  }
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Trigger data-testid={`${NAME}-delete`}>Delete</Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Backdrop />
        <DeleteOrganizationPopup
          orgName={orgName}
          typed={typed}
          onTypedChange={setTyped}
          pending={remove.isPending}
          error={failure}
          onConfirm={() => {
            remove.mutate({ path: { id: orgId }, body: { confirmName: typed } });
          }}
        />
      </Dialog.Portal>
    </Dialog.Root>
  );
}
