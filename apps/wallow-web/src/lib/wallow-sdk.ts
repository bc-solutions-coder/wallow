/**
 * getWallowSdk() facade — the single guarded-singleton entry every feature
 * (Phases 3-6) extends.
 *
 * On first use it configures the BFF client exactly once and wires the CSRF
 * request interceptor onto the shared `@hey-api` client; thereafter it returns
 * a namespaced object whose slice methods delegate to the SDK's generated ops
 * and unwrap `{ data, error }` — returning `data` on success and THROWING the
 * `error` (RFC 7807 ProblemDetails) so React Query surfaces it, never returning
 * `undefined`.
 *
 * CONVENTION: this file (or `src/lib/sdk/<feature>.ts` slices it re-exports) is
 * the ONLY place allowed to import generated ops (`getV1Identity...`) from
 * `@bc-solutions-coder/sdk`. Route/component files import only from their
 * feature's `api.ts` and `getWallowSdk()`.
 *
 * SLICE-APPEND PATTERN (features 4-7 follow this): the `sdk` object is a FLAT
 * namespaced object. Each later feature APPENDS a top-level key (apps, settings,
 * mfa, inquiries) — e.g. `apps: { list, get, create, ... }` wrapping its
 * generated ops via the same `unwrap()` helper. Slices are additive top-level
 * keys, never nested reshapes, so parallel features touch disjoint keys of one
 * object. If contention on this file becomes a problem, split a slice into
 * `src/lib/sdk/<feature>.ts` and re-export it here — the flat top-level key
 * shape is preserved either way.
 */
import {
  type AddInquiryCommentRequest,
  client,
  configureBffClient,
  configureSsrClient,
  createConfiguredOnce,
  deleteV1IdentityOrganizationsByIdMembersByUserId,
  getSsrRequestContext,
  getUser,
  getV1IdentityApps,
  getV1IdentityAppsByClientId,
  getV1IdentityClientsByTenantByTenantId,
  getV1IdentityOrganizations,
  getV1IdentityOrganizationsById,
  getV1IdentityOrganizationsByIdMembers,
  getV1IdentityMfaStatus,
  getV1IdentityUsersMe,
  getV1InquiriesById,
  getV1InquiriesByIdComments,
  getV1InquiriesSubmitted,
  patchV1InquiriesByIdStatus,
  postV1IdentityAppsByClientIdBranding,
  postV1IdentityAppsRegister,
  postV1IdentityClients,
  postV1IdentityMfaBackupCodesRegenerate,
  postV1IdentityMfaDisable,
  postV1IdentityMfaEnrollConfirm,
  postV1IdentityMfaEnrollTotp,
  postV1IdentityOrganizations,
  postV1IdentityOrganizationsByIdArchive,
  postV1IdentityOrganizationsByIdMembers,
  postV1IdentityOrganizationsByIdReactivate,
  postV1Inquiries,
  postV1InquiriesByIdComments,
  unwrap,
  wireCsrfInterceptor,
  type RegisterAppRequest,
  type SsrRequestContext,
  type SubmitInquiryRequest,
  type WallowUser,
} from "@bc-solutions-coder/sdk";

/**
 * Configure the BFF client and wire the matching request interceptor. Invoked
 * exactly once by the {@link createConfiguredOnce} guard wrapping
 * `getWallowSdk()`.
 *
 * During SSR (`import.meta.env.SSR`) the SDK's {@link configureSsrClient} points
 * the client at the request's ABSOLUTE origin (`${origin}/api`) so Node's `fetch`
 * can parse the URL and wires the live cookie-forwarding interceptor that carries
 * the session; in the browser the same-origin relative `/api` default and the
 * CSRF interceptor apply. The origin is stable per host, so configuring it once
 * on the first request is correct.
 */
function configureClient(): void {
  if (import.meta.env.SSR) {
    configureSsrClient(getSsrRequestContext());
  } else {
    configureBffClient();
    wireCsrfInterceptor(client);
  }
}

