/**
 * Organizations feature query layer (Wallow-8w1h.4.1) — the CANONICAL template
 * every later vertical (Apps/Settings/MFA/Inquiries, Phases 4-6) copies.
 *
 * FEATURE-FOLDER SHAPE: src/features/<name>/{api.ts, api.test.ts, components/,
 * types.ts}. `api.ts` is the ONLY layer route/component files import for data —
 * it exposes TanStack Query `queryOptions` factories and mutation factories, all
 * delegating to `getWallowSdk().<slice>` (never importing generated SDK ops
 * directly; that is the facade's job).
 *
 */
import { queryOptions, type QueryClient } from "@tanstack/react-query";

import { getWallowSdk } from "../../lib/wallow-sdk";

/**
 * queryOptions factories for the organizations list and a single org's detail.
 * `list()` is keyed `['orgs']`; `detail(id)` is keyed `['orgs', id]` so a single
 * `invalidateQueries({ queryKey: ['orgs'] })` sweeps the whole feature's cache.
 */
export const organizationsQueries = {
  list: () =>
    queryOptions({
      queryKey: ["orgs"] as const,
      queryFn: () => getWallowSdk().organizations.list(),
    }),
  detail: (id: string) =>
    queryOptions({
      queryKey: ["orgs", id] as const,
      queryFn: () => getWallowSdk().organizations.get(id),
    }),
  members: (id: string) =>
    queryOptions({
      queryKey: ["orgs", id, "members"] as const,
      queryFn: () => getWallowSdk().organizations.members(id),
    }),
  clients: (id: string) =>
    queryOptions({
      queryKey: ["orgs", id, "clients"] as const,
      queryFn: () => getWallowSdk().organizations.clients(id),
    }),
};

/** The create-organization request body (domain is nullable per the API). */
export interface CreateOrganizationBody {
  name: string;
  domain: string | null;
}

/**
 * Mutation factory for creating an organization. Takes the router/context
 * `QueryClient` so its `onSuccess` can invalidate the `['orgs']` list query,
 * keeping the create form free of cache wiring.
 */
export const createOrganizationMutation = (queryClient: QueryClient) => ({
  mutationFn: (body: CreateOrganizationBody): Promise<unknown> =>
    getWallowSdk().organizations.create(body),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: ["orgs"] });
  },
});

/** The add-member request body (mirrors the API `AddMemberRequest`). */
export interface AddMemberBody {
  userId: string;
}

/*
 * Member & lifecycle mutation factories (Wallow-8w1h.4.4). Each closes over the
 * `QueryClient` and the target `orgId` so its `onSuccess` can invalidate the
 * right query: member add/remove sweep the members query
 * (`['orgs', id, 'members']`); archive/reactivate sweep the org list
 * (`['orgs']`). Each `mutationFn` delegates to `getWallowSdk().organizations.*`.
 */

/** Add a member to `orgId`; invalidates that org's members query on success. */
export const addMemberMutation = (queryClient: QueryClient, orgId: string) => ({
  mutationFn: (body: AddMemberBody): Promise<unknown> =>
    getWallowSdk().organizations.addMember(orgId, body),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: ["orgs", orgId, "members"] });
  },
});

/** Remove a member from `orgId`; invalidates that org's members query on success. */
export const removeMemberMutation = (queryClient: QueryClient, orgId: string) => ({
  mutationFn: (userId: string): Promise<unknown> =>
    getWallowSdk().organizations.removeMember(orgId, userId),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: ["orgs", orgId, "members"] });
  },
});

/** Archive `orgId`; invalidates the `['orgs']` list on success. */
export const archiveOrganizationMutation = (queryClient: QueryClient, orgId: string) => ({
  mutationFn: (): Promise<unknown> => getWallowSdk().organizations.archive(orgId),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: ["orgs"] });
  },
});

/** Reactivate `orgId`; invalidates the `['orgs']` list on success. */
export const reactivateOrganizationMutation = (queryClient: QueryClient, orgId: string) => ({
  mutationFn: (): Promise<unknown> => getWallowSdk().organizations.reactivate(orgId),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: ["orgs"] });
  },
});

/**
 * The register-client request body (Wallow-ffpq.3.6). Mirrors the Blazor
 * `RegisterClientForm` (display name, client type, newline-split redirect URIs);
 * the facade maps it onto the API's `CreateClientRequest`.
 */
export interface RegisterClientBody {
  displayName: string;
  clientType: string;
  redirectUris: string[];
}

/**
 * Register an OAuth client bound to `orgId`; invalidates that org's clients
 * query on success so the bound-clients table refreshes.
 */
export const registerClientMutation = (queryClient: QueryClient, orgId: string) => ({
  mutationFn: (body: RegisterClientBody): Promise<unknown> =>
    getWallowSdk().organizations.registerClient(orgId, body),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: ["orgs", orgId, "clients"] });
  },
});
