/**
 * Organization detail (Wallow-8w1h.4.4). Drives
 * `useQuery(organizationsQueries.detail(orgId))` and renders the org heading +
 * info (rendering `organization-detail-heading` /
 * `organization-detail-back-link` / `organization-detail-not-found`),
 * archive/reactivate actions, and the `MemberList` for `orgId`.
 *
 * The back link is a plain anchor (not a router `Link`) so the component renders
 * standalone under a `QueryClientProvider` without a router context. The new
 * lifecycle actions carry `organization-detail-archive` /
 * `organization-detail-reactivate` (`{page}-{element}` kebab-case).
 */
import { Button, ErrorBanner, Field, Input, MutedText } from "@bc-solutions-coder/ui";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { SelectControl, type SelectControlOption } from "../../../components/SelectControl";
import {
  archiveOrganizationMutation,
  organizationsQueries,
  reactivateOrganizationMutation,
  registerClientMutation,
} from "../api";
import type { Organization } from "../types";
import { MemberList } from "./MemberList";

/** The shared list/table card surface — rows bleed to its edge, so no padding. */
const TABLE_CARD = "bg-card rounded-lg shadow-sm border border-border overflow-hidden";

/** A list row inside `TABLE_CARD`. */
const TABLE_ROW = "flex items-center justify-between px-6 py-4 hover:bg-background/50";

/** A bound OAuth client (narrowed at the render boundary). */
interface BoundClient {
  id?: string;
  clientId?: string;
  name?: string;
}

/** The register-client form body. */
interface RegisterClientInput {
  displayName: string;
  clientType: string;
  redirectUris: string[];
}

/** A single bound-client row. */
function ClientRow(props: { client: BoundClient }) {
  const { client } = props;
  return (
    <li data-testid="organization-detail-client-row" className={TABLE_ROW}>
      <span className="text-sm font-medium text-card-foreground">{client.name}</span>
      <span className="text-sm text-foreground/70 font-mono">{client.clientId}</span>
    </li>
  );
}

/** The bound-clients list (empty until the org's clients load). */
function ClientsTable(props: { clients: BoundClient[] }) {
  return (
    <div className={TABLE_CARD}>
      <ul data-testid="organization-detail-clients-table" className="divide-y divide-border">
        {props.clients.map((client) => (
          <ClientRow key={client.id ?? client.clientId} client={client} />
        ))}
      </ul>
    </div>
  );
}

/** The two client types the API accepts, as catalog-`Select` options. */
const CLIENT_TYPE_OPTIONS: readonly SelectControlOption[] = [
  { value: "public", label: "Public" },
  { value: "confidential", label: "Confidential" },
];

/** Public/confidential client-type select (kept shallow for jsx-max-depth). */
function ClientTypeSelect(props: { value: string; onChange: (value: string) => void }) {
  const { value, onChange } = props;
  return (
    <SelectControl
      testId="organization-detail-register-client-type"
      value={value}
      options={CLIENT_TYPE_OPTIONS}
      onChange={onChange}
    />
  );
}

/** The one-time client-id/secret reveal after a successful registration. */
function RegisterClientResult(props: { clientId?: string; clientSecret?: string | null }) {
  return (
    <div
      data-testid="organization-detail-register-success"
      className="rounded-md border border-border bg-background p-4 space-y-2 font-mono text-sm text-foreground"
    >
      <code data-testid="organization-detail-register-client-id">{props.clientId}</code>
      <code data-testid="organization-detail-register-client-secret">{props.clientSecret}</code>
    </div>
  );
}

/** Inline register-client form; owns its input state and reports the body up. */
function RegisterClientForm(props: { onRegister: (body: RegisterClientInput) => void }) {
  const [displayName, setDisplayName] = useState("");
  const [clientType, setClientType] = useState("public");
  const [redirectUris, setRedirectUris] = useState("");

  return (
    <form
      data-testid="organization-detail-register-form"
      className="space-y-6"
      onSubmit={(e) => {
        e.preventDefault();
        props.onRegister({
          displayName,
          clientType,
          redirectUris: redirectUris
            .split("\n")
            .map((uri) => uri.trim())
            .filter(Boolean),
        });
      }}
    >
      <Field>
        <Input
          data-testid="organization-detail-register-display-name"
          value={displayName}
          onChange={(e) => {
            setDisplayName(e.target.value);
          }}
        />
      </Field>
      <ClientTypeSelect value={clientType} onChange={setClientType} />
      <textarea
        data-testid="organization-detail-register-redirect-uris"
        className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground"
        value={redirectUris}
        onChange={(e) => {
          setRedirectUris(e.target.value);
        }}
      />
      <Button
        type="submit"
        className="rounded-full"
        data-testid="organization-detail-register-submit"
      >
        Register client
      </Button>
    </form>
  );
}

