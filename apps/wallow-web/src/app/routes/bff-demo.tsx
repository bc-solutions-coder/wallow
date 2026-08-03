import {
  getUser,
  isWallowError,
  login,
  logout,
  organizationsCreate,
  setCsrfToken,
  usersGetCurrentUser,
  type WallowUser,
} from "@bc-solutions-coder/sdk";
import { Text } from "@bc-solutions-coder/ui";
import { log } from "@shared/lib/log";
import { createFileRoute, useRouteContext } from "@tanstack/react-router";
import { useEffect, useState } from "react";

/**
 * The dedicated `/bff-demo` route (Wallow-8w1h.8.2) — the React port of the
 * vanilla-DOM BFF example that used to live in `src/app.ts` behind a static
 * `public/index.html` host page. Both are deleted: under TanStack Start `/` is an
 * SSR route, and a static `index.html` in the public assets would shadow it. This
 * route is now the only BFF demo surface.
 *
 * It preserves the `bff-*` `data-testid` contract the C# `BffFlowTests`
 * (api/tests/Wallow.E2E.Tests/Flows/BffFlowTests.cs) drives:
 *   - bff-user-status   ("anonymous" | "authenticated")
 *   - bff-user-email    (authenticated user's email)
 *   - bff-login         (button -> login("/"))
 *   - bff-logout        (button -> logout())
 *   - bff-call-api      (button -> GET usersGetCurrentUser() through /api)
 *   - bff-mutate        (button -> POST organizationsCreate() with CSRF)
 *   - bff-api-result    (result of the last safe /api call)
 *   - bff-mutate-result (result of the last state-changing /api call)
 *
 * The two result surfaces no longer print the wire status on success. A
 * generated operation resolves the response BODY and rejects on anything else,
 * so "it resolved" IS the success signal; a status is only meaningful on the
 * failure path, where the thrown `WallowError` still carries it.
 *
 * Living at `/bff-demo` (rather than overwriting `src/routes/index.tsx`, which
 * owns the `home-heading` SSR contract) keeps both surfaces intact. As the raw
 * BFF example it calls the generated operations DIRECTLY, binding each to the
 * request's SDK instance off the router context — there is no module-global
 * client to configure any more, and the instance wires the CSRF interceptor
 * itself. Retargeting the Docker `bff-example` container to this route is Phase
 * 8's job.
 */

/**
 * Render a rejected operation as a string. Every failure arrives as the SDK's
 * `WallowError`, which already carries the status and the RFC 7807 title/detail
 * the BFF or the API sent; anything unbranded gets its own message.
 */
function describeFailure(error: unknown): string {
  if (!isWallowError(error)) {
    return error instanceof Error ? error.message : "Request failed";
  }

  const detail: string = error.detail ?? "";
  return `${error.status} ${error.title}${detail === "" ? "" : ` — ${detail}`}`;
}

/**
 * End the session. `logout()` POSTs to the CSRF-gated `/bff/logout` and
 * navigates on the redirect it answers with, so it can reject (403 CSRF, 405)
 * with the session still live — which must not surface as an unhandled rejection.
 */
async function handleLogout(): Promise<void> {
  try {
    await logout();
  } catch (error: unknown) {
    // The session may still be live, so this is the record that explains a user
    // who "logged out" and is still signed in on the next page.
    log.error("bff.logout.failed", {}, error);
  }
}

function BffDemoComponent() {
  const { sdk } = useRouteContext({ from: "__root__" });
  const [status, setStatus] = useState<"anonymous" | "authenticated">("anonymous");
  const [email, setEmail] = useState("");
  const [apiResult, setApiResult] = useState("");
  const [mutateResult, setMutateResult] = useState("");

  // Reflect the current auth state into the status/email surface on mount
  // (browser only — this route is part of the SSR tree). Nothing is configured
  // here any more: the request's SDK instance already owns its transport and its
  // CSRF interceptor.
  useEffect(() => {
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

    try {
      setApiResult(`ok ${JSON.stringify(await usersGetCurrentUser({ client: sdk.client }))}`);
    } catch (error: unknown) {
      setApiResult(describeFailure(error));
    }
  }

  /**
   * A state-changing (POST) call through the `/api` proxy — the request the CSRF
   * interceptor exists for. Creating an organization is granted to an ordinary
   * signed-in user, so a resolved call proves the token cleared the CSRF gate AND
   * the request reached the API.
   */
  async function handleMutate(): Promise<void> {
    setMutateResult("…");

    try {
      const created = await organizationsCreate({
        client: sdk.client,
        body: { name: `tanstack-min demo ${Date.now()}`, domain: null },
      });
      setMutateResult(`created org ${created.organizationId}`);
    } catch (error: unknown) {
      setMutateResult(describeFailure(error));
    }
  }

  return (
    <main>
      <Text as="h1" variant="display">
        Wallow BFF example
      </Text>

      <Text as="p">
        Status:{" "}
        <Text as="span" data-testid="bff-user-status">
          {status}
        </Text>
      </Text>
      <Text as="p">
        Signed in as:{" "}
        <Text as="span" data-testid="bff-user-email">
          {email}
        </Text>
      </Text>

      <button type="button" data-testid="bff-login" onClick={() => login("/")}>
        Sign in
      </button>
      <button
        type="button"
        data-testid="bff-logout"
        onClick={() => {
          void handleLogout();
        }}
      >
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
