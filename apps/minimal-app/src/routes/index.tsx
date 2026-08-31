import {
  getCurrentUser,
  isWallowError,
  loginRedirect,
  logout,
  usersGetCurrentUser,
} from "@bc-solutions-coder/sdk";
import { createFileRoute, useRouteContext } from "@tanstack/react-router";
import { useEffect, useState, type ReactElement } from "react";

/** Indentation for the pretty-printed API result. */
const JSON_INDENT = 2;

function describeFailure(error: unknown): string {
  if (!isWallowError(error)) {
    return error instanceof Error ? error.message : "Request failed";
  }
  return `${error.status} ${error.title}${error.detail ? ` — ${error.detail}` : ""}`;
}

function StatusLine({
  status,
  email,
}: {
  readonly status: string;
  readonly email: string;
}): ReactElement {
  return (
    <p>
      Status: <strong data-testid="bff-user-status">{status}</strong>
      {email && " as "}
      {email && <span data-testid="bff-user-email">{email}</span>}
    </p>
  );
}

function SignedInPanel({
  apiResult,
  onCallApi,
}: {
  readonly apiResult: string;
  readonly onCallApi: () => Promise<void>;
}): ReactElement {
  return (
    <>
      <button type="button" data-testid="bff-logout" onClick={() => void logout()}>
        Sign out
      </button>{" "}
      <button type="button" data-testid="bff-call-api" onClick={() => void onCallApi()}>
        Call the API as me
      </button>
      <pre data-testid="bff-api-result">{apiResult}</pre>
    </>
  );
}

/**
 * The whole relying party in one screen: who is signed in, sign in, sign out,
 * one typed API call through the `/api` proxy, one anonymous call through the
 * service account. The `bff-*` testids are what
 * `apps/wallow-web/e2e-cross-app/external-origin-login.spec.ts` drives.
 */
function Home(): ReactElement {
  const { sdk } = useRouteContext({ from: "__root__" });
  const [status, setStatus] = useState<"loading" | "anonymous" | "authenticated">("loading");
  const [email, setEmail] = useState("");
  const [apiResult, setApiResult] = useState("");
  const [contactResult, setContactResult] = useState("");

  useEffect(() => {
    let cancelled = false;
    void getCurrentUser({ client: sdk.client })
      .then((user) => {
        if (cancelled) {
          return;
        }
        setStatus(user === null ? "anonymous" : "authenticated");
        setEmail(user?.email ?? "");
      })
      .catch(() => {
        if (!cancelled) {
          setStatus("anonymous");
        }
      });
    return () => {
      cancelled = true;
    };
  }, [sdk]);

  async function callApi(): Promise<void> {
    setApiResult("…");
    try {
      setApiResult(
        JSON.stringify(await usersGetCurrentUser({ client: sdk.client }), null, JSON_INDENT),
      );
    } catch (error: unknown) {
      setApiResult(describeFailure(error));
    }
  }

  async function sendContact(): Promise<void> {
    setContactResult("…");
    const response = await fetch("/contact", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        name: "Example visitor",
        email: "visitor@example.com",
        message: "hello from the example relying party",
      }),
    });
    setContactResult(`${response.status} ${await response.text()}`);
  }

  const login = loginRedirect("/");

  return (
    <main>
      <h1>Example relying party</h1>
      <StatusLine status={status} email={email} />
      {status === "anonymous" && (
        <a className="button" href={login.href} data-testid="bff-login">
          Sign in with Wallow
        </a>
      )}
      {status === "authenticated" && <SignedInPanel apiResult={apiResult} onCallApi={callApi} />}
      <hr />
      <p>Anonymous action (goes to the API as the service account, not as you):</p>
      <button type="button" data-testid="contact-send" onClick={() => void sendContact()}>
        Send contact message
      </button>
      <pre data-testid="contact-result">{contactResult}</pre>
    </main>
  );
}

export const Route = createFileRoute("/")({ component: Home });
