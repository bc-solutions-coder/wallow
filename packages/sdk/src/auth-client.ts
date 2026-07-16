/**
 * `createAuthClient()` — the typed auth facade over the generated identity ops
 * (Wallow-vec7.2.1).
 *
 * FACADE RULE: this module is the ONLY place in the workspace permitted to
 * import the generated `postV1IdentityAuth*` / `getV1IdentityAuth*` ops. App
 * code (apps/wallow-auth routes and components) imports `createAuthClient` and
 * never a raw generated op, so the wire shape stays swappable behind one seam.
 *
 * ENVELOPE CONTRACT: every generated op resolves `{ data, error }` rather than
 * throwing. Each method here unwraps that envelope — returning `data` on
 * success and THROWING a {@link WallowError} built from the RFC 7807 problem
 * details on failure — so callers never have to branch on `error` and never
 * receive `undefined`. This mirrors the `unwrap()` helper in
 * `apps/wallow-web/src/lib/wallow-sdk.ts`, upgraded from "throw the raw
 * ProblemDetails" to "throw a typed WallowError" per this bead's acceptance.
 *
 * WHY `./server/errors`: {@link WallowError} lives under `src/server/` but that
 * module is dependency free (no node/h3/openid-client imports), so importing
 * the file DIRECTLY — never the `./server` barrel, which does pull node deps —
 * is safe from a browser module. `WallowError` is not re-exported from here, so
 * nothing new leaks into the browser entry's export surface.
 *
 * UNTYPED RESPONSES: only `getConsentInfo` and `getClientBranding` have typed
 * response bodies in the OpenAPI spec. The other ops' controller actions carry
 * no `ProducesResponseType`, so the generated types resolve `200: unknown` and
 * those methods return `Promise<unknown>`. Callers narrow at their own boundary
 * (see bd memory `most-packages-sdk-generated-identity-auth-ops-login`) rather
 * than this facade inventing response types the spec does not model.
 */

import {
  type AccountForgotPasswordRequest,
  type AccountLoginRequest,
  type AccountRegisterRequest,
  type AccountResetPasswordRequest,
  type ClientBrandingDto,
  type ConsentInfoResponse,
  type CreateMembershipRequest,
  getV1IdentityAppsByClientIdBranding,
  getV1IdentityAppsConsentInfoByClientId,
  getV1IdentityAuthClientTenantByClientId,
  getV1IdentityAuthExternalProviders,
  getV1IdentityAuthPasswordlessMagicLinkVerify,
  getV1IdentityAuthRedirectUriValidate,
  getV1IdentityAuthVerifyEmail,
  getV1IdentityInvitationsVerifyByToken,
  getV1IdentityOrganizationDomainsMatch,
  type MfaConfirmRequest,
  postV1IdentityAuthForgotPassword,
  postV1IdentityAuthLogin,
  postV1IdentityAuthMfaVerify,
  postV1IdentityAuthPasswordlessMagicLink,
  postV1IdentityAuthPasswordlessOtp,
  postV1IdentityAuthPasswordlessOtpVerify,
  postV1IdentityAuthRegister,
  postV1IdentityAuthResetPassword,
  postV1IdentityInvitationsByTokenAccept,
  postV1IdentityMembershipRequests,
  postV1IdentityMfaEnrollConfirm,
  postV1IdentityMfaEnrollExchangeToken,
  postV1IdentityMfaEnrollTotp,
  type SendMagicLinkRequest,
  type SendOtpRequest,
  type VerifyOtpRequest,
} from "./generated";
import { UNKNOWN_ERROR_CODE, WallowError } from "./server/errors";

/**
 * Options reserved for future per-instance configuration (base URL overrides,
 * custom fetch). The client currently rides the shared `@hey-api` client that
 * the app configures via `configureBffClient`, so every member is optional and
 * `createAuthClient()` with no argument is the normal call.
 */
export interface AuthClientOptions {
  /** Reserved. No per-instance options are honoured yet. */
  readonly reserved?: never;
}

/**
 * The auth facade. One method per identity endpoint the wallow-auth app drives.
 *
 * Methods returning `Promise<unknown>` wrap ops whose response bodies the
 * OpenAPI spec does not type (see the module header).
 */
