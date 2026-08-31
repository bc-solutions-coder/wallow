/**
 * Organizations feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query
 * entry (`@bc-solutions-coder/sdk/query`). Routes/components keep importing from
 * `./api`, so the "api.ts is the only data import" convention survives; what
 * changed in Wallow-pu6a.5.5 is what sits behind it. Every factory here is now
 * GENERATED from the OpenAPI document — there is no hand-written
 * `organizationsQueries` namespace, no hierarchical key registry, and nothing
 * closing over the deleted module-global client.
 *
 * Two consequences for call sites:
 *
 *  - Each factory takes `{ client }`. Read the request-scoped instance off the
 *    router context (`useRouteContext({ from: "__root__" }).sdk`) and pass
 *    `sdk.client`; there is nothing to configure or bootstrap first.
 *  - Generated keys are flat (`[{ _id, baseUrl, tags, ...args }]`) with no prefix
 *    to sweep by, so a mutation's `onSuccess` invalidates through the curated
 *    predicates re-exported below rather than by naming a parent key.
 */
export {
  invitationsGetByTenantOptions,
  invitationsGetByTenantQueryKey,
  invitationsRevokeMutation,
  meGetOrganizationsOptions,
  meGetOrganizationsQueryKey,
  organizationClientBrandingDeleteLogoMutation,
  organizationClientBrandingGetBrandingOptions,
  organizationClientBrandingGetBrandingQueryKey,
  organizationClientBrandingUpsertBrandingMutation,
  organizationClientsListOptions,
  organizationClientsDeleteMutation,
  organizationClientsLiftPlatformSuspensionMutation,
  organizationClientsListQueryKey,
  organizationClientsPlacePlatformSuspensionMutation,
  organizationClientsRegisterMutation,
  organizationClientsReinstateMutation,
  organizationClientsRotateSecretMutation,
  organizationClientsSuspendMutation,
  organizationsAddMemberMutation,
  organizationsApproveMemberMutation,
  organizationsArchiveMutation,
  organizationsCreateMutation,
  organizationsDeleteMutation,
  organizationsDenyMemberMutation,
  organizationsGetAllOptions,
  organizationsGetAllQueryKey,
  organizationsGetByIdOptions,
  organizationsGetMembersOptions,
  organizationsGetMembersQueryKey,
  organizationsGetPendingMembersOptions,
  organizationsGetPendingMembersQueryKey,
  organizationsLeaveMutation,
  organizationsLiftPlatformSuspensionMutation,
  organizationsPlacePlatformSuspensionMutation,
  organizationsReactivateMutation,
  organizationsRemoveMemberMutation,
  queriesForOperation,
  queriesWithTag,
  rolesGetRolesOptions,
  scopesListOptions,
  usersAssignRoleMutation,
  usersGetUsersOptions,
  usersRemoveRoleMutation,
} from "@bc-solutions-coder/sdk/query";