/**
 * Bound OAuth clients + register-client flow (Wallow-ffpq.3.6). Lists the org's
 * bound clients and offers an inline register-client form, reachable straight
 * from the org detail page, rendering `organization-detail-clients-table` /
 * `organization-detail-register-*` testids. The flow is a structural port (per
 * the epic's reachability bar), not a hardened one.
 */
function ClientsSection(props: { orgId: string }) {
  const { orgId } = props;
  const queryClient = useQueryClient();
  const { data } = useQuery(organizationsQueries.clients(orgId));
  const register = useMutation(registerClientMutation(queryClient, orgId));

  const clients = (data ?? []) as BoundClient[];
  const result = register.data as { clientId?: string; clientSecret?: string | null } | undefined;

  return (
    <section className="space-y-4">
      <h2
        data-testid="organization-detail-clients-heading"
        className="text-xl font-semibold text-foreground mb-4"
      >
        Bound Clients
      </h2>
      <ClientsTable clients={clients} />
      {register.isSuccess && result !== undefined ? (
        <RegisterClientResult clientId={result.clientId} clientSecret={result.clientSecret} />
      ) : null}
      {register.isError ? (
        <ErrorBanner data-testid="organization-detail-register-error">
          Failed to register client.
        </ErrorBanner>
      ) : null}
      <div className="bg-card rounded-lg shadow-sm border border-border p-8">
        <RegisterClientForm
          onRegister={(body) => {
            register.mutate(body);
          }}
        />
      </div>
    </section>
  );
}

/**
 * A plain anchor, not a router `Link`, so the component renders standalone under
 * a `QueryClientProvider` without a router context.
 */
function BackLink() {
  return (
    <a
      href="/dashboard/organizations"
      data-testid="organization-detail-back-link"
      className="text-sm text-primary hover:opacity-80 no-underline inline-block"
    >
      Back to organizations
    </a>
  );
}

/**
 * The missing-org card. It keeps the original sentence, "Organization not
 * found.", as the card heading rather than rewriting it.
 */
function NotFoundCard() {
  return (
    <div
      data-testid="organization-detail-not-found"
      className="bg-card rounded-lg shadow-sm border border-border p-12 text-center"
    >
      <h2 className="text-xl font-semibold text-foreground mb-2">Organization not found.</h2>
      <p className="text-foreground/60">
        It may have been archived, or the link may point somewhere that no longer exists.
      </p>
    </div>
  );
}

export function OrganizationDetail(props: { orgId: string }) {
  const { orgId } = props;
  const queryClient = useQueryClient();
  const { data, isPending } = useQuery(organizationsQueries.detail(orgId));
  const archive = useMutation(archiveOrganizationMutation(queryClient, orgId));
  const reactivate = useMutation(reactivateOrganizationMutation(queryClient, orgId));

  if (isPending) {
    return <MutedText data-testid="organization-detail-loading">Loading organization…</MutedText>;
  }

  // The facade returns the detail as `unknown`; narrow to the feature view-model
  // at the render boundary. A missing org surfaces as `null`/`undefined`.
  const org = (data ?? null) as Organization | null;

  if (org === null) {
    return (
      <div className="space-y-8">
        <BackLink />
        <NotFoundCard />
      </div>
    );
  }

  // A plain column, not one giant card: each section below owns its own card
  // surface, so wrapping the page in a `Card` would nest card inside card.
  return (
    <div className="space-y-8">
      <BackLink />
      <h1 data-testid="organization-detail-heading" className="text-3xl font-bold text-foreground">
        {org.name}
      </h1>

      <div className="flex gap-3">
        <Button
          type="button"
          variant="destructive"
          className="w-auto"
          data-testid="organization-detail-archive"
          onClick={() => {
            archive.mutate();
          }}
        >
          Archive
        </Button>
        <Button
          type="button"
          variant="secondary"
          className="w-auto"
          data-testid="organization-detail-reactivate"
          onClick={() => {
            reactivate.mutate();
          }}
        >
          Reactivate
        </Button>
      </div>

      <MemberList orgId={orgId} />

      <ClientsSection orgId={orgId} />
    </div>
  );
}
