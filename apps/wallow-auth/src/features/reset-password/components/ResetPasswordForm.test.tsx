import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from "@tanstack/react-router";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as resetPasswordRoute } from "../../../routes/reset-password";
import { ResetPasswordForm } from "./ResetPasswordForm";

/**
 * Component spec for the ResetPassword screen (Wallow-vec7.3.2).
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `reset-password-error`, `reset-password-new-password`, `reset-password-confirm`,
 * `reset-password-submit`.
 *
 * MOCKING SEAM: `../../../lib/wallow-auth-sdk` — the app's own facade, never
 * `@bc-solutions-coder/sdk` directly (that module is the only permitted importer
 * of the SDK). Per bd memories `vitest-resetmodules-breaks-instanceof-across-
 * graphs`, this file uses a plain `vi.mock` factory + `vi.hoisted` spies and
 * NEVER `vi.resetModules()`.
 *
 * ── THE ERROR-BRANCH FINDING (verified against the source, not assumed) ───────
 *
 * The oracle switches its message on `result.Error`:
 *
 *     "invalid_token" => "This reset link is invalid or has expired..."
 *     _               => "Failed to reset password. Please try again."
 *
 * That switch CANNOT be ported as written. `AccountController.ResetPassword`
 * (api/.../Controllers/AccountController.cs:771-794) returns its failures as
 * **`BadRequest(new { succeeded = false, error = "invalid_token" })`** — a 400
 * whose body is a bare anon object, NOT RFC 7807 problem details. The error
 * string does not survive the TS seam: `unwrap()` THROWS on any non-2xx, and
 * `toWallowError()` (packages/sdk/src/auth-client.ts:257-280) builds its `code`
 * from `extensions.code` ?? `code` only — it never reads a top-level `error`
 * field. So `{ succeeded: false, error: "invalid_token" }` arrives as
 * `WallowError{ code: "UNKNOWN", title: "Unknown error", detail: undefined }`.
 * The reason string is LOST at the seam (bd memory `wallow-auth-auth-client-ts-
 * wallowerror-code-loss`).
 *
 * What survives is the HTTP status — and that is enough here, because this
 * endpoint has exactly two failure returns and BOTH are
 * `400 + error: "invalid_token"` (unknown email, and a rejected
 * `ResetPasswordAsync`). A 400 from this endpoint therefore *means*
 * invalid_token, and the oracle's two branches map cleanly onto status:
 *
 *     400          -> "This reset link is invalid or has expired..."
 *     anything else -> "Failed to reset password. Please try again."
 *
 * The screen must narrow on `status` STRUCTURALLY (`error.status === 400`)
 * rather than with `instanceof WallowError`: `WallowError` is exported from the
 * SDK's `./server` entry, and screens may not import from the SDK at all.
 *
 * Consequently the rejection fixtures below are WallowError-SHAPED objects
 * (`status`/`code`/`title`), matching what the real facade throws — including
 * the `code: "UNKNOWN"` that proves the port is not secretly relying on a code
 * the seam never delivers.
 */

// Hoisted so the vi.mock factories and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  resetPassword: vi.fn(),
  navigate: vi.fn(),
}));

vi.mock("../../../lib/wallow-auth-sdk", () => ({
  getWallowAuthSdk: () => ({
    auth: { resetPassword: mocks.resetPassword },
    oidc: {},
  }),
}));

vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const EMAIL = "ada@example.com";
const TOKEN = "reset-token-abc";
const PASSWORD = "N3w-Passw0rd!";

/** What the facade really throws for this endpoint's 400 — reason string already lost. */
function invalidTokenRejection(): Error & { status: number; code: string } {
  return Object.assign(new Error("Unknown error"), {
    name: "WallowError",
    status: 400,
    code: "UNKNOWN",
    title: "Unknown error",
  });
}

/** A non-400 failure (a 500, say) — the oracle's generic branch. */
function serverErrorRejection(): Error & { status: number; code: string } {
  return Object.assign(new Error("Unknown error"), {
    name: "WallowError",
    status: 500,
    code: "UNKNOWN",
    title: "Unknown error",
  });
}

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(ui: ReactElement) {
  return render(<QueryClientProvider client={newClient()}>{ui}</QueryClientProvider>);
}

/** Render the screen as a valid reset link would: both query params present. */
function renderForm(props: Partial<{ email?: string; token?: string }> = {}) {
  return renderWithClient(<ResetPasswordForm email={EMAIL} token={TOKEN} {...props} />);
}

/** Type both password fields and submit — the whole happy interaction. */
async function submitPasswords(
  user: ReturnType<typeof userEvent.setup>,
  newPassword: string = PASSWORD,
  confirmPassword: string = newPassword,
) {
  if (newPassword !== "") {
    await user.type(page.getByTestId("reset-password-new-password"), newPassword);
  }
  if (confirmPassword !== "") {
    await user.type(page.getByTestId("reset-password-confirm"), confirmPassword);
  }
  await user.click(page.getByTestId("reset-password-submit"));
}

