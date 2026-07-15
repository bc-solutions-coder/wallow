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
  client,
  configureBffClient,
  deleteV1IdentityOrganizationsByIdMembersByUserId,
  getUser,
  getV1IdentityApps,
  getV1IdentityAppsByClientId,
  getV1IdentityOrganizations,
  getV1IdentityOrganizationsById,
  getV1IdentityOrganizationsByIdMembers,
  postV1IdentityAppsRegister,
  postV1IdentityOrganizations,
  postV1IdentityOrganizationsByIdArchive,
  postV1IdentityOrganizationsByIdMembers,
  postV1IdentityOrganizationsByIdReactivate,
  type ProblemDetails,
  type RegisterAppRequest,
  type WallowUser,
} from "@bc-solutions-coder/sdk";

import { wireCsrfInterceptor } from "./csrf";

/**
 * Guarded singleton flag. Kept at module scope so the first `getWallowSdk()`
 * call configures the client + interceptor once and every later call is a
 * cheap no-op.
 */
let configured = false;

/**
 * Configure the BFF client and wire the CSRF interceptor exactly once. Safe to
 * call on every `getWallowSdk()` — after the first call it returns immediately.
 */
function ensureConfigured(): void {
  if (configured) {
    return;
  }
  configureBffClient();
  wireCsrfInterceptor(client);
  configured = true;
}

/** The `{ data, error }` envelope every generated op resolves to. */
interface Envelope<T> {
  data?: T;
  error?: unknown;
}

/**
 * Await a generated op and unwrap its `{ data, error }` envelope: return `data`
 * on success, THROW the `error` (as ProblemDetails) on failure. React Query
 * turns the thrown value into an error state, so slice methods never leak
 * `undefined`.
 */
async function unwrap<T>(p: Promise<Envelope<T>>): Promise<T> {
  const { data, error } = await p;
  if (error !== undefined) {
    throw error as ProblemDetails;
  }
  return data as T;
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
}

/** Current-user slice (delegates to the SDK's `getUser()`). */
export interface UserSlice {
  me: () => Promise<WallowUser | null>;
}

/**
 * The namespaced facade object. Phases 3-6 each APPEND their own slice here
 * (apps, settings, mfa, inquiries) — see the slice-append pattern above.
 */
export interface WallowSdk {
  organizations: OrganizationsSlice;
  apps: AppsSlice;
  user: UserSlice;
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
  },
  apps: {
    list: () => unwrap(getV1IdentityApps()),
    get: (clientId: string) => unwrap(getV1IdentityAppsByClientId({ path: { clientId } })),
    register: (body: RegisterAppRequest) => unwrap(postV1IdentityAppsRegister({ body })),
  },
  user: {
    me: () => getUser(),
  },
};

/**
 * Return the singleton facade, configuring the BFF client and CSRF interceptor
 * on first use.
 */
export function getWallowSdk(): WallowSdk {
  ensureConfigured();
  return sdk;
}
