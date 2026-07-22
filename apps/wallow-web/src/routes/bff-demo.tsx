import {
  client,
  configureBffClient,
  getUser,
  getV1IdentityUsersMe,
  login,
  logout,
  postV1IdentityOrganizations,
  setCsrfToken,
  wireCsrfInterceptor,
  type ProblemDetails,
  type WallowUser,
} from "@bc-solutions-coder/sdk";
import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useState } from "react";

/**
 * The dedicated `/bff-demo` route (Wallow-8w1h.8.2) — the React port of the
 * vanilla-DOM BFF example (`src/app.ts` + `public/index.html`).
 *
 * It preserves the `bff-*` `data-testid` contract the C# `BffFlowTests`
 * (api/tests/Wallow.E2E.Tests/Flows/BffFlowTests.cs) drives:
 *   - bff-user-status   ("anonymous" | "authenticated")
 *   - bff-user-email    (authenticated user's email)
 *   - bff-login         (button -> login("/"))
 *   - bff-logout        (button -> logout())
 *   - bff-call-api      (button -> GET getV1IdentityUsersMe() through /api)
 *   - bff-mutate        (button -> POST postV1IdentityOrganizations() with CSRF)
 *   - bff-api-result    (result of the last safe /api call)
 *   - bff-mutate-result (result of the last state-changing /api call)
 *
 * Living at `/bff-demo` (rather than overwriting `src/routes/index.tsx`, which
 * owns the `home-heading` SSR contract) keeps both surfaces intact. As the raw
 * BFF example, it imports the generated ops DIRECTLY from
 * `@bc-solutions-coder/sdk` — exempt from the `getWallowSdk()`-only convention.
 * Retargeting the Docker `bff-example` container to this route is Phase 8's job.
 */

/**
 * Render a failed operation as a string. The BFF and the API both answer with
 * RFC 7807 problem+json, so `error` is a {@link ProblemDetails} whenever the
 * body parsed; fall back to the raw status when it did not.
 */
function describeFailure(response: Response, error: unknown): string {
  const problem: ProblemDetails = (error ?? {}) as ProblemDetails;
  const title: string = problem.title ?? response.statusText ?? "Request failed";
  const detail: string = problem.detail ?? "";
  return `${response.status} ${title}${detail === "" ? "" : ` — ${detail}`}`;
}

function BffDemoComponent() {
  const [status, setStatus] = useState<"anonymous" | "authenticated">("anonymous");
  const [email, setEmail] = useState("");
  const [apiResult, setApiResult] = useState("");
  const [mutateResult, setMutateResult] = useState("");

  // Configure the same-origin `/api` transport and wire the CSRF interceptor
  // once on mount (browser only — this route is now part of the SSR tree), then
  // reflect the current auth state into the status/email surface.
  useEffect(() => {
    configureBffClient();
    wireCsrfInterceptor(client);

    let cancelled = false;

    async function refreshUser(): Promise<void> {
      const user: WallowUser | null = await getUser();
      if (cancelled) {
        return;
      }

      if (user === null) {
        setCsrfToken(null);
        setStatus("anonymous");
        setEmail("");
        return;
      }

      // `/bff/user` returns the identity claims plus the session's CSRF token;
      // arm the interceptor with it before any mutate call.
      setCsrfToken(typeof user.csrfToken === "string" ? user.csrfToken : null);
      setStatus("authenticated");
      setEmail(typeof user.email === "string" ? user.email : (user.sub ?? ""));
    }

    void refreshUser();

    return () => {
      cancelled = true;
    };
  }, []);

  /**
   * A safe (GET) call through the `/api` proxy, using a generated typed
   * operation rather than raw `fetch`. No CSRF token is needed on a GET.
   */
  async function handleCallApi(): Promise<void> {
    setApiResult("…");

    const { data, error, response } = await getV1IdentityUsersMe();
    if (error !== undefined) {
      setApiResult(describeFailure(response, error));
      return;
    }

    setApiResult(`${response.status} ${JSON.stringify(data)}`);
  }

  /**
   * A state-changing (POST) call through the `/api` proxy — the request the CSRF
   * interceptor exists for. Creating an organization is granted to an ordinary
   * signed-in user, so the `201` proves the token cleared the CSRF gate AND the
   * request reached the API.
   */
  async function handleMutate(): Promise<void> {
    setMutateResult("…");

    const { data, error, response } = await postV1IdentityOrganizations({
      body: { name: `tanstack-min demo ${Date.now()}`, domain: null },
    });
    if (error !== undefined || data === undefined) {
      setMutateResult(describeFailure(response, error));
      return;
    }

    setMutateResult(`${response.status} created org ${data.organizationId}`);
  }

  return (
    <main>
      <h1>Wallow BFF example</h1>

      <p>
        Status: <span data-testid="bff-user-status">{status}</span>
      </p>
      <p>
        Signed in as: <span data-testid="bff-user-email">{email}</span>
      </p>

      <button type="button" data-testid="bff-login" onClick={() => login("/")}>
        Sign in
      </button>
      <button type="button" data-testid="bff-logout" onClick={() => logout()}>
        Sign out
      </button>
      <button
        type="button"
        data-testid="bff-call-api"
        onClick={() => {
          void handleCallApi();
        }}
      >
        Call API (GET)
      </button>
      <button
        type="button"
        data-testid="bff-mutate"
        onClick={() => {
          void handleMutate();
        }}
      >
        Create org (POST, sends CSRF token)
      </button>

      <pre data-testid="bff-api-result">{apiResult}</pre>
      <pre data-testid="bff-mutate-result">{mutateResult}</pre>
    </main>
  );
}

export const Route = createFileRoute("/bff-demo")({
  component: BffDemoComponent,
});
