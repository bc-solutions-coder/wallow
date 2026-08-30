/**
 * The platform operator's suspension surface. The reason renders read-only for
 * everyone who can see the page; the place/lift controls exist only for a
 * caller whose `users/me` read carries the global-admin authority — the API
 * refuses anyone else regardless, so the gate here is presentation, not
 * security.
 */
import { isGlobalAdmin, useCurrentUser } from "@bc-solutions-coder/auth";
import { errorText } from "@bc-solutions-coder/forms";
import { useMutation, useQueryClient } from "@bc-solutions-coder/query";
import type { OrganizationClientResponse } from "@bc-solutions-coder/sdk";
import { Button, Dialog, Input, MutedText } from "@bc-solutions-coder/ui";
import { useState } from "react";
import { useRouteContext } from "@tanstack/react-router";

import {
  organizationClientsLiftPlatformSuspensionMutation,
  organizationClientsListQueryKey,
  organizationClientsPlacePlatformSuspensionMutation,
  organizationsLiftPlatformSuspensionMutation,
  organizationsPlacePlatformSuspensionMutation,
  queriesForOperation,
  queriesWithTag,
} from "../api";

/** Whether the signed-in caller holds the platform operator's own authority. */
function useIsGlobalAdmin(): boolean {
  const { sdk } = useRouteContext({ from: "__root__" });
  const me = useCurrentUser(sdk.client);
  return isGlobalAdmin(me.data);
}

/** What a place-suspension dialog needs from the mutation that backs it. */
interface PlaceSuspension {
  readonly pending: boolean;
  readonly error: string | null;
  readonly reset: () => void;
  readonly place: (reason: string, onPlaced: () => void) => void;
}

/** The dialog's footer: cancel, and a confirm that stays dead until a reason is given. */
function PlaceSuspensionActions(props: {
  name: string;
  armed: boolean;
  pending: boolean;
  onConfirm: () => void;
}) {
  const { name, armed, pending, onConfirm } = props;
  return (
    <div className="mt-6 flex justify-end gap-2">
      <Dialog.Close data-testid={`${name}-platform-suspend-cancel`} disabled={pending}>
        Cancel
      </Dialog.Close>
      <Button
        type="button"
        className="w-auto"
        variant="destructive"
        disabled={!armed || pending}
        onClick={onConfirm}
        data-testid={`${name}-platform-suspend-confirm`}
      >
        {pending ? "Suspending…" : "Suspend"}
      </Button>
    </div>
  );
}

/** The popup body: what the suspension does, the reason field, any error, and the footer. */
function PlaceSuspensionPopup(props: {
  name: string;
  subject: string;
  reason: string;
  onReasonChange: (reason: string) => void;
  pending: boolean;
  error: string | null;
  onConfirm: () => void;
}) {
  const { name, subject, reason, onReasonChange, pending, error, onConfirm } = props;
  return (
    <Dialog.Popup data-testid={`${name}-platform-suspend-popup`}>
      <Dialog.Title>Suspend {subject} on behalf of the platform?</Dialog.Title>
      <Dialog.Description>
        Every token dies now, and only a global admin can lift the suspension. The reason is shown
        to the organization&apos;s admins.
      </Dialog.Description>
      <Input
        className="mt-4"
        value={reason}
        autoComplete="off"
        placeholder="Reason"
        aria-label="Reason"
        onChange={(event) => {
          onReasonChange(event.target.value);
        }}
        data-testid={`${name}-platform-suspend-reason`}
      />
      {error === null ? null : (
        <MutedText data-testid={`${name}-platform-error`} className="mt-4 text-destructive">
          {error}
        </MutedText>
      )}
      <PlaceSuspensionActions
        name={name}
        armed={reason.trim() !== ""}
        pending={pending}
        onConfirm={onConfirm}
      />
    </Dialog.Popup>
  );
}

/** The place control: a trigger plus the dialog that demands a reason. */
function PlaceSuspensionDialog(props: {
  name: string;
  subject: string;
  suspension: PlaceSuspension;
}) {
  const { name, subject, suspension } = props;
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const onOpenChange = (next: boolean): void => {
    setOpen(next);
    if (!next) {
      setReason("");
      suspension.reset();
    }
  };
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Trigger data-testid={`${name}-platform-suspend`}>Suspend (platform)</Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Backdrop />
        <PlaceSuspensionPopup
          name={name}
          subject={subject}
          reason={reason}
          onReasonChange={setReason}
          pending={suspension.pending}
          error={suspension.error}
          onConfirm={() => {
            suspension.place(reason, () => {
              setOpen(false);
              setReason("");
            });
          }}
        />
      </Dialog.Portal>
    </Dialog.Root>
  );
}

