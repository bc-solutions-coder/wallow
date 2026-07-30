/**
 * Organization detail (Wallow-8w1h.4.4). Drives
 * `useQuery(organizationsGetByIdOptions(...))` and renders the org heading +
 * info (rendering `organization-detail-heading` /
 * `organization-detail-back-link` / `organization-detail-not-found` /
 * `organization-detail-error`), archive/reactivate actions, and the `MemberList`
 * for `orgId`.
 *
 * The back link is a plain anchor (not a router `Link`) so it needs no matched
 * route of its own; the SDK still comes off the router context, which every
 * mount site (app and spec alike) supplies. The lifecycle actions carry
 * `organization-detail-archive` / `organization-detail-reactivate`
 * (`{page}-{element}` kebab-case).
 */
import { useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
import type { ClientResponse } from "@bc-solutions-coder/sdk";
import { Button, ErrorBanner, Field, Input, MutedText } from "@bc-solutions-coder/ui";
import { useState } from "react";
import { useRouteContext } from "@tanstack/react-router";

import { SelectControl, type SelectControlOption } from "@shared/components/SelectControl";
import { errorText } from "@shared/lib/error-text";
import {
  clientsCreateMutation,
  clientsGetByTenantOptions,
  clientsGetByTenantQueryKey,
  organizationsArchiveMutation,
  organizationsGetByIdOptions,
  organizationsReactivateMutation,
  queriesForOperation,
  queriesWithTag,
} from "../api";
import { MemberList } from "./MemberList";

/** The shared list/table card surface — rows bleed to its edge, so no padding. */
const TABLE_CARD = "bg-card rounded-lg shadow-sm border border-border overflow-hidden";

/** A list row inside `TABLE_CARD`. */
const TABLE_ROW = "flex items-center justify-between px-6 py-4 hover:bg-background/50";

/**
 * What the register-client form collects. `clientType` is the form's own
 * public/confidential switch: the clients endpoint infers it from whether a
 * secret is issued, so this value never reaches the wire.
 */
interface RegisterClientInput {
  displayName: string;
  clientType: string;
  redirectUris: string[];
}

/** A single bound-client row. */
function ClientRow(props: { client: ClientResponse }) {
  const { client } = props;
  return (
    <li data-testid="organization-detail-client-row" className={TABLE_ROW}>
      <span className="text-sm font-medium text-card-foreground">{client.name}</span>
      <span className="text-sm text-foreground/70 font-mono">{client.clientId}</span>
    </li>
  );
}

/** The bound-clients list (empty until the org's clients load). */
function ClientsTable(props: { clients: readonly ClientResponse[] }) {
  return (
    <div className={TABLE_CARD}>
      <ul data-testid="organization-detail-clients-table" className="divide-y divide-border">
        {props.clients.map((client) => (
          <ClientRow key={client.id} client={client} />
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
 * from the org detail page, rendering `organization-detail-clients-table` (or
 * `organization-detail-clients-error`) / `organization-detail-register-*`
 * testids. The flow is a structural port (per
 * the epic's reachability bar), not a hardened one.
 */
function ClientsSection(props: { orgId: string }) {
  const { orgId } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const { data, isError, error } = useQuery(
    clientsGetByTenantOptions({ client: sdk.client, path: { tenantId: orgId } }),
  );
  const register = useMutation({
    ...clientsCreateMutation({ client: sdk.client }),
    onSuccess: (): void => {
      void queryClient.invalidateQueries(
        queriesForOperation(
          clientsGetByTenantQueryKey({ client: sdk.client, path: { tenantId: orgId } }),
        ),
      );
    },
  });

  const clients: readonly ClientResponse[] = data ?? [];
  const result: ClientResponse | undefined = register.data;

  return (
    <section className="space-y-4">
      <h2
        data-testid="organization-detail-clients-heading"
        className="text-xl font-semibold text-foreground mb-4"
      >
        Bound Clients
      </h2>
      {/* A failed read is not an empty tenant: without this the section would
          render a bound-clients table with no rows and say nothing went wrong.
          Cached clients still win over a failed background refetch. */}
      {isError && data === undefined ? (
        <ErrorBanner data-testid="organization-detail-clients-error">
          {errorText(error, "Could not load the bound clients.")}
        </ErrorBanner>
      ) : (
        <ClientsTable clients={clients} />
      )}
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
          onRegister={(input) => {
            // `clientType` stays in the form: the endpoint derives public vs
            // confidential from the secret it issues, so there is no wire field
            // for it (the deleted hand-written mutation dropped it the same way).
            register.mutate({
              body: {
                name: input.displayName,
                redirectUris: input.redirectUris,
                postLogoutRedirectUris: [],
                tenantId: orgId,
              },
            });
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
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const { data, isPending, isError, error } = useQuery(
    organizationsGetByIdOptions({ client: sdk.client, path: { id: orgId } }),
  );
  // Archive and reactivate each flip a field the LIST also renders, so both
  // sweep the whole Organizations tag rather than just this org's detail.
  const invalidateOrganizations = (): void => {
    void queryClient.invalidateQueries(queriesWithTag("Organizations"));
  };
  const archive = useMutation({
    ...organizationsArchiveMutation({ client: sdk.client }),
    onSuccess: invalidateOrganizations,
  });
  const reactivate = useMutation({
    ...organizationsReactivateMutation({ client: sdk.client }),
    onSuccess: invalidateOrganizations,
  });

  if (isPending) {
    return <MutedText data-testid="organization-detail-loading">Loading organization…</MutedText>;
  }

  // The split `InquiryDetail` already ships: an errored read reaches `org ===
  // null` just as a resolved-empty one does, so without this branch a genuine
  // 500 claims the organization does not exist. `data === undefined` is what
  // separates them — a resolved-null body still means "not found", and a cached
  // org survives a failed background refetch.
  if (isError && data === undefined) {
    return (
      <div className="space-y-8">
        <BackLink />
        <ErrorBanner data-testid="organization-detail-error">
          {errorText(error, "Could not load the organization.")}
        </ErrorBanner>
      </div>
    );
  }

  const org = data ?? null;

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
            archive.mutate({ path: { id: orgId } });
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
            reactivate.mutate({ path: { id: orgId } });
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
