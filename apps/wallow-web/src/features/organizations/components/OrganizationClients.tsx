/**
 * The organization's registered clients: an Applications ledger and a Service
 * accounts ledger, each with its own register flow. Renders
 * `organization-detail-applications-*` / `organization-detail-service-accounts-*`
 * (or `organization-detail-clients-error` when the read fails) and hosts the
 * `organization-detail-register-*` stepper and reveal for applications and the
 * `organization-detail-register-service-account-*` pair for service accounts.
 * Each row shows who created the client and who last rotated its secret, and
 * carries a `{row}-rotate` dialog whose reveal reuses the registration one.
 */
import { errorText } from "@bc-solutions-coder/forms";
import { useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
import type {
  OrganizationClientRegistrationResponse,
  OrganizationClientResponse,
  UserDto,
} from "@bc-solutions-coder/sdk";
import {
  Badge,
  type BadgeProps,
  Button,
  Checkbox,
  Dialog,
  EmptyState,
  ErrorBanner,
  ListCard,
  ListRow,
  MutedText,
  Text,
} from "@bc-solutions-coder/ui";
import { formatLongDate } from "@bc-solutions-coder/utils/format";
import { type ReactNode, useState } from "react";
import { useRouteContext } from "@tanstack/react-router";

import {
  organizationClientsListOptions,
  organizationClientsListQueryKey,
  organizationClientsRotateSecretMutation,
  organizationsGetMembersOptions,
  queriesForOperation,
} from "../api";
import {
  type ClientKind,
  RegisterClient,
  RegistrationReveal,
  registerTestIdPrefix,
} from "./RegisterClient";

const STATUS_VARIANT: Record<string, BadgeProps["variant"]> = {
  active: "success",
  suspended: "warning",
};

/** Resolves a user id to something a person recognises; the id itself when nobody matches. */
type NameOf = (userId: string) => string;

function displayName(user: UserDto): string {
  const full = [user.firstName, user.lastName].filter(Boolean).join(" ").trim();
  return full === "" ? user.email : full;
}

/** "Created by Ada · 30 Aug 2026" and "Secret rotated by … · …" or "never rotated". */
function ClientProvenance(props: {
  name: string;
  client: OrganizationClientResponse;
  nameOf: NameOf;
}) {
  const { name, client, nameOf } = props;
  const rotated =
    client.lastRotatedByUserId && client.lastRotatedAt
      ? `Secret rotated by ${nameOf(client.lastRotatedByUserId)} · ${formatLongDate(client.lastRotatedAt)}`
      : "Secret never rotated";
  return (
    <div className="flex flex-col gap-0.5">
      <Text as="span" variant="bodySm" color="muted" data-testid={`${name}-created`}>
        Created by {nameOf(client.createdByUserId)} · {formatLongDate(client.createdAt)}
      </Text>
      <Text as="span" variant="bodySm" color="muted" data-testid={`${name}-rotated`}>
        {rotated}
      </Text>
    </div>
  );
}

/** The dialog's footer, its own component so the popup stays under `jsx-max-depth`. */
function RotateSecretActions(props: { name: string; pending: boolean; onConfirm: () => void }) {
  const { name, pending, onConfirm } = props;
  return (
    <div className="mt-6 flex justify-end gap-2">
      <Dialog.Close data-testid={`${name}-rotate-cancel`} disabled={pending}>
        Cancel
      </Dialog.Close>
      <Button
        type="button"
        className="w-auto"
        variant="destructive"
        disabled={pending}
        onClick={onConfirm}
        data-testid={`${name}-rotate-confirm`}
      >
        {pending ? "Rotating…" : "Rotate secret"}
      </Button>
    </div>
  );
}

/** The "also revoke active tokens" choice. */
function RevokeTokensOption(props: {
  name: string;
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
}) {
  const { name, checked, onCheckedChange } = props;
  return (
    <label className="mt-4 flex items-start gap-3">
      <Checkbox.Root
        checked={checked}
        onCheckedChange={(next: boolean) => {
          onCheckedChange(next);
        }}
        data-testid={`${name}-rotate-revoke`}
      >
        <Checkbox.Indicator>✓</Checkbox.Indicator>
      </Checkbox.Root>
      <Text as="span" variant="bodySm" color="onCard">
        Also revoke active tokens — every access and refresh token this client holds stops working
        now, not just the old secret.
      </Text>
    </label>
  );
}

/** The popup body: what rotation does, the revoke option, any error, and the footer. */
function RotateSecretPopup(props: {
  name: string;
  client: OrganizationClientResponse;
  revoke: boolean;
  onRevokeChange: (revoke: boolean) => void;
  pending: boolean;
  error: string | null;
  onConfirm: () => void;
}) {
  const { name, client, revoke, onRevokeChange, pending, error, onConfirm } = props;
  return (
    <Dialog.Popup data-testid={`${name}-rotate-popup`}>
      <Dialog.Title>Rotate the secret for {client.name}?</Dialog.Title>
      <Dialog.Description>
        The current secret stops working the moment the new one is issued — there is no overlap. The
        new secret is shown once.
      </Dialog.Description>
      <RevokeTokensOption name={name} checked={revoke} onCheckedChange={onRevokeChange} />
      {error === null ? null : (
        <MutedText data-testid={`${name}-rotate-error`} className="mt-4 text-destructive">
          {error}
        </MutedText>
      )}
      <RotateSecretActions name={name} pending={pending} onConfirm={onConfirm} />
    </Dialog.Popup>
  );
}

/** The per-row rotate control: a trigger plus the dialog that confirms it. */
function RotateSecretDialog(props: {
  name: string;
  orgId: string;
  client: OrganizationClientResponse;
  onRotated: (result: OrganizationClientRegistrationResponse) => void;
}) {
  const { name, orgId, client, onRotated } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [revoke, setRevoke] = useState(false);
  const rotate = useMutation({
    ...organizationClientsRotateSecretMutation({ client: sdk.client }),
    onSuccess: (result): void => {
      // The row's "last rotated" line comes off the ledger read; the secret
      // itself is shown once and never refetched, so the result is handed up.
      void queryClient.invalidateQueries(
        queriesForOperation(
          organizationClientsListQueryKey({ client: sdk.client, path: { orgId } }),
        ),
      );
      setOpen(false);
      onRotated(result);
    },
  });
  const onOpenChange = (next: boolean): void => {
    setOpen(next);
    if (!next) {
      setRevoke(false);
      rotate.reset();
    }
  };
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Trigger data-testid={`${name}-rotate`}>Rotate secret</Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Backdrop />
        <RotateSecretPopup
          name={name}
          client={client}
          revoke={revoke}
          onRevokeChange={setRevoke}
          pending={rotate.isPending}
          error={rotate.isError ? errorText(rotate.error, "Could not rotate the secret.") : null}
          onConfirm={() => {
            rotate.mutate({
              path: { orgId, clientId: client.clientId },
              body: { revokeActiveTokens: revoke },
            });
          }}
        />
      </Dialog.Portal>
    </Dialog.Root>
  );
}