beforeEach(() => {
  vi.clearAllMocks();
  mocks.resetPassword.mockResolvedValue({ succeeded: true });
});

describe("ResetPasswordForm", () => {
  it("renders the oracle's fields, and no error before submit", async () => {
    renderForm();

    await expect.element(page.getByTestId("reset-password-new-password")).toBeInTheDocument();
    await expect.element(page.getByTestId("reset-password-confirm")).toBeInTheDocument();
    await expect.element(page.getByTestId("reset-password-submit")).toBeInTheDocument();
    expect(page.getByTestId("reset-password-error").query()).toBeNull();
  });

  it("masks both password fields", async () => {
    // Oracle: both inputs are `type="password"`. A reset form that echoed the
    // new password in plain text would be a real regression, so it is pinned.
    renderForm();

    await expect
      .element(page.getByTestId("reset-password-new-password"))
      .toHaveAttribute("type", "password");
    await expect
      .element(page.getByTestId("reset-password-confirm"))
      .toHaveAttribute("type", "password");
  });

  it("links back to sign in", async () => {
    // The card footer. It has no testid and the scout's inventory forbids
    // inventing one for an element that shipped without one, so this asserts the
    // link by role + href instead.
    renderForm();

    await expect
      .element(page.getByRole("link", { name: /back to sign in/iu }))
      .toHaveAttribute("href", "/login");
  });

  it("sends the query's email and token with the typed password", async () => {
    // The threading criterion: the reset link's identity comes from the URL, the
    // secret from the form. Oracle: `new ResetPasswordRequest(Email, Token, _newPassword)`.
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user);

    await vi.waitFor(() => {
      expect(mocks.resetPassword).toHaveBeenCalledWith({
        email: EMAIL,
        token: TOKEN,
        newPassword: PASSWORD,
      });
    });
  });

  it("redirects to the login page with the password_reset notice on success", async () => {
    // Oracle: `Navigation.NavigateTo("/login?message=password_reset")`. `href` (a
    // raw location) rather than `to` + `search`: /login's `validateSearch` is
    // owned by the in-flight Login task, and this screen must not couple to it
    // (bd memory tanstack-router-redirect-to-an-unregistered-route-use-href-not-to).
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: "/login?message=password_reset" });
    });
  });

  it("rejects a mismatched confirmation without calling the endpoint", async () => {
    // Oracle: `if (_newPassword != _confirmPassword) { _error = "Passwords do not
    // match."; return; }` — the guard is client-side and short-circuits.
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user, PASSWORD, "something-else");

    await expect
      .element(page.getByTestId("reset-password-error"))
      .toHaveTextContent(/passwords do not match/iu);
    expect(mocks.resetPassword).not.toHaveBeenCalled();
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("refuses to submit a link with no token", async () => {
    // Oracle: `if (string.IsNullOrEmpty(Token) || string.IsNullOrEmpty(Email))`.
    const user = userEvent.setup();
    renderForm({ token: undefined });

    await submitPasswords(user);

    await expect
      .element(page.getByTestId("reset-password-error"))
      .toHaveTextContent(/invalid reset link/iu);
    expect(mocks.resetPassword).not.toHaveBeenCalled();
  });

  it("refuses to submit a link with no email", async () => {
    const user = userEvent.setup();
    renderForm({ email: undefined });

    await submitPasswords(user);

    await expect
      .element(page.getByTestId("reset-password-error"))
      .toHaveTextContent(/invalid reset link/iu);
    expect(mocks.resetPassword).not.toHaveBeenCalled();
  });

  it("treats an empty-string token as a missing one", async () => {
    // `IsNullOrEmpty`, not `is null` — `?token=` must not reach the endpoint.
    const user = userEvent.setup();
    renderForm({ token: "" });

    await submitPasswords(user);

    await expect
      .element(page.getByTestId("reset-password-error"))
      .toHaveTextContent(/invalid reset link/iu);
    expect(mocks.resetPassword).not.toHaveBeenCalled();
  });

  it("requires a new password before calling the endpoint", async () => {
    // DELIBERATE local required-field check, flagged on the bead. Without it, an
    // empty password would POST, the server fails `ResetPasswordAsync` and
    // returns 400 invalid_token — so the user is
    // told their *link* expired when in fact they typed nothing. That message is
    // actively misleading. A required check keeps the empty case local and
    // matches the field-error convention the sibling ForgotPassword port set.
    const user = userEvent.setup();
    renderForm();

    await user.click(page.getByTestId("reset-password-submit"));

    expect(mocks.resetPassword).not.toHaveBeenCalled();
    await expect.element(page.getByTestId("reset-password-new-password-error")).toBeInTheDocument();
  });

  it("explains an expired or invalid reset link when the endpoint rejects it", async () => {
    // The oracle's `"invalid_token" =>` branch, reached via HTTP status because
    // the reason string does not survive the WallowError seam — see the file
    // header. This endpoint's only 400 IS invalid_token.
    mocks.resetPassword.mockRejectedValue(invalidTokenRejection());
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user);

    const error = page.getByTestId("reset-password-error");
    await expect.element(error).toHaveTextContent(/invalid or has expired/iu);
    await expect.element(error).toHaveTextContent(/request a new one/iu);
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("falls back to the generic message for a non-400 failure", async () => {
    // The oracle's `_ =>` branch. A 500 is not a bad link and must not be
    // reported as one.
    mocks.resetPassword.mockRejectedValue(serverErrorRejection());
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user);

    const error = page.getByTestId("reset-password-error");
    await expect.element(error).toHaveTextContent(/failed to reset password/iu);
    await expect.element(error).not.toHaveTextContent(/expired/iu);
  });

  it("shows the generic message when the request fails without a status", async () => {
    // A network-level rejection has no `status` at all; structural narrowing
    // must not throw on it, and must not claim the link is bad.
    mocks.resetPassword.mockRejectedValue(new Error("network down"));
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user);

    await expect
      .element(page.getByTestId("reset-password-error"))
      .toHaveTextContent(/failed to reset password/iu);
  });

  it("never leaks the raw rejection into the page", async () => {
    // The seam hands the screen `title: "Unknown error"`. Rendering that verbatim
    // would be the lazy port and tells the user nothing actionable.
    mocks.resetPassword.mockRejectedValue(invalidTokenRejection());
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user);

    await expect.element(page.getByTestId("reset-password-error")).toBeInTheDocument();
    expect(page.getByText(/unknown error/iu).query()).toBeNull();
  });

  it("clears a previous error when the next attempt succeeds", async () => {
    // Oracle: `_error = null;` immediately before the call. A stale "link
    // expired" banner sitting above a successful reset would be a lie.
    mocks.resetPassword.mockRejectedValueOnce(invalidTokenRejection());
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user);
    await expect.element(page.getByTestId("reset-password-error")).toBeInTheDocument();

    await user.click(page.getByTestId("reset-password-submit"));

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: "/login?message=password_reset" });
    });
    expect(page.getByTestId("reset-password-error").query()).toBeNull();
  });

  it("disables submit while the request is in flight", async () => {
    // Oracle: `Disabled="_loading"` — one click, one reset attempt.
    let release: () => void = () => {};
    mocks.resetPassword.mockReturnValue(
      new Promise<void>((resolve) => {
        release = resolve;
      }),
    );
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user);

    await expect.element(page.getByTestId("reset-password-submit")).toBeDisabled();
    await expect
      .element(page.getByTestId("reset-password-submit"))
      .toHaveTextContent(/resetting/iu);

    release();
    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalled();
    });
  });
});