export interface AuthClient {
  /** Password sign-in. Body is `AccountLoginRequest`. Response body is untyped. */
  login: (body: AccountLoginRequest) => Promise<unknown>;
  /** Self-service account registration. Response body is untyped. */
  register: (body: AccountRegisterRequest) => Promise<unknown>;
  /** Send a password-reset email. Response body is untyped. */
  forgotPassword: (body: AccountForgotPasswordRequest) => Promise<unknown>;
  /** Complete a password reset with the emailed token. Response body is untyped. */
  resetPassword: (body: AccountResetPasswordRequest) => Promise<unknown>;
  /** Confirm an email address from the emailed link. Response body is untyped. */
  verifyEmail: (query: { email: string; token: string }) => Promise<unknown>;
  /** Email a passwordless magic link. Response body is untyped. */
  sendMagicLink: (body: SendMagicLinkRequest) => Promise<unknown>;
  /** Redeem a magic-link token. Response body is untyped. */
  verifyMagicLink: (query: { token: string; rememberMe?: boolean }) => Promise<unknown>;
  /** Email a one-time passcode. Response body is untyped. */
  sendOtp: (body: SendOtpRequest) => Promise<unknown>;
  /** Redeem a one-time passcode. Response body is untyped. */
  verifyOtp: (body: VerifyOtpRequest) => Promise<unknown>;
  /**
   * Verify a TOTP code at the MFA challenge. Sends `useBackupCode: false`.
   * Response body is untyped.
   */
  verifyMfa: (code: string) => Promise<unknown>;
  /**
   * Verify a recovery code at the MFA challenge. There is NO separate backup-code
   * endpoint — this is the same op as {@link AuthClient.verifyMfa} with
   * `useBackupCode: true`. Response body is untyped.
   */
  useBackupCode: (code: string) => Promise<unknown>;
  /** Begin TOTP enrollment (no body). Response body is untyped. */
  enrollTotp: () => Promise<unknown>;
  /** Confirm TOTP enrollment with `{ secret, code }`. Response body is untyped. */
  confirmEnrollment: (body: MfaConfirmRequest) => Promise<unknown>;
  /** Exchange an enrollment ticket for a session (token is a query param). Untyped. */
  exchangeEnrollmentToken: (token: string) => Promise<unknown>;
  /**
   * Fetch the consent screen's client + requested-scope details. TYPED response.
   * `scopes` is sent as a comma-joined query STRING, not repeated params.
   */
  getConsentInfo: (clientId: string, scopes?: readonly string[]) => Promise<ConsentInfoResponse>;
  /** List configured external identity providers. Response body is untyped. */
  getExternalProviders: () => Promise<unknown>;
  /** Resolve the tenant an OAuth client belongs to. Response body is untyped. */
  getClientTenant: (clientId: string) => Promise<unknown>;
  /** Fetch per-client branding for the auth screens. TYPED response. */
  getClientBranding: (clientId: string) => Promise<ClientBrandingDto>;
  /** Verify an invitation token. Response body is untyped. */
  verifyInvitation: (token: string) => Promise<unknown>;
  /** Accept an invitation by token (no body). Response body is untyped. */
  acceptInvitation: (token: string) => Promise<unknown>;
  /** Find the organization matching an email's domain. Response body is untyped. */
  getMatchingOrgByDomain: (email: string) => Promise<unknown>;
  /** Request membership of a domain-matched organization. Response body is untyped. */
  requestMembership: (body: CreateMembershipRequest) => Promise<unknown>;
  /**
   * Ask the API whether a redirect URI is registered for the current client.
   *
   * This is the SERVER-authoritative check against the client's registered
   * redirect URIs. It is distinct from `auth-oidc`'s client-side
   * `isSafeReturnUrl` guard (Wallow-vec7.2.2), which is a pure local
   * open-redirect check with no generated-op backing. Response body is untyped.
   */
  validateRedirectUri: (uri: string) => Promise<unknown>;
}

/**
 * Build an auth facade over the generated identity ops.
 *
 * @param options Reserved; see {@link AuthClientOptions}.
 */
