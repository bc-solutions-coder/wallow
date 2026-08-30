/**
 * The organization's registered clients: an Applications ledger and a Service
 * accounts ledger, each with its own register flow. Renders
 * `organization-detail-applications-*` / `organization-detail-service-accounts-*`
 * (or `organization-detail-clients-error` when the read fails) and hosts the
 * `organization-detail-register-*` stepper and reveal for applications and the
 * `organization-detail-register-service-account-*` pair for service accounts.
 */
import { errorText } from "@bc-solutions-coder/forms";
import { useQuery } from "@bc-solutions-coder/query";
import type {
  OrganizationClientRegistrationResponse,
  OrganizationClientResponse,
} from "@bc-solutions-coder/sdk";
import {
  Badge,
  type BadgeProps,
  Button,
  EmptyState,
  ErrorBanner,
  ListCard,
  ListRow,
  Text,
} from "@bc-solutions-coder/ui";
import { type ReactNode, useState } from "react";
import { useRouteContext } from "@tanstack/react-router";

import { organizationClientsListOptions } from "../api";
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

/** One client row; `ListRow` derives the test id `{name}-item`. */
function ClientRow(props: { name: string; client: OrganizationClientResponse }) {
  const { name, client } = props;
  return (
    <ListRow name={name}>
      <Text as="span" variant="bodySm" color="onCard" weight="medium">
        {client.name}
      </Text>
      <Text as="span" variant="bodySm" color="muted" className="font-mono">
        {client.clientId}
      </Text>
      <Badge variant={STATUS_VARIANT[client.status] ?? "neutral"}>{client.status}</Badge>
    </ListRow>
  );
}

/** A ledger body: the rows, or the empty state when there are none. */
function Ledger(props: {
  name: string;
  clients: readonly OrganizationClientResponse[];
  emptyMessage: string;
}) {
  const { name, clients, emptyMessage } = props;
  if (clients.length === 0) {
    return <EmptyState data-testid={`${name}s-empty`} message={emptyMessage} />;
  }
  return (
    <ListCard name={`${name}s`}>
      {clients.map((client) => (
        <ClientRow key={client.clientId} name={name} client={client} />
      ))}
    </ListCard>
  );
}

/** What a ledger is doing besides listing. */
type Flow =
  | { readonly kind: "idle" }
  | { readonly kind: "registering" }
  | { readonly kind: "revealed"; readonly result: OrganizationClientRegistrationResponse };

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
            onFlow({ kind: "revealed", result });
          }}
          onCancel={toIdle}
        />
      );
    }
    case "revealed": {
      return <RegistrationReveal kind={kind} result={flow.result} onDone={toIdle} />;
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
}) {
  const { kind, orgId, clients } = props;
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
        clients={clients}
        emptyMessage={presentation.emptyMessage}
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
      />
      <KindLedger
        kind="service-account"
        orgId={orgId}
        clients={clients.filter((client) => client.kind === "service-account")}
      />
    </div>
  );
}