/** Organizations slice — the template every later feature slice copies. */
export interface OrganizationsSlice {
  list: () => Promise<unknown>;
  get: (id: string) => Promise<unknown>;
  create: (body: { name: string; domain: string | null }) => Promise<unknown>;
  /** List an organization's members (returns `UserDto[]`). */
  members: (id: string) => Promise<unknown>;
  /** Add a user to an organization (body is `AddMemberRequest` = `{ userId }`). */
  addMember: (id: string, body: { userId: string }) => Promise<unknown>;
  /** Remove a user from an organization by user id. */
  removeMember: (id: string, userId: string) => Promise<unknown>;
  /** Archive an organization. */
  archive: (id: string) => Promise<unknown>;
  /** Reactivate an archived organization. */
  reactivate: (id: string) => Promise<unknown>;
  /** List the OAuth clients bound to the org's tenant (returns `ClientResponse[]`). */
  clients: (id: string) => Promise<unknown>;
  /** Register an OAuth client bound to the org's tenant (returns `ClientResponse`). */
  registerClient: (
    id: string,
    body: { displayName: string; clientType: string; redirectUris: string[] },
  ) => Promise<unknown>;
}

/**
 * Apps slice — self-service OAuth client registration (Wallow-8w1h.5.1). Mirrors
 * the Organizations slice shape. `register` maps a `RegisterAppRequest` body
 * (`{ clientName, requestedScopes, clientType?, redirectUris? }`) to
 * `AppRegistrationResponse` (the ONLY place the one-time client secret is
 * returned). There is no delete/revoke op for self-service apps.
 */
export interface AppsSlice {
  /** List the caller's developer apps (returns `DeveloperAppResponse[]`). */
  list: () => Promise<unknown>;
  /** Fetch a single app by client id (returns `DeveloperAppResponse`, no secret). */
  get: (clientId: string) => Promise<unknown>;
  /** Register a new app (returns `AppRegistrationResponse` with the one-time secret). */
  register: (body: RegisterAppRequest) => Promise<unknown>;
  /**
   * Upsert an app's optional branding (display name, tagline, logo file) —
   * POST `/v1/identity/apps/{clientId}/branding` (multipart). Mirrors Blazor
   * `AppService.UpsertBrandingAsync` (Wallow-ffpq.3.6).
   */
  upsertBranding: (
    clientId: string,
    body: { displayName?: string; tagline?: string; logo?: File },
  ) => Promise<unknown>;
}

/** Current-user slice (delegates to the SDK's `getUser()`). */
export interface UserSlice {
  me: () => Promise<WallowUser | null>;
}

/**
 * Settings slice (Wallow-8w1h.6.1) — the Settings/Profile feature's data source.
 *
 * RECONCILIATION (scout CRITICAL DIVERGENCE #1): the bead DESIGN wrongly mapped
 * `getV1IdentitySettingsUser`/`putV1IdentitySettingsUser` (GET/PUT
 * `/v1/identity/settings/user`) to profile get/update. Those are a GENERIC
 * tenant-scoped key/value settings store (`ResolvedSetting[]` /
 * `SettingUpdateRequest{key,value}`), NOT a profile endpoint, and the Blazor
 * oracle (`Settings.razor`) never calls them for profile — it renders
 * name/email/roles READ-ONLY off the authenticated `ClaimsPrincipal`. The
 * closest typed read is `getV1IdentityUsersMe()` -> `CurrentUserResponse`
 * (`{ id, email, firstName, lastName, roles, permissions }`). So this slice is
 * READ-ONLY: `getProfile()` wraps `getV1IdentityUsersMe()`. There is NO profile
 * update mutation — no backend endpoint exists to mutate firstName/lastName/
 * email from this surface, so the bead's "profile mutation" is intentionally
 * NOT implemented (SDK-accurate over acceptance-literal).
 */
export interface SettingsSlice {
  /** Read the current user's profile (returns `CurrentUserResponse`). Read-only. */
  getProfile: () => Promise<unknown>;
}

