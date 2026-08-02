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
import {
  AppForm,
  errorText,
  FormError,
  type SelectFieldOption,
  SubmitButton,
  useAppForm,
} from "@bc-solutions-coder/forms";
import { useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
import type { ClientResponse } from "@bc-solutions-coder/sdk";
import {
  Button,
  EmptyState,
  ErrorBanner,
  ListCard,
  ListRow,
  MutedText,
  Text,
} from "@bc-solutions-coder/ui";
import { useState } from "react";
import { Link, useRouteContext } from "@tanstack/react-router";
import { z } from "zod";

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

/**
 * A single bound-client row. `ListRow` derives its test id from `name` as
 * `organization-detail-client-item`.
 */
function ClientRow(props: { client: ClientResponse }) {
  const { client } = props;
  return (
    <ListRow name="organization-detail-client">
      <Text as="span" variant="bodySm" color="onCard" weight="medium">
        {client.name}
      </Text>
      <Text as="span" variant="bodySm" color="muted" className="font-mono">
        {client.clientId}
      </Text>
    </ListRow>
  );
}

/** The bound-clients list (empty until the org's clients load). */
function ClientsTable(props: { clients: readonly ClientResponse[] }) {
  return (
    <ListCard name="organization-detail-clients">
      {props.clients.map((client) => (
        <ClientRow key={client.id} client={client} />
      ))}
    </ListCard>
  );
}

/** The two client types the API accepts, as catalog-`SelectField` options. */
const CLIENT_TYPE_OPTIONS: readonly SelectFieldOption[] = [
  { value: "public", label: "Public" },
  { value: "confidential", label: "Confidential" },
];

/**
 * Newline-separated textarea input to the wire's `string[]`. Lifted verbatim
 * from `RegisterAppForm`, which collects redirect URIs the same way.
 */
function toUriList(value: string): string[] {
  return value
    .split("\n")
    .map((uri) => uri.trim())
    .filter(Boolean);
}

/**
 * What the register-client form collects. `clientType` is the form's own
 * public/confidential switch: the clients endpoint infers it from whether a
 * secret is issued, so `toVariables` DROPS it rather than sending it — which is
 * also why the body is mapped field by field and never spread.
 *
 * `.trim()` makes `"   "` fail the `min(1)`; it does not trim the submitted
 * value (TanStack's standard-schema adapter keeps `form.state.values` raw), so
 * the body `OrganizationDetail.clients.test.tsx` pins is unchanged.
 */
const registerClientSchema = z.object({
  displayName: z.string().trim().min(1, "Display name is required"),
  clientType: z.string(),
  redirectUris: z.string(),
});

/** The one-time client-id/secret reveal after a successful registration. */
function RegisterClientResult(props: { clientId?: string; clientSecret?: string | null }) {
  return (
    <div
      data-testid="organization-detail-register-success"
      className="rounded-md border border-border bg-background p-4 space-y-2 font-mono text-sm text-foreground"
    >
      <Text as="code" data-testid="organization-detail-register-client-id">
        {props.clientId}
      </Text>
      <Text as="code" data-testid="organization-detail-register-client-secret">
        {props.clientSecret}
      </Text>
    </div>
  );
}

/**
 * The register-client card. It is the form's DOM parent — the surface
 * `OrganizationDetail.restyle.test.tsx` reads off `parentOf(form)` — and exists
 * separately from the fields below only so the `AppForm` tree starts at JSX
 * depth 1 (`react/jsx-max-depth` is 2, and `AppForm > AppField > field.X` already
 * spends both levels).
 */
function RegisterClientForm(props: {
  orgId: string;
  onRegistered: (result: ClientResponse) => void;
}) {
  return (
    <div className="bg-card rounded-lg shadow-sm border border-border p-8">
      <RegisterClientFormFields orgId={props.orgId} onRegistered={props.onRegistered} />
    </div>
  );
}

/**
 * Register-client form (migrated to `@bc-solutions-coder/forms` in
 * Wallow-lrlm.5.5). The clients mutation lives HERE, not in `ClientsSection`:
 * `useAppForm` owns it, so the generated factory's `mutationFn` — which is all
 * `{op}Mutation` returns — is joined to the success work below rather than to a
 * hand-written `useMutation`.
 *
 * Every testid the closed specs select DERIVES from `testIdPrefix`:
 * `-form`, `-display-name`, `-client-type`, `-redirect-uris`, `-submit`, and the
 * server banner `-error`.
 */
function RegisterClientFormFields(props: {
  orgId: string;
  onRegistered: (result: ClientResponse) => void;
}) {
  const { orgId, onRegistered } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();

  const form = useAppForm({
    schema: registerClientSchema,
    defaultValues: { displayName: "", clientType: "public", redirectUris: "" },
    mutation: clientsCreateMutation({ client: sdk.client }),
    // Field by field, never a spread: `displayName` is the wire's `name`, the
    // URIs arrive as one newline-separated string, `postLogoutRedirectUris` has
    // no control at all, and `clientType` must NOT reach the wire.
    toVariables: (values) => ({
      body: {
        name: values.displayName,
        redirectUris: toUriList(values.redirectUris),
        postLogoutRedirectUris: [],
        tenantId: orgId,
      },
    }),
    onSuccess: (created) => {
      // Re-homed from `ClientsSection`'s deleted `useMutation`: registering a
      // client adds a row to the list this section renders above.
      void queryClient.invalidateQueries(
        queriesForOperation(
          clientsGetByTenantQueryKey({ client: sdk.client, path: { tenantId: orgId } }),
        ),
      );
      // The secret is shown ONCE and never refetched, so the reveal cannot read
      // it back off the cache — the result is handed to the section that renders it.
      onRegistered(created);
    },
    fallbackError: "Failed to register client.",
  });

  return (
    <AppForm form={form} testIdPrefix="organization-detail-register" className="space-y-6">
      <form.AppField name="displayName">
        {(field) => <field.TextField label="Display name" />}
      </form.AppField>

      <form.AppField name="clientType">
        {(field) => <field.SelectField label="Client type" options={CLIENT_TYPE_OPTIONS} />}
      </form.AppField>

      <form.AppField name="redirectUris">
        {(field) => <field.TextareaField label="Redirect URIs" />}
      </form.AppField>

      <FormError />

      {/* Pill submit, pinned by `OrganizationDetail.restyle.test.tsx`. */}
      <SubmitButton className="rounded-full">Register client</SubmitButton>
    </AppForm>
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
  const { data, isError, error } = useQuery(
    clientsGetByTenantOptions({ client: sdk.client, path: { tenantId: orgId } }),
  );
  // The registration mutation moved into the form (`useAppForm` owns it). What
  // stays here is only its RESULT: the one-time secret is rendered outside the
  // card, so the form hands it up rather than the section reading it back.
  const [result, setResult] = useState<ClientResponse | null>(null);

  const clients: readonly ClientResponse[] = data ?? [];

  return (
    <section className="space-y-4">
      <Text
        as="h2"
        variant="subheading"
        className="mb-4"
        data-testid="organization-detail-clients-heading"
      >
        Bound Clients
      </Text>
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
      {result !== null ? (
        <RegisterClientResult clientId={result.clientId} clientSecret={result.clientSecret} />
      ) : null}
      {/* The failure banner is the form's own `FormError`, rendered inside the
          card below — it carries the same `organization-detail-register-error`
          testid, and the reveal above still survives a later failure. */}
      <RegisterClientForm orgId={orgId} onRegistered={setResult} />
    </section>
  );
}

/**
 * Links to the two `$orgId`-scoped screens: pending requests and member roles.
 * Neither reaches the global rail (it has no `orgId` to fill into a
 * destination), so this page is their only way in.
 */
function ManageLinks(props: { orgId: string }) {
  const { orgId } = props;
  return (
    <div className="flex gap-3">
      <Button
        render={<Link to="/dashboard/organizations/$orgId/requests" params={{ orgId }} />}
        nativeButton={false}
        variant="secondary"
        className="w-auto no-underline"
        data-testid="organization-detail-requests-link"
      >
        Pending requests
      </Button>
      <Button
        render={<Link to="/dashboard/organizations/$orgId/members" params={{ orgId }} />}
        nativeButton={false}
        variant="secondary"
        className="w-auto no-underline"
        data-testid="organization-detail-members-link"
      >
        Manage roles
      </Button>
    </div>
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
 *
 * A not-found branch rather than a list's empty branch, but the same card: it
 * hand-rolled the identical centered surface the empty states did, so it belongs
 * to `EmptyState` for the same reason they do. No icon — this card never carried
 * one, and `EmptyState` omits the slot it is not given.
 */
function NotFoundCard() {
  return (
    <EmptyState
      data-testid="organization-detail-not-found"
      message="Organization not found."
      description="It may have been archived, or the link may point somewhere that no longer exists."
    />
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
      <Text as="h1" variant="title" data-testid="organization-detail-heading">
        {org.name}
      </Text>

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

      <ManageLinks orgId={orgId} />

      <MemberList orgId={orgId} />

      <ClientsSection orgId={orgId} />
    </div>
  );
}