/** One client row; `ListRow` derives the test id `{name}-item`. */
function ClientRow(props: {
  name: string;
  orgId: string;
  client: OrganizationClientResponse;
  nameOf: NameOf;
  onRotated: (result: OrganizationClientRegistrationResponse) => void;
}) {
  const { name, orgId, client, nameOf, onRotated } = props;
  return (
    <ListRow name={name} className="flex-wrap gap-x-6 gap-y-2">
      <Text as="span" variant="bodySm" color="onCard" weight="medium">
        {client.name}
      </Text>
      <Text as="span" variant="bodySm" color="muted" className="font-mono">
        {client.clientId}
      </Text>
      <Badge variant={STATUS_VARIANT[client.status] ?? "neutral"}>{client.status}</Badge>
      <ClientProvenance name={name} client={client} nameOf={nameOf} />
      <RotateSecretDialog name={name} orgId={orgId} client={client} onRotated={onRotated} />
    </ListRow>
  );
}

/** A ledger body: the rows, or the empty state when there are none. */
function Ledger(props: {
  name: string;
  orgId: string;
  clients: readonly OrganizationClientResponse[];
  emptyMessage: string;
  nameOf: NameOf;
  onRotated: (result: OrganizationClientRegistrationResponse) => void;
}) {
  const { name, orgId, clients, emptyMessage, nameOf, onRotated } = props;
  if (clients.length === 0) {
    return <EmptyState data-testid={`${name}s-empty`} message={emptyMessage} />;
  }
  return (
    <ListCard name={`${name}s`}>
      {clients.map((client) => (
        <ClientRow
          key={client.clientId}
          name={name}
          orgId={orgId}
          client={client}
          nameOf={nameOf}
          onRotated={onRotated}
        />
      ))}
    </ListCard>
  );
}

/** What a ledger is doing besides listing. */
type Flow =
  | { readonly kind: "idle" }
  | { readonly kind: "registering" }
  | {
      readonly kind: "revealed";
      readonly result: OrganizationClientRegistrationResponse;
      /** What minted the secret being shown; a rotation titles the reveal differently. */
      readonly origin: "registered" | "rotated";
    };

const ROTATED_TITLE = "Secret rotated";