/**
 * MFA slice (Wallow-8w1h.6.3) — the Settings-page MFA status card + enroll flow's
 * data source. Maps the five MFA ops the SPA needs.
 *
 * UNTYPED-RESPONSE GAP (scout): the generated MFA ops all resolve `unknown` bodies
 * (the backend declares no `ProducesResponseType`), so every method here returns
 * `Promise<unknown>` — exactly like the apps one-time-secret slice. The feature's
 * local response interfaces (`src/features/mfa/types.ts`:
 * `MfaStatusResponse`/`MfaEnrollResponse`/`MfaConfirmResponse`/`MfaDisableResponse`/
 * `MfaRegenerateBackupCodesResponse`, mirroring the C# records) are the narrowing
 * boundary the components cast to, rather than leaking `any`.
 *
 * REQUEST-SHAPE RECONCILIATION (scout, over the bead DESIGN): the generated ops
 * REQUIRE typed bodies the terse DESIGN omitted — `confirmEnroll` sends
 * `MfaConfirmRequest{secret,code}` (not just `{code}`), and `disable`/
 * `regenerateBackupCodes` send `MfaDisableRequest`/`MfaRegenerateBackupCodesRequest`
 * `{password}` (not no-body). So those methods take those args.
 */
export interface MfaSlice {
  /** Read MFA status (returns `MfaStatusResponse`). */
  status: () => Promise<unknown>;
  /** Begin TOTP enrollment; returns `MfaEnrollResponse` (`{ secret, qrUri }`). */
  enrollTotp: () => Promise<unknown>;
  /** Confirm enrollment with the TOTP `secret` + user `code`; returns `MfaConfirmResponse`. */
  confirmEnroll: (secret: string, code: string) => Promise<unknown>;
  /** Disable MFA (requires the account `password`); returns `MfaDisableResponse`. */
  disable: (password: string) => Promise<unknown>;
  /** Regenerate backup codes (requires the account `password`); returns `MfaRegenerateBackupCodesResponse`. */
  regenerateBackupCodes: (password: string) => Promise<unknown>;
}

/**
 * Inquiries slice (Wallow-8w1h.7.1) — the Inquiries feature's data source.
 * Mirrors the Organizations slice shape.
 *
 * SDK-ACCURATE MAPPING (scout, over the terse bead DESIGN):
 *  - `list()` wraps `getV1InquiriesSubmitted()` = GET /v1/inquiries/submitted =
 *    the CALLING user's own inquiries (`InquiryResponse[]`). (The admin all-view
 *    `getV1Inquiries()` is intentionally NOT used here.)
 *  - `setStatus()` sends `UpdateInquiryStatusRequest = { newStatus }` — the field
 *    is `newStatus`, NOT `status` as the bead DESIGN said.
 *
 * UNTYPED-RESPONSE GAP (scout, like MFA/apps): `postV1InquiriesByIdComments`
 * resolves `unknown` on 201 (`PostV1InquiriesByIdCommentsResponses = { 201: unknown }`)
 * even though its request body (`AddInquiryCommentRequest`) is typed. So
 * `addComment()` returns `Promise<unknown>`; the add-comment mutation ignores the
 * body and just invalidates the comments query.
 */
export interface InquiriesSlice {
  /** List the caller's submitted inquiries (returns `InquiryResponse[]`). */
  list: () => Promise<unknown>;
  /** Submit a new inquiry (body is `SubmitInquiryRequest`); returns `InquiryResponse`. */
  create: (body: SubmitInquiryRequest) => Promise<unknown>;
  /** Fetch a single inquiry by id (returns `InquiryResponse`; 404 typed as `ProblemDetails`). */
  get: (id: string) => Promise<unknown>;
  /** List an inquiry's comments (returns `InquiryCommentResponse[]`). */
  comments: (id: string) => Promise<unknown>;
  /** Add a comment to an inquiry (body is `AddInquiryCommentRequest`); 201 body is untyped. */
  addComment: (id: string, body: AddInquiryCommentRequest) => Promise<unknown>;
  /** Change an inquiry's status (body is `{ newStatus }`); returns `InquiryResponse`. */
  setStatus: (id: string, newStatus: string) => Promise<unknown>;
}

/**
 * The namespaced facade object. Phases 3-6 each APPEND their own slice here
 * (apps, settings, mfa, inquiries) — see the slice-append pattern above.
 */
export interface WallowSdk {
  organizations: OrganizationsSlice;
  apps: AppsSlice;
  user: UserSlice;
  settings: SettingsSlice;
  mfa: MfaSlice;
  inquiries: InquiriesSlice;
}