/** The lift control: one button, since lifting needs no justification on the record. */
function LiftSuspensionButton(props: {
  name: string;
  pending: boolean;
  error: string | null;
  onLift: () => void;
}) {
  const { name, pending, error, onLift } = props;
  return (
    <div className="flex flex-col gap-1">
      <Button
        type="button"
        className="w-auto"
        variant="secondary"
        disabled={pending}
        onClick={onLift}
        data-testid={`${name}-platform-lift`}
      >
        Lift platform suspension
      </Button>
      {error === null ? null : (
        <MutedText data-testid={`${name}-platform-error`} className="text-destructive">
          {error}
        </MutedText>
      )}
    </div>
  );
}

/** The organization-level place/lift pair; renders nothing for anyone but a global admin. */
export function OrganizationPlatformControls(props: { orgId: string; suspended: boolean }) {
  const { orgId, suspended } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const globalAdmin = useIsGlobalAdmin();
  // The suspension flips fields the organizations LIST also renders, so the
  // whole tag is swept, exactly as archive and reactivate sweep it.
  const invalidate = (): void => {
    void queryClient.invalidateQueries(queriesWithTag("Organizations"));
  };
  const place = useMutation({
    ...organizationsPlacePlatformSuspensionMutation({ client: sdk.client }),
    onSuccess: invalidate,
  });
  const lift = useMutation({
    ...organizationsLiftPlatformSuspensionMutation({ client: sdk.client }),
    onSuccess: invalidate,
  });
  if (!globalAdmin) {
    return null;
  }
  if (suspended) {
    return (
      <LiftSuspensionButton
        name="organization-detail"
        pending={lift.isPending}
        error={lift.isError ? errorText(lift.error, "Could not lift the suspension.") : null}
        onLift={() => {
          lift.mutate({ path: { id: orgId } });
        }}
      />
    );
  }
  return (
    <PlaceSuspensionDialog
      name="organization-detail"
      subject="this organization"
      suspension={{
        pending: place.isPending,
        error: place.isError ? errorText(place.error, "Could not suspend the organization.") : null,
        reset: () => {
          place.reset();
        },
        place: (reason, onPlaced) => {
          place.mutate({ path: { id: orgId }, body: { reason } }, { onSuccess: onPlaced });
        },
      }}
    />
  );
}

/** The per-client place/lift pair; renders nothing for anyone but a global admin. */
export function ClientPlatformControls(props: {
  name: string;
  orgId: string;
  client: OrganizationClientResponse;
}) {
  const { name, orgId, client } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const globalAdmin = useIsGlobalAdmin();
  const invalidate = (): void => {
    void queryClient.invalidateQueries(
      queriesForOperation(organizationClientsListQueryKey({ client: sdk.client, path: { orgId } })),
    );
  };
  const place = useMutation({
    ...organizationClientsPlacePlatformSuspensionMutation({ client: sdk.client }),
    onSuccess: invalidate,
  });
  const lift = useMutation({
    ...organizationClientsLiftPlatformSuspensionMutation({ client: sdk.client }),
    onSuccess: invalidate,
  });
  if (!globalAdmin) {
    return null;
  }
  if (typeof client.platformSuspendedAt === "string") {
    return (
      <LiftSuspensionButton
        name={name}
        pending={lift.isPending}
        error={lift.isError ? errorText(lift.error, "Could not lift the suspension.") : null}
        onLift={() => {
          lift.mutate({ path: { orgId, clientId: client.clientId } });
        }}
      />
    );
  }
  return (
    <PlaceSuspensionDialog
      name={name}
      subject={client.name}
      suspension={{
        pending: place.isPending,
        error: place.isError ? errorText(place.error, "Could not suspend the client.") : null,
        reset: () => {
          place.reset();
        },
        place: (reason, onPlaced) => {
          place.mutate(
            { path: { orgId, clientId: client.clientId }, body: { reason } },
            { onSuccess: onPlaced },
          );
        },
      }}
    />
  );
}
