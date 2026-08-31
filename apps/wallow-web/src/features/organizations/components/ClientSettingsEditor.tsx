/**
 * The per-client settings editor an application row opens: the refresh-token
 * lifetime and the back-channel logout session switch, pre-filled from the
 * row. Renders `organization-detail-client-settings-*`; Save PATCHes the
 * client with its URIs and scopes echoed back unchanged, so only the edited
 * settings move. A blank lifetime keeps the current one (null on the wire —
 * the server treats null as "keep the current policy"), and a change applies
 * to new logins only: refresh tokens already issued keep the lifetime they
 * were minted with.
 */
import { errorText } from "@bc-solutions-coder/forms";
import { useMutation, useQueryClient } from "@bc-solutions-coder/query";
import type { OrganizationClientResponse } from "@bc-solutions-coder/sdk";
import { Button, Card, CardHeader, Checkbox, Input, MutedText, Text } from "@bc-solutions-coder/ui";
import type { ReactElement } from "react";
import { useState } from "react";
import { useRouteContext } from "@tanstack/react-router";

import {
  organizationClientsListQueryKey,
  organizationClientsUpdateMutation,
  queriesForOperation,
} from "../api";
import { isValidRefreshLifetime, REFRESH_LIFETIME_RANGE_MESSAGE } from "./RegisterClient";

/** Every element in the editor hangs its test id off this prefix. */
const TEST_ID = "organization-detail-client-settings";

/** The saved lifetime as the input's initial text; absent means the global default decides. */
function initialLifetime(client: OrganizationClientResponse): string {
  const { refreshTokenLifetime } = client;
  return refreshTokenLifetime === null || refreshTokenLifetime === undefined
    ? ""
    : String(refreshTokenLifetime);
}

/** The footer: Close, and a Save that stays dead while the value is out of bounds. */
function SettingsActions(props: {
  saving: boolean;
  saveDisabled: boolean;
  onSave: () => void;
  onDone: () => void;
}): ReactElement {
  const { saving, saveDisabled, onSave, onDone } = props;
  return (
    <div className="mt-6 flex flex-wrap items-center justify-end gap-2">
      <Button
        type="button"
        variant="secondary"
        className="w-auto"
        disabled={saving}
        onClick={onDone}
        data-testid={`${TEST_ID}-close`}
      >
        Close
      </Button>
      <Button
        type="button"
        className="w-auto"
        disabled={saveDisabled || saving}
        onClick={onSave}
        data-testid={`${TEST_ID}-save`}
      >
        {saving ? "Saving…" : "Save settings"}
      </Button>
    </div>
  );
}

/** The lifetime field: label, input and the note on what blank means. */
function LifetimeField(props: {
  value: string;
  onValueChange: (value: string) => void;
}): ReactElement {
  const { value, onValueChange } = props;
  return (
    <label className="flex flex-col gap-1">
      <Text as="span" variant="bodySm" color="onCard" weight="medium">
        Refresh token lifetime (seconds)
      </Text>
      <Input
        value={value}
        autoComplete="off"
        inputMode="numeric"
        placeholder="86400"
        onChange={(event) => {
          onValueChange(event.target.value);
        }}
        data-testid={`${TEST_ID}-lifetime`}
      />
      <MutedText>
        How long after sign-in a session lasts before the user must sign in again. Applies to new
        logins only; leave blank to keep the current lifetime.
      </MutedText>
    </label>
  );
}

/** The back-channel logout session switch: guarantees `sid` in logout tokens. */
function SessionRequiredField(props: {
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
}): ReactElement {
  const { checked, onCheckedChange } = props;
  return (
    <label className="mt-4 flex items-start gap-3">
      <Checkbox.Root
        checked={checked}
        onCheckedChange={(next: boolean) => {
          onCheckedChange(next);
        }}
        data-testid={`${TEST_ID}-session-required`}
      >
        <Checkbox.Indicator>✓</Checkbox.Indicator>
      </Checkbox.Root>
      <Text as="span" variant="bodySm" color="onCard">
        Require a session id in back-channel logout tokens — for relying parties that end one
        session at a time rather than everything the user holds.
      </Text>
    </label>
  );
}

/** The settings editor for one application. */
export function ClientSettingsEditor(props: {
  orgId: string;
  client: OrganizationClientResponse;
  onDone: () => void;
}): ReactElement {
  const { orgId, client, onDone } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const [lifetime, setLifetime] = useState(() => initialLifetime(client));
  const [sessionRequired, setSessionRequired] = useState(
    () => client.backchannelLogoutSessionRequired === true,
  );

  const save = useMutation({
    ...organizationClientsUpdateMutation({ client: sdk.client }),
    onSuccess: (): void => {
      // The row's lifetime comes off the ledger read, so the save re-reads it.
      void queryClient.invalidateQueries(
        queriesForOperation(
          organizationClientsListQueryKey({ client: sdk.client, path: { orgId } }),
        ),
      );
      onDone();
    },
  });

  const invalid = !isValidRefreshLifetime(lifetime);
  const onSave = (): void => {
    const trimmed = lifetime.trim();
    save.mutate({
      path: { orgId, clientId: client.clientId },
      body: {
        redirectUris: [...client.redirectUris],
        postLogoutRedirectUris: [...client.postLogoutRedirectUris],
        backchannelLogoutUri: client.backchannelLogoutUri,
        backchannelLogoutSessionRequired: sessionRequired,
        scopes: [...client.scopes],
        refreshTokenLifetime: trimmed === "" ? null : Number(trimmed),
      },
    });
  };
  const errors = [
    invalid ? REFRESH_LIFETIME_RANGE_MESSAGE : null,
    save.isError ? errorText(save.error, "Could not save the client settings.") : null,
  ].filter((message): message is string => message !== null);

  return (
    <Card data-testid={`${TEST_ID}-card`}>
      <CardHeader
        title={`Settings for ${client.name}`}
        titleTestId={`${TEST_ID}-heading`}
        description="Per-client sign-in policy for this application."
      />
      <LifetimeField value={lifetime} onValueChange={setLifetime} />
      <SessionRequiredField checked={sessionRequired} onCheckedChange={setSessionRequired} />
      {errors.map((message) => (
        <MutedText key={message} className="mt-4 text-destructive" data-testid={`${TEST_ID}-error`}>
          {message}
        </MutedText>
      ))}
      <SettingsActions
        saving={save.isPending}
        saveDisabled={invalid}
        onSave={onSave}
        onDone={onDone}
      />
    </Card>
  );
}