const sdk: WallowSdk = {
  organizations: {
    list: () => unwrap(getV1IdentityOrganizations()),
    get: (id: string) => unwrap(getV1IdentityOrganizationsById({ path: { id } })),
    create: (body: { name: string; domain: string | null }) =>
      unwrap(postV1IdentityOrganizations({ body })),
    members: (id: string) => unwrap(getV1IdentityOrganizationsByIdMembers({ path: { id } })),
    addMember: (id: string, body: { userId: string }) =>
      unwrap(postV1IdentityOrganizationsByIdMembers({ path: { id }, body })),
    removeMember: (id: string, userId: string) =>
      unwrap(deleteV1IdentityOrganizationsByIdMembersByUserId({ path: { id, userId } })),
    archive: (id: string) => unwrap(postV1IdentityOrganizationsByIdArchive({ path: { id } })),
    reactivate: (id: string) => unwrap(postV1IdentityOrganizationsByIdReactivate({ path: { id } })),
    clients: (id: string) =>
      unwrap(getV1IdentityClientsByTenantByTenantId({ path: { tenantId: id } })),
    registerClient: (id: string, body: { displayName: string; redirectUris: string[] }) =>
      unwrap(
        postV1IdentityClients({
          body: {
            name: body.displayName,
            redirectUris: body.redirectUris,
            postLogoutRedirectUris: [],
            tenantId: id,
          },
        }),
      ),
  },
  apps: {
    list: () => unwrap(getV1IdentityApps()),
    get: (clientId: string) => unwrap(getV1IdentityAppsByClientId({ path: { clientId } })),
    register: (body: RegisterAppRequest) => unwrap(postV1IdentityAppsRegister({ body })),
    upsertBranding: (
      clientId: string,
      body: { displayName?: string; tagline?: string; logo?: File },
    ) =>
      unwrap(
        postV1IdentityAppsByClientIdBranding({
          path: { clientId },
          body: { DisplayName: body.displayName, Tagline: body.tagline, logo: body.logo },
        }),
      ),
  },
  user: {
    me: () => {
      // During SSR `getUser()` runs under Node's fetch: pass the request's
      // absolute origin (so the URL parses) and forward the session cookie (so
      // the BFF resolves the signed-in user instead of 401ing). In the browser
      // the relative same-origin request with the ambient cookie is correct.
      if (import.meta.env.SSR) {
        const context: SsrRequestContext | undefined = getSsrRequestContext();
        if (context !== undefined) {
          return getUser({
            baseUrl: context.origin,
            ...(context.cookie !== undefined ? { headers: { cookie: context.cookie } } : {}),
          });
        }
      }
      return getUser();
    },
  },
  settings: {
    getProfile: () => unwrap(getV1IdentityUsersMe()),
  },
  mfa: {
    status: () => unwrap(getV1IdentityMfaStatus()),
    enrollTotp: () => unwrap(postV1IdentityMfaEnrollTotp()),
    confirmEnroll: (secret: string, code: string) =>
      unwrap(postV1IdentityMfaEnrollConfirm({ body: { secret, code } })),
    disable: (password: string) => unwrap(postV1IdentityMfaDisable({ body: { password } })),
    regenerateBackupCodes: (password: string) =>
      unwrap(postV1IdentityMfaBackupCodesRegenerate({ body: { password } })),
  },
  inquiries: {
    list: () => unwrap(getV1InquiriesSubmitted()),
    create: (body: SubmitInquiryRequest) => unwrap(postV1Inquiries({ body })),
    get: (id: string) => unwrap(getV1InquiriesById({ path: { id } })),
    comments: (id: string) => unwrap(getV1InquiriesByIdComments({ path: { id } })),
    addComment: (id: string, body: AddInquiryCommentRequest) =>
      unwrap(postV1InquiriesByIdComments({ path: { id }, body })),
    setStatus: (id: string, newStatus: string) =>
      unwrap(patchV1InquiriesByIdStatus({ path: { id }, body: { newStatus } })),
  },
};

/**
 * Return the singleton facade, configuring the BFF client and matching request
 * interceptor on first use. The one-time configure-then-build guard is the SDK's
 * shared {@link createConfiguredOnce} helper.
 */
export const getWallowSdk: () => WallowSdk = createConfiguredOnce(configureClient, () => sdk);