/**
 * Route-level spec. Rendered through a real memory router rather than by poking
 * at `Route.options.component`, because the criterion under test — "email+token
 * read from the query string" — only exists once a URL is parsed by a router.
 * The root here is a throwaway: the app's real `__root.tsx` renders `<html>`,
 * and `src/router.tsx` is off-limits to this task (Wallow-vec7.3.16).
 */
function renderRouteAt(url: string) {
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    resetPasswordRoute.update({
      id: "/reset-password",
      path: "/reset-password",
      getParentRoute: () => rootRoute,
    }),
  ]);
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [url] }),
  });

  return renderWithClient(<RouterProvider router={router} />);
}

describe("/reset-password route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    // Wallow-vec7.3.16 registered this path against a placeholder component;
    // this task's job is to replace it. The path is the contract and is not this
    // task's to change.
    renderRouteAt(`/reset-password?email=${encodeURIComponent(EMAIL)}&token=${TOKEN}`);

    await expect.element(page.getByTestId("reset-password-new-password")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
  });

  it("threads the email and token out of the query string into the reset call", async () => {
    const user = userEvent.setup();
    renderRouteAt(`/reset-password?email=${encodeURIComponent(EMAIL)}&token=${TOKEN}`);

    await expect.element(page.getByTestId("reset-password-new-password")).toBeInTheDocument();
    await submitPasswords(user);

    await vi.waitFor(() => {
      expect(mocks.resetPassword).toHaveBeenCalledWith({
        email: EMAIL,
        token: TOKEN,
        newPassword: PASSWORD,
      });
    });
  });

  it("renders without throwing when the link carries no query at all", async () => {
    // A bare /reset-password must still render its form and refuse on submit —
    // `validateSearch` has to treat both params as optional rather than throw.
    const user = userEvent.setup();
    renderRouteAt("/reset-password");

    await expect.element(page.getByTestId("reset-password-new-password")).toBeInTheDocument();
    await submitPasswords(user);

    await expect
      .element(page.getByTestId("reset-password-error"))
      .toHaveTextContent(/invalid reset link/iu);
    expect(mocks.resetPassword).not.toHaveBeenCalled();
  });
});