export function createAuthClient(options?: AuthClientOptions): AuthClient {
  void options;

  return {
    login: (body: AccountLoginRequest) => unwrap(postV1IdentityAuthLogin({ body })),
    register: (body: AccountRegisterRequest) => unwrap(postV1IdentityAuthRegister({ body })),
    forgotPassword: (body: AccountForgotPasswordRequest) =>
      unwrap(postV1IdentityAuthForgotPassword({ body })),
    resetPassword: (body: AccountResetPasswordRequest) =>
      unwrap(postV1IdentityAuthResetPassword({ body })),
    verifyEmail: (query: { email: string; token: string }) =>
      unwrap(getV1IdentityAuthVerifyEmail({ query })),
    sendMagicLink: (body: SendMagicLinkRequest) =>
      unwrap(postV1IdentityAuthPasswordlessMagicLink({ body })),
    verifyMagicLink: (query: { token: string; rememberMe?: boolean }) =>
      unwrap(getV1IdentityAuthPasswordlessMagicLinkVerify({ query })),
    sendOtp: (body: SendOtpRequest) => unwrap(postV1IdentityAuthPasswordlessOtp({ body })),
    verifyOtp: (body: VerifyOtpRequest) =>
      unwrap(postV1IdentityAuthPasswordlessOtpVerify({ body })),
    verifyMfa: (code: string) =>
      unwrap(postV1IdentityAuthMfaVerify({ body: { code, useBackupCode: false } })),
    useBackupCode: (code: string) =>
      unwrap(postV1IdentityAuthMfaVerify({ body: { code, useBackupCode: true } })),
    enrollTotp: () => unwrap(postV1IdentityMfaEnrollTotp()),
    confirmEnrollment: (body: MfaConfirmRequest) =>
      unwrap(postV1IdentityMfaEnrollConfirm({ body })),
    exchangeEnrollmentToken: (token: string) =>
      unwrap(postV1IdentityMfaEnrollExchangeToken({ query: { token } })),
    getConsentInfo: (clientId: string, scopes?: readonly string[]) =>
      unwrap<ConsentInfoResponse>(
        getV1IdentityAppsConsentInfoByClientId(
          // The API takes ONE comma-joined `scopes` string, not repeated params.
          // Omitting the key entirely (rather than sending `scopes: undefined`)
          // keeps the query string clean when no scopes were requested.
          scopes?.length
            ? { path: { clientId }, query: { scopes: scopes.join(",") } }
            : { path: { clientId } },
        ),
      ),
    getExternalProviders: () => unwrap(getV1IdentityAuthExternalProviders()),
    getClientTenant: (clientId: string) =>
      unwrap(getV1IdentityAuthClientTenantByClientId({ path: { clientId } })),
    getClientBranding: (clientId: string) =>
      unwrap<ClientBrandingDto>(getV1IdentityAppsByClientIdBranding({ path: { clientId } })),
    verifyInvitation: (token: string) =>
      unwrap(getV1IdentityInvitationsVerifyByToken({ path: { token } })),
    acceptInvitation: (token: string) =>
      unwrap(postV1IdentityInvitationsByTokenAccept({ path: { token } })),
    getMatchingOrgByDomain: (email: string) =>
      unwrap(getV1IdentityOrganizationDomainsMatch({ query: { email } })),
    requestMembership: (body: CreateMembershipRequest) =>
      unwrap(postV1IdentityMembershipRequests({ body })),
    validateRedirectUri: (uri: string) =>
      unwrap(getV1IdentityAuthRedirectUriValidate({ query: { uri } })),
  };
}

/**
 * The `{ data, error }` envelope every generated op resolves to. `response` is
 * the raw `Response`, used only to recover a status when the problem details
 * omit one.
 */
interface Envelope<TData> {
  readonly data?: TData;
  readonly error?: unknown;
  readonly response?: { readonly status?: number };
}

/** Status attributed to a failure that names no status anywhere. */
const FALLBACK_ERROR_STATUS: number = 500;

/** Title used when the error body carries no problem details. */
const UNKNOWN_ERROR_TITLE: string = "Unknown error";

/**
 * Await a generated op and unwrap its `{ data, error }` envelope: return `data`
 * on success, throw a {@link WallowError} built from the problem details on
 * failure.
 *
 * Only a defined `error` throws — a `null` data body is a legitimate success
 * (204-shaped responses) and is returned as-is.
 */
async function unwrap<TData>(pending: Promise<Envelope<TData>>): Promise<TData> {
  const envelope: Envelope<TData> = await pending;

  if (envelope.error !== undefined) {
    throw toWallowError(envelope.error, envelope.response?.status);
  }

  return envelope.data as TData;
}

/**
 * Map an envelope error onto a {@link WallowError}.
 *
 * The envelope hands us an ALREADY-PARSED error, so `parseProblemDetails` (which
 * takes a `Response` + body text) does not apply; this mirrors its field mapping
 * against a parsed object instead. The error is not guaranteed to be problem
 * details — it can be a bare string or `{}` — so every member is probed.
 */
function toWallowError(error: unknown, responseStatus: number | undefined): WallowError {
  const problem: Record<string, unknown> = isPlainObject(error) ? error : {};
  const status: unknown = problem["status"];
  const title: unknown = problem["title"];
  const detail: unknown = problem["detail"];

  return new WallowError({
    status: typeof status === "number" ? status : (responseStatus ?? FALLBACK_ERROR_STATUS),
    code: readCode(problem) ?? UNKNOWN_ERROR_CODE,
    title: typeof title === "string" ? title : UNKNOWN_ERROR_TITLE,
    detail: typeof detail === "string" ? detail : undefined,
  });
}

/**
 * Recover the machine-readable error code from a parsed error body.
 *
 * Three placements are probed, most authoritative first:
 * 1. `extensions.code` — RFC 7807 as ASP.NET Core emits it;
 * 2. `code` — the same, for serializer setups that flatten extension members;
 * 3. `error` — the Identity auth endpoints (`/v1/identity/auth/*`) do NOT emit
 *    problem details at all; they return a bare `{ succeeded: false, error }`
 *    anonymous object (see AccountController), so the code arrives under
 *    `error`. Probed last so real problem details always win.
 *
 * Non-string members are ignored rather than coerced: OAuth-style bodies can
 * carry an object under `error`, and a stringified object is not a code.
 */
function readCode(problem: Record<string, unknown>): string | undefined {
  const extensions: unknown = problem["extensions"];
  if (isPlainObject(extensions) && typeof extensions["code"] === "string") {
    return extensions["code"];
  }

  const code: unknown = problem["code"];
  if (typeof code === "string") {
    return code;
  }

  const error: unknown = problem["error"];
  return typeof error === "string" ? error : undefined;
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
