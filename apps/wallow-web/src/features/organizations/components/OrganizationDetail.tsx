/**
 * Organization detail (Wallow-8w1h.4.4). Drives
 * `useQuery(organizationsQueries.detail(orgId))` and renders the org heading +
 * info (mirroring the Blazor oracle `organization-detail-heading` /
 * `organization-detail-back-link` / `organization-detail-not-found`),
 * archive/reactivate actions, and the `MemberList` for `orgId`.
 *
 * The back link is a plain anchor (not a router `Link`) so the component renders
 * standalone under a `QueryClientProvider` without a router context. The new
 * lifecycle actions carry `organization-detail-archive` /
 * `organization-detail-reactivate` (`{page}-{element}` kebab-case).
 */
import { Button, Card, ErrorBanner, Field, Input, MutedText } from "@bc-solutions-coder/ui";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  archiveOrganizationMutation,
  organizationsQueries,
  reactivateOrganizationMutation,
  registerClientMutation,
} from "../api";
import type { Organization } from "../types";
import { MemberList } from "./MemberList";

/** A bound OAuth client (narrowed at the render boundary). */
interface BoundClient {
  id?: string;
  clientId?: string;
  name?: string;
}

/** The register-client form body, mirroring the Blazor `RegisterClientForm`. */
interface RegisterClientInput {
  displayName: string;
  clientType: string;
  redirectUris: string[];
}

/** A single bound-client row. */
function ClientRow(props: { client: BoundClient }) {
  const { client } = props;
  return (
    <li data-testid="organization-detail-client-row">
      <span>{client.name}</span>
      <span>{client.clientId}</span>
    </li>
  );
}

/** The bound-clients list (empty until the org's clients load). */
function ClientsTable(props: { clients: BoundClient[] }) {
  return (
    <ul data-testid="organization-detail-clients-table">
      {props.clients.map((client) => (
        <ClientRow key={client.id ?? client.clientId} client={client} />
      ))}
    </ul>
  );
}

/** Public/confidential client-type select (kept shallow for jsx-max-depth). */
function ClientTypeSelect(props: { value: string; onChange: (value: string) => void }) {
  const { value, onChange } = props;
  return (
    <select
      data-testid="organization-detail-register-client-type"
      value={value}
      onChange={(e) => {
        onChange(e.target.value);
      }}
    >
      <option value="public">Public</option>
      <option value="confidential">Confidential</option>
    </select>
  );
}

/** The one-time client-id/secret reveal after a successful registration. */
function RegisterClientResult(props: { clientId?: string; clientSecret?: string | null }) {
  return (
    <div data-testid="organization-detail-register-success">
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
        value={redirectUris}
        onChange={(e) => {
          setRedirectUris(e.target.value);
        }}
      />
      <Button type="submit" data-testid="organization-detail-register-submit">
        Register client
      </Button>
    </form>
  );
}

/**
 * Bound OAuth clients + register-client flow (Wallow-ffpq.3.6) — the React port
 * of Blazor `OrganizationDetail.razor`'s client section. Lists the org's bound
 * clients and offers an inline register-client form, reachable straight from the
 * org detail page. Testids mirror the Blazor oracle
 * (`organization-detail-clients-table`, `organization-detail-register-*`). The
 * flow is a structural port (per the epic's reachability bar), not a hardened
 * one.
 */
function ClientsSection(props: { orgId: string }) {
  const { orgId } = props;
  const queryClient = useQueryClient();
  const { data } = useQuery(organizationsQueries.clients(orgId));
  const register = useMutation(registerClientMutation(queryClient, orgId));

  const clients = (data ?? []) as BoundClient[];
  const result = register.data as { clientId?: string; clientSecret?: string | null } | undefined;

  return (
    <section>
      <h2>Bound Clients</h2>
      <ClientsTable clients={clients} />
      {register.isSuccess && result !== undefined ? (
        <RegisterClientResult clientId={result.clientId} clientSecret={result.clientSecret} />
      ) : null}
      {register.isError ? (
        <ErrorBanner data-testid="organization-detail-register-error">
          Failed to register client.
        </ErrorBanner>
      ) : null}
      <RegisterClientForm
        onRegister={(body) => {
          register.mutate(body);
        }}
      />
    </section>
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
      <Card>
        <a href="/dashboard/organizations" data-testid="organization-detail-back-link">
          Back to organizations
        </a>
        <MutedText data-testid="organization-detail-not-found">Organization not found.</MutedText>
      </Card>
    );
  }

  return (
    <Card>
      <a href="/dashboard/organizations" data-testid="organization-detail-back-link">
        Back to organizations
      </a>
      <h1 data-testid="organization-detail-heading">{org.name}</h1>

      <div>
        <Button
          type="button"
          variant="destructive"
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
    </Card>
  );
}
