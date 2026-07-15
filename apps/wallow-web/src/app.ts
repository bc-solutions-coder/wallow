/**
 * Browser entry for the tanstack-min BFF example.
 *
 * Shows the three things every BFF browser client has to get right:
 *   1. `configureBffClient()` — point the generated typed operations at the
 *      same-origin `/api` proxy and send the httpOnly session cookie.
 *   2. A CSRF request interceptor — the BFF rejects any state-changing request
 *      (POST/PUT/PATCH/DELETE) that does not echo the session's CSRF token in
 *      the `x-csrf-token` header. Safe methods are exempt.
 *   3. Typed errors — generated operations resolve to `{ data, error }` and the
 *      BFF reports failures as RFC 7807 problem+json, so nothing here throws on
 *      a non-2xx response.
 *
 * It reflects auth state into the `data-testid` elements the E2E test drives:
 *   - bff-user-status   ("anonymous" | "authenticated")
 *   - bff-user-email    (authenticated user's email)
 *   - bff-login         (button -> login())
 *   - bff-logout        (button -> logout())
 *   - bff-call-api      (button -> typed GET through /api)
 *   - bff-mutate        (button -> typed POST through /api, carrying the CSRF token)
 *   - bff-api-result    (result of the last safe /api call)
 *   - bff-mutate-result (result of the last state-changing /api call)
 */
import {
  client,
  configureBffClient,
  getUser,
  getV1IdentityUsersMe,
  login,
  logout,
  postV1IdentityOrganizations,
  type ProblemDetails,
  type WallowUser,
} from "@bc-solutions-coder/sdk";

import { setCsrfToken, wireCsrfInterceptor } from "./lib/csrf";

// Point the generated client at the same-origin `/api` BFF proxy and send the
// httpOnly session cookie with every request. Every generated operation below
// (getV1IdentityUsersMe, postV1IdentityAuthKeys, ...) calls through this one
// shared client, so this is the only place the transport is configured.
configureBffClient();

// Echo the CSRF token on every state-changing request. Without this the proxy
// answers 403 `CSRF_INVALID` and the request never reaches the API — which is
// exactly what stops a cross-site form post from riding on the session cookie.
// The token store and interceptor live in `./lib/csrf`; here we just wire it
// onto the shared client and keep the store in sync with `/bff/user`.
wireCsrfInterceptor(client);

function requireElement<T extends HTMLElement>(testId: string): T {
  const element: HTMLElement | null = document.querySelector(`[data-testid="${testId}"]`);
  if (element === null) {
    throw new Error(`Missing element with data-testid="${testId}"`);
  }
  return element as T;
}

/**
 * Render a failed operation. The BFF and the API both answer with RFC 7807
 * problem+json, so `error` is a {@link ProblemDetails} whenever the body parsed;
 * fall back to the raw status when it did not.
 */
function renderFailure(target: HTMLElement, response: Response, error: unknown): void {
  const problem: ProblemDetails = (error ?? {}) as ProblemDetails;
  const title: string = problem.title ?? response.statusText ?? "Request failed";
  const detail: string = problem.detail ?? "";
  target.textContent = `${response.status} ${title}${detail === "" ? "" : ` — ${detail}`}`;
}

/** Fetch the current user, cache the CSRF token, and paint status/email. */
async function refreshUser(): Promise<void> {
  const status: HTMLElement = requireElement("bff-user-status");
  const emailSpan: HTMLElement = requireElement("bff-user-email");

  const user: WallowUser | null = await getUser();
  if (user === null) {
    setCsrfToken(null);
    status.textContent = "anonymous";
    emailSpan.textContent = "";
    return;
  }

  // `/bff/user` returns the identity claims plus the session's CSRF token.
  setCsrfToken(typeof user.csrfToken === "string" ? user.csrfToken : null);

  status.textContent = "authenticated";
  emailSpan.textContent = typeof user.email === "string" ? user.email : (user.sub ?? "");
}

/**
 * A safe (GET) call through the `/api` proxy, using a generated typed operation
 * rather than raw `fetch`. The proxy attaches the Bearer token and refreshes it
 * silently when it has expired; no CSRF token is needed on a GET.
 */
async function callApi(): Promise<void> {
  const result: HTMLElement = requireElement("bff-api-result");
  result.textContent = "…";

  const { data, error, response } = await getV1IdentityUsersMe();
  if (error !== undefined) {
    renderFailure(result, response, error);
    return;
  }

  result.textContent = `${response.status} ${JSON.stringify(data)}`;
}

/**
 * A state-changing (POST) call through the `/api` proxy. This is the request the
 * CSRF interceptor above exists for: strip the `x-csrf-token` header and the BFF
 * rejects it with 403 `CSRF_INVALID` before the API ever sees it.
 *
 * Creating an organization is granted to an ordinary signed-in user, so the `201`
 * it returns means the request cleared the CSRF gate AND reached the API — the
 * end-to-end proof that the token was sent and accepted.
 */
async function mutateApi(): Promise<void> {
  const result: HTMLElement = requireElement("bff-mutate-result");
  result.textContent = "…";

  const { data, error, response } = await postV1IdentityOrganizations({
    body: { name: `tanstack-min demo ${Date.now()}`, domain: null },
  });
  if (error !== undefined || data === undefined) {
    renderFailure(result, response, error);
    return;
  }

  result.textContent = `${response.status} created org ${data.organizationId}`;
}

function wire(): void {
  requireElement<HTMLButtonElement>("bff-login").addEventListener("click", (): void => login("/"));
  requireElement<HTMLButtonElement>("bff-logout").addEventListener("click", (): void => logout());
  requireElement<HTMLButtonElement>("bff-call-api").addEventListener("click", (): void => {
    void callApi();
  });
  requireElement<HTMLButtonElement>("bff-mutate").addEventListener("click", (): void => {
    void mutateApi();
  });

  void refreshUser();
}

if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", wire);
} else {
  wire();
}
