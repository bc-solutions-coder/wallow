/**
 * Browser entry for the tanstack-min BFF example.
 *
 * Wires the @bc-solutions-coder/sdk browser helpers to the DOM contract in
 * public/index.html and reflects auth state into the `data-testid` elements the
 * E2E test drives:
 *   - bff-user-status   ("anonymous" | "authenticated")
 *   - bff-user-email    (authenticated user's email)
 *   - bff-login         (button -> login())
 *   - bff-logout        (button -> logout())
 *   - bff-call-api      (button -> authed GET through /api)
 *   - bff-api-result    (status/body of the /api call)
 */
import {
  configureBffClient,
  getUser,
  login,
  logout,
  type WallowUser,
} from "@bc-solutions-coder/sdk";

// Point the generated client at the same-origin `/api` BFF proxy and send the
// httpOnly session cookie with every request.
configureBffClient();

function requireElement<T extends HTMLElement>(testId: string): T {
  const element: HTMLElement | null = document.querySelector(
    `[data-testid="${testId}"]`,
  );
  if (element === null) {
    throw new Error(`Missing element with data-testid="${testId}"`);
  }
  return element as T;
}

/** Fetch the current user and paint the status/email elements. */
async function refreshUser(): Promise<void> {
  const status: HTMLElement = requireElement("bff-user-status");
  const emailSpan: HTMLElement = requireElement("bff-user-email");

  const user: WallowUser | null = await getUser();
  if (user === null) {
    status.textContent = "anonymous";
    emailSpan.textContent = "";
    return;
  }

  status.textContent = "authenticated";
  emailSpan.textContent =
    typeof user.email === "string" ? user.email : (user.sub ?? "");
}

/** Call the authenticated `/api` proxy and render the HTTP status. */
async function callApi(): Promise<void> {
  const result: HTMLElement = requireElement("bff-api-result");
  result.textContent = "…";
  try {
    const response: Response = await fetch("/api/v1/identity/users/me", {
      headers: { accept: "application/json" },
      credentials: "include",
    });
    const body: string = await response.text();
    result.textContent = `${response.status} ${body}`;
  } catch (error: unknown) {
    result.textContent = `error ${String(error)}`;
  }
}

function wire(): void {
  requireElement<HTMLButtonElement>("bff-login").addEventListener(
    "click",
    (): void => login("/"),
  );
  requireElement<HTMLButtonElement>("bff-logout").addEventListener(
    "click",
    (): void => logout(),
  );
  requireElement<HTMLButtonElement>("bff-call-api").addEventListener(
    "click",
    (): void => {
      void callApi();
    },
  );

  void refreshUser();
}

if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", wire);
} else {
  wire();
}