function RegistrationFlow(props: {
  kind: ClientKind;
  orgId: string;
  flow: Flow;
  onFlow: (flow: Flow) => void;
}) {
  const { kind, orgId, flow, onFlow } = props;
  const toIdle = (): void => {
    onFlow({ kind: "idle" });
  };
  switch (flow.kind) {
    case "registering": {
      return (
        <RegisterClient
          kind={kind}
          orgId={orgId}
          onRegistered={(result) => {
            onFlow({ kind: "revealed", result, origin: "registered" });
          }}
          onCancel={toIdle}
        />
      );
    }
    case "revealed": {
      return (
        <RegistrationReveal
          kind={kind}
          result={flow.result}
          title={flow.origin === "rotated" ? ROTATED_TITLE : undefined}
          onDone={toIdle}
        />
      );
    }
    default: {
      return null;
    }
  }
}

/** How each kind's ledger is labelled and where its test ids hang. */
interface LedgerPresentation {
  readonly heading: string;
  readonly headingTestId: string;
  readonly rowName: string;
  readonly emptyMessage: string;
  readonly registerLabel: string;
}

const LEDGERS: Record<ClientKind, LedgerPresentation> = {
  application: {
    heading: "Applications",
    headingTestId: "organization-detail-applications-heading",
    rowName: "organization-detail-application",
    emptyMessage: "No applications registered yet.",
    registerLabel: "Register application",
  },
  "service-account": {
    heading: "Service accounts",
    headingTestId: "organization-detail-service-accounts-heading",
    rowName: "organization-detail-service-account",
    emptyMessage: "No service accounts yet.",
    registerLabel: "Register service account",
  },
};

function LedgerHeading(props: { testId: string; children: string; action?: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4">
      <Text as="h2" variant="subheading" data-testid={props.testId}>
        {props.children}
      </Text>
      {props.action}
    </div>
  );
}

/** One kind's ledger: its rows, its Register button, and the flow the button opens. */
function KindLedger(props: {
  kind: ClientKind;
  orgId: string;
  clients: readonly OrganizationClientResponse[];
  nameOf: NameOf;
}) {
  const { kind, orgId, clients, nameOf } = props;
  const presentation = LEDGERS[kind];
  const [flow, setFlow] = useState<Flow>({ kind: "idle" });
  const registerButton =
    flow.kind === "idle" ? (
      <Button
        type="button"
        className="w-auto"
        onClick={() => {
          setFlow({ kind: "registering" });
        }}
        data-testid={`${registerTestIdPrefix(kind)}-open`}
      >
        {presentation.registerLabel}
      </Button>
    ) : null;
  return (
    <section className="space-y-4">
      <LedgerHeading testId={presentation.headingTestId} action={registerButton}>
        {presentation.heading}
      </LedgerHeading>
      <Ledger
        name={presentation.rowName}
        orgId={orgId}
        clients={clients}
        emptyMessage={presentation.emptyMessage}
        nameOf={nameOf}
        onRotated={(result) => {
          setFlow({ kind: "revealed", result, origin: "rotated" });
        }}
      />
      <RegistrationFlow kind={kind} orgId={orgId} flow={flow} onFlow={setFlow} />
    </section>
  );
}

export function OrganizationClients(props: { orgId: string }) {
  const { orgId } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const { data, isError, error } = useQuery(
    organizationClientsListOptions({ client: sdk.client, path: { orgId } }),
  );
  // The member list `OrganizationDetail` already reads — shared cache entry,
  // no second request — names the people behind the created/rotated ids.
  const members = useQuery(
    organizationsGetMembersOptions({ client: sdk.client, path: { id: orgId } }),
  );
  const nameOf: NameOf = (userId) => {
    const member = members.data?.find((candidate) => candidate.id === userId);
    return member === undefined ? userId : displayName(member);
  };

  // A failed read is not an empty org: without this the ledgers would render
  // empty and say nothing went wrong. Cached clients still win over a failed
  // background refetch.
  if (isError && data === undefined) {
    return (
      <ErrorBanner data-testid="organization-detail-clients-error">
        {errorText(error, "Could not load the organization's clients.")}
      </ErrorBanner>
    );
  }

  const clients: readonly OrganizationClientResponse[] = data ?? [];
  return (
    <div className="space-y-8">
      <KindLedger
        kind="application"
        orgId={orgId}
        clients={clients.filter((client) => client.kind === "application")}
        nameOf={nameOf}
      />
      <KindLedger
        kind="service-account"
        orgId={orgId}
        clients={clients.filter((client) => client.kind === "service-account")}
        nameOf={nameOf}
      />
    </div>
  );
}
