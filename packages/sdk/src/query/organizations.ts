/**
 * Organizations query module — the CANONICAL domain template every later vertical
 * (apps, settings, mfa, inquiries, user, auth) copies.
 *
 * Ported from apps/wallow-web/src/features/organizations/api.ts with three
 * changes: (a) every queryKey comes from `queryKeys.organizations.*`; (b) every
 * queryFn/mutationFn starts with `ensureQueryBootstrapped()` then calls the
 * generated op directly via `unwrap(...)` (the op-to-call mapping mirrors the
 * old wallow-web facade); (c) the request-body interfaces live and are exported
 * here.
 */
import { queryOptions, type QueryClient } from "@tanstack/react-query";

import { unwrap } from "../facade";
import {
  deleteV1IdentityOrganizationsByIdMembersByUserId,
  getV1IdentityClientsByTenantByTenantId,
  getV1IdentityOrganizations,
  getV1IdentityOrganizationsById,
  getV1IdentityOrganizationsByIdMembers,
  postV1IdentityClients,
  postV1IdentityOrganizations,
  postV1IdentityOrganizationsByIdArchive,
  postV1IdentityOrganizationsByIdMembers,
  postV1IdentityOrganizationsByIdReactivate,
} from "../generated";
import { ensureQueryBootstrapped } from "./bootstrap";
import { queryKeys } from "./keys";

/**
 * queryOptions factories for the organizations list and a single org's detail.
 * `list()` is keyed `['orgs']`; `detail(id)` is keyed `['orgs', id]` so a single
 * `invalidateQueries({ queryKey: queryKeys.organizations.all })` sweeps the whole
 * feature's cache.
 */
export const organizationsQueries = {
  list: () =>
    queryOptions({
      queryKey: queryKeys.organizations.all,
      queryFn: () => {
        ensureQueryBootstrapped();
        return unwrap(getV1IdentityOrganizations());
      },
    }),
  detail: (id: string) =>
    queryOptions({
      queryKey: queryKeys.organizations.detail(id),
      queryFn: () => {
        ensureQueryBootstrapped();
        return unwrap(getV1IdentityOrganizationsById({ path: { id } }));
      },
    }),
  members: (id: string) =>
    queryOptions({
      queryKey: queryKeys.organizations.members(id),
      queryFn: () => {
        ensureQueryBootstrapped();
        return unwrap(getV1IdentityOrganizationsByIdMembers({ path: { id } }));
      },
    }),
  clients: (id: string) =>
    queryOptions({
      queryKey: queryKeys.organizations.clients(id),
      queryFn: () => {
        ensureQueryBootstrapped();
        return unwrap(getV1IdentityClientsByTenantByTenantId({ path: { tenantId: id } }));
      },
    }),
};

/** The create-organization request body (domain is nullable per the API). */
export interface CreateOrganizationBody {
  name: string;
  domain: string | null;
}

/**
 * Mutation factory for creating an organization. Takes the router/context
 * `QueryClient` so its `onSuccess` can invalidate the org list query, keeping the
 * create form free of cache wiring.
 */
export const createOrganizationMutation = (queryClient: QueryClient) => ({
  mutationFn: (body: CreateOrganizationBody): Promise<unknown> => {
    ensureQueryBootstrapped();
    return unwrap(postV1IdentityOrganizations({ body }));
  },
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.organizations.all });
  },
});

/** The add-member request body (mirrors the API `AddMemberRequest`). */
export interface AddMemberBody {
  userId: string;
}

/*
 * Member & lifecycle mutation factories. Each closes over the `QueryClient` and
 * the target `orgId` so its `onSuccess` invalidates the right query: member
 * add/remove sweep the members query; archive/reactivate sweep the org list.
 */

/** Add a member to `orgId`; invalidates that org's members query on success. */
export const addMemberMutation = (queryClient: QueryClient, orgId: string) => ({
  mutationFn: (body: AddMemberBody): Promise<unknown> => {
    ensureQueryBootstrapped();
    return unwrap(postV1IdentityOrganizationsByIdMembers({ path: { id: orgId }, body }));
  },
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.organizations.members(orgId) });
  },
});

/** Remove a member from `orgId`; invalidates that org's members query on success. */
export const removeMemberMutation = (queryClient: QueryClient, orgId: string) => ({
  mutationFn: (userId: string): Promise<unknown> => {
    ensureQueryBootstrapped();
    return unwrap(
      deleteV1IdentityOrganizationsByIdMembersByUserId({ path: { id: orgId, userId } }),
    );
  },
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.organizations.members(orgId) });
  },
});

/** Archive `orgId`; invalidates the org list on success. */
export const archiveOrganizationMutation = (queryClient: QueryClient, orgId: string) => ({
  mutationFn: (): Promise<unknown> => {
    ensureQueryBootstrapped();
    return unwrap(postV1IdentityOrganizationsByIdArchive({ path: { id: orgId } }));
  },
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.organizations.all });
  },
});

/** Reactivate `orgId`; invalidates the org list on success. */
export const reactivateOrganizationMutation = (queryClient: QueryClient, orgId: string) => ({
  mutationFn: (): Promise<unknown> => {
    ensureQueryBootstrapped();
    return unwrap(postV1IdentityOrganizationsByIdReactivate({ path: { id: orgId } }));
  },
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.organizations.all });
  },
});

/**
 * The register-client request body: display name, client type, and redirect
 * URIs. It maps onto the API's `CreateClientRequest` via `postV1IdentityClients`.
 */
export interface RegisterClientBody {
  displayName: string;
  clientType: string;
  redirectUris: string[];
}

/**
 * Register an OAuth client bound to `orgId`; invalidates that org's clients query
 * on success so the bound-clients table refreshes.
 */
export const registerClientMutation = (queryClient: QueryClient, orgId: string) => ({
  mutationFn: (body: RegisterClientBody): Promise<unknown> => {
    ensureQueryBootstrapped();
    return unwrap(
      postV1IdentityClients({
        body: {
          name: body.displayName,
          redirectUris: body.redirectUris,
          postLogoutRedirectUris: [],
          tenantId: orgId,
        },
      }),
    );
  },
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.organizations.clients(orgId) });
  },
});
