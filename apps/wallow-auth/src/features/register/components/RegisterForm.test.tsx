/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from "@tanstack/react-router";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as registerRoute } from "../../../routes/register";
import { RegisterForm } from "./RegisterForm";

// No global `expect` (vitest `globals` is off), so register the jest-dom
// matchers explicitly — the convention every wallow-auth RTL test follows.
expect.extend(matchers);

/**
 * Component spec for the Register screen (Wallow-vec7.3.8), ported from the
 * Blazor oracle `api/src/Wallow.Auth/Components/Pages/Register.razor`.
 *
 * Testids: `register-error`, `register-email`, `register-password`,
 * `register-confirm-password`, `register-terms`, `register-privacy`,
 * `register-submit` come VERBATIM from the oracle (scout inventory on
 * Wallow-vec7.3). The oracle ships no testid for the strength meter, the
 * passwordless toggle, or the org interstitial, so those are minted under the
 * `{page}-{element}` rule the scout authorised: `register-password-strength`,
 * `register-passwordless-toggle`, `register-org-match`,
 * `register-org-match-accept`, `register-org-match-dismiss`, plus
 * `register-loading`, `register-org-name`, `register-external-providers`.
 *
 * MOCKING SEAM: `../../../lib/wallow-auth-sdk` — the app's own facade, never
 * `@bc-solutions-coder/sdk` directly. Plain `vi.mock` factory + `vi.hoisted`
 * spies and NEVER `vi.resetModules()` (bd memory
 * `vitest-resetmodules-breaks-instanceof-across-graphs`).
 *
 * ── FINDING 1 (REVISED — was "the error switch is UNPORTABLE") ───────────────
 *
 * The oracle switches its message on `result.Error`:
 *
 *     "email_taken"       => "An account with this email already exists."
 *     "password_too_weak" => "Password does not meet the minimum requirements."
 *     _                   => result.Error ?? "An error occurred..."
 *
 * The first cut of this spec pinned ONE generic message for every server
 * rejection, on the finding that the reason string died at the seam:
 * `AccountController.Register` (api/.../Controllers/AccountController.cs:639-724)
 * returns every failure as `BadRequest(new { succeeded = false, error = "..." })`
 * — a bare anon object, NOT RFC 7807 — and `toWallowError()` built its `code`
 * from `extensions.code ?? code` only, so the token under `error` was never read
 * and the screen always got `code: "UNKNOWN"`. Status could not substitute:
 * unlike the sibling ResetPassword port (one failure reason, so 400 *meant*
 * invalid_token), Register's 400 is AMBIGUOUS across four reasons.
 *
 * Wallow-vec7.7 closed that: `readCode` (packages/sdk/src/auth-client.ts) now
 * probes `extensions.code > code > error`, so the API's own token reaches this
 * screen intact. THREE of the four reasons are now recoverable and this spec is
 * revised UPWARD to pin what the oracle's switch was reaching for:
 *
 *     code "email_taken"             -> the oracle's duplicate-email branch
 *                                       (line ~686, mapped from Identity's
 *                                       DuplicateEmail/DuplicateUserName).
 *     code "passwords_do_not_match"  -> line 648. Server-side echo of the local
 *                                       guard; reachable when the guard is
 *                                       bypassed (a passwordless->password race).
 *     code "invalid_client_id"       -> line 658. NOT the user's fault: the link
 *                                       they followed names an unknown client.
 *                                       The copy must not blame their input.
 *     anything else                  -> the oracle's `_` tail, minus its leak.
 *
 * THE FOURTH REASON STAYS GENERIC, and this is not a shortfall of the port. The
 * controller's fallback is `_ => result.Errors.First().Description` — a RAW
 * human-readable IdentityResult sentence ("Passwords must have at least one
 * digit ('0'-'9')."), not a token. There is nothing stable to key on, so
 * weak-password keeps the generic branch. Its rejection carries that sentence as
 * `code`, which is exactly why 'never leaks the API's raw sentence' below has
 * teeth: `code` is matched against KNOWN tokens and NEVER rendered.
 *
 * The test that actually BINDS this mapping is 'an unrecognised code on the same
 * 400 falls back to the generic message' (bd memory
 * `code-keyed-error-mapping-needs-an-unrecognised-code-test-to-bind`): the
 * per-token tests alone would all pass under a blanket `400 -> email_taken` rule,
 * since every token here shares the 400.
 *
 * NOTE: the oracle's `"password_too_weak"` branch is DEAD CODE even in Blazor —
 * the controller never emits that string; it emits
 * `result.Errors.First().Description`. Not ported.
 *
 * ── FINDING 2: THE MATCH ENDPOINT RETURNS A DOMAIN, NOT AN ORG NAME ──────────
 *
 * `OrganizationDomainsController.Match` (.../OrganizationDomainsController.cs:67-88)
 * returns `Ok(new { organizationId, domain })` on a verified match and **404**
 * otherwise. Blazor's `AuthApiClient.GetMatchingOrganizationByDomainAsync`
 * (api/src/Wallow.Auth/Services/AuthApiClient.cs:124-146) deserialises that into
 * `record OrganizationDomainMatchResponse(string? OrgName)` and returns
 * `body?.OrgName` — a field the endpoint NEVER sends. `OrgName` is therefore
 * always null, `_suggestedOrgName` is always null, and the oracle's interstitial
 * is UNREACHABLE in production. Its acceptance criterion nonetheless requires the
 * branch, so the port keys the suggestion on `domain` — the field actually on the
 * wire — which fixes the latent bug rather than faithfully reproducing it.
 *
 * Consequences pinned below: no-match is a 404 REJECTION in TS (not a `null`
 * resolve), so the screen must catch it and fall through to verify-email.
 *
 * ── FINDING 3: requestMembership REQUIRES AUTH (flagged, not fixed here) ─────
 *
 * `MembershipRequestsController` is `[Authorize]` and does `User.GetUserId()!`,
 * so a just-registered, unverified, anonymous user gets 401. The accept path is
 * ported per acceptance, and 'a failed membership request does not claim it was
 * sent' pins that the screen tells the truth when that 401 arrives. Making the
 * endpoint anonymous is out of this bead's scope — follow-up bead.
 *
 * ── FINDING 4: NO ApiBaseUrl PREPEND ────────────────────────────────────────
 *
 * The oracle builds external-login links as `{ApiBaseUrl}/v1/...` against a
 * cross-origin API. wallow-auth is same-origin behind an h3 passthrough proxy,
 * so the origin stays "" (per Wallow-vec7.3.4). Pinned below.
 */

const mocks = vi.hoisted(() => ({
  register: vi.fn(),
  getExternalProviders: vi.fn(),
  getClientTenant: vi.fn(),
  getMatchingOrgByDomain: vi.fn(),
  requestMembership: vi.fn(),
  navigate: vi.fn(),
}));

vi.mock("../../../lib/wallow-auth-sdk", () => ({
  getWallowAuthSdk: () => ({
    auth: {
      register: mocks.register,
      getExternalProviders: mocks.getExternalProviders,
      getClientTenant: mocks.getClientTenant,
      getMatchingOrgByDomain: mocks.getMatchingOrgByDomain,
      requestMembership: mocks.requestMembership,
    },
    oidc: {
      // Faithful restatement of the real `isSafeReturnUrl`
      // (packages/sdk/src/auth-oidc.ts:49-56): nullish/blank unsafe, else a
      // single leading '/'. Restated rather than imported because screens may
      // not import the SDK and this file mocks the whole facade.
      isSafeReturnUrl: (url: string | null | undefined): boolean =>
        url !== null &&
        url !== undefined &&
        url.trim() !== "" &&
        url.startsWith("/") &&
        !url.startsWith("//"),
    },
  }),
}));

vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const EMAIL = "ada@example.com";
const PASSWORD = "N3w-Passw0rd!";
const CLIENT_ID = "wallow-web";
const RETURN_URL = "/dashboard";

/** The domain half of EMAIL — what the oracle splits out for the membership call. */
const EMAIL_DOMAIN = "example.com";

/**
 * What the facade really throws for these endpoints. `title` stays "Unknown
 * error": none of them emit problem details, so no human-readable title ever
 * arrives and the screen must supply its own copy. `code` carries whatever
 * `readCode` recovered — as of Wallow-vec7.7 that includes the `error` member of
 * the bare `{ succeeded, error }` body (Finding 1). `"UNKNOWN"` is the honest
 * value where the endpoint sends no body at all (the two 404s below).
 */
function rejection(status: number, code: string): Error & { status: number; code: string } {
  return Object.assign(new Error("Unknown error"), {
    name: "WallowError",
    status,
    code,
    title: "Unknown error",
  });
}

/**
 * `OrganizationDomainsController.Match` and the client-tenant lookup both answer
 * a miss with a bare `NotFound()` — no body, so nothing for `readCode` to find.
 */
function notFoundRejection(): Error & { status: number; code: string } {
  return rejection(404, "UNKNOWN");
}

/**
 * The weak-password rejection, shaped as it REALLY arrives: the controller's
 * `_ => result.Errors.First().Description` fallback puts a raw English sentence
 * where a token belongs, and `readCode` faithfully surfaces it as `code`. This
 * fixture is the reason the no-leak test below is not theoretical.
 */
const RAW_IDENTITY_SENTENCE = "Passwords must have at least one digit ('0'-'9').";

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(ui: ReactElement) {
  return render(<QueryClientProvider client={newClient()}>{ui}</QueryClientProvider>);
}

function renderForm(props: Partial<{ clientId?: string; returnUrl?: string }> = {}) {
  return renderWithClient(<RegisterForm {...props} />);
}

/** Wait out the concurrent init so the form is on screen. */
async function renderReadyForm(props: Partial<{ clientId?: string; returnUrl?: string }> = {}) {
  const result = renderForm(props);
  await screen.findByTestId("register-email");
  return result;
}

/** Fill every field the oracle's guards demand, then submit. */
async function fillAndSubmit(
  user: ReturnType<typeof userEvent.setup>,
  overrides: Partial<{
    email: string;
    password: string;
    confirmPassword: string;
    terms: boolean;
    privacy: boolean;
  }> = {},
) {
  const {
    email = EMAIL,
    password = PASSWORD,
    confirmPassword = password,
    terms = true,
    privacy = true,
  } = overrides;

  if (email !== "") {
    await user.type(screen.getByTestId("register-email"), email);
  }
  if (password !== "") {
    await user.type(screen.getByTestId("register-password"), password);
  }
  if (confirmPassword !== "") {
    await user.type(screen.getByTestId("register-confirm-password"), confirmPassword);
  }
  if (terms) {
    await user.click(screen.getByTestId("register-terms"));
  }
  if (privacy) {
    await user.click(screen.getByTestId("register-privacy"));
  }
  await user.click(screen.getByTestId("register-submit"));
}

beforeEach(() => {
  vi.clearAllMocks();
  mocks.getExternalProviders.mockResolvedValue([]);
  mocks.getClientTenant.mockResolvedValue({ tenantId: "t-1", orgName: "Acme Inc" });
  mocks.register.mockResolvedValue({ succeeded: true });
  // Default: no verified domain match — a 404 REJECTION, not a null resolve (Finding 2).
  mocks.getMatchingOrgByDomain.mockRejectedValue(notFoundRejection());
  mocks.requestMembership.mockResolvedValue({ id: "req-1" });
});

describe("RegisterForm — concurrent init", () => {
  it("shows a loading state until init settles, then the form", async () => {
    // The oracle awaits both calls in OnInitializedAsync with prerender:false,
    // so nothing renders until they finish. React needs an explicit loading state.
    let release!: () => void;
    mocks.getExternalProviders.mockReturnValue(
      new Promise((resolve) => {
        release = () => {
          resolve([]);
        };
      }),
    );

    renderForm({ clientId: CLIENT_ID });

    expect(await screen.findByTestId("register-loading")).toBeInTheDocument();
    expect(screen.queryByTestId("register-email")).toBeNull();

    release();

    // Anchors the negative above: the field really does appear once init lands.
    expect(await screen.findByTestId("register-email")).toBeInTheDocument();
    expect(screen.queryByTestId("register-loading")).toBeNull();
  });

  it("fires getExternalProviders and getClientTenant CONCURRENTLY, not in sequence", async () => {
    // The oracle's whole point: "These two calls have no data dependency on each
    // other, so run them concurrently to collapse two sequential API round-trips
    // into one latency unit." A sequential port would still pass a
    // both-were-called assertion, so this pins that the SECOND call is issued
    // before the FIRST has resolved.
    let releaseProviders!: () => void;
    mocks.getExternalProviders.mockReturnValue(
      new Promise((resolve) => {
        releaseProviders = () => {
          resolve([]);
        };
      }),
    );

    renderForm({ clientId: CLIENT_ID });

    await waitFor(() => {
      expect(mocks.getClientTenant).toHaveBeenCalledWith(CLIENT_ID);
    });
    expect(mocks.getExternalProviders).toHaveBeenCalled();

    releaseProviders();
    await screen.findByTestId("register-email");
  });

  it("skips the client-tenant lookup when no client_id is supplied", async () => {
    // Oracle: `if (!string.IsNullOrEmpty(ClientId))` gates ResolveOrgNameAsync.
    await renderReadyForm();

    expect(mocks.getClientTenant).not.toHaveBeenCalled();
    // Anchor: init genuinely ran, so the negative above is about the gate.
    expect(mocks.getExternalProviders).toHaveBeenCalled();
    expect(screen.queryByTestId("register-org-name")).toBeNull();
  });

  it("announces the resolved organisation when a client_id maps to one", async () => {
    // Oracle: "You're registering for @_orgName".
    await renderReadyForm({ clientId: CLIENT_ID });

    expect(await screen.findByTestId("register-org-name")).toHaveTextContent(/acme inc/iu);
  });

  it("ignores a failed client-tenant lookup — the org name is informational only", async () => {
    // Oracle swallows HttpRequestException; the endpoint 404s for an unknown
    // client. A registration form must not be blocked by a cosmetic lookup.
    mocks.getClientTenant.mockRejectedValue(notFoundRejection());

    await renderReadyForm({ clientId: CLIENT_ID });

    // Anchored: the form is usable and no error banner was raised.
    expect(screen.getByTestId("register-submit")).toBeInTheDocument();
    expect(screen.queryByTestId("register-org-name")).toBeNull();
    expect(screen.queryByTestId("register-error")).toBeNull();
  });

  it("links external providers same-origin, WITHOUT the oracle's ApiBaseUrl prepend", async () => {
    // Finding 4. The oracle builds `{ApiBaseUrl}/v1/...` for a cross-origin API;
    // wallow-auth is same-origin behind the h3 passthrough, so the origin stays "".
    mocks.getExternalProviders.mockResolvedValue(["Google"]);

    await renderReadyForm();

    const link = await screen.findByTestId("register-external-google");
    const href: string = link.getAttribute("href") ?? "";

    expect(href.startsWith("/v1/identity/auth/external-login")).toBe(true);
    expect(href).toContain("provider=Google");
    expect(href).not.toContain("http");
    expect(href).not.toContain("5001");
  });

  it("renders no provider section when the API offers none", async () => {
    // Oracle: `@if (_externalProviders.Count > 0)`.
    await renderReadyForm();

    expect(screen.getByTestId("register-submit")).toBeInTheDocument();
    expect(screen.queryByTestId("register-external-providers")).toBeNull();
  });
});

describe("RegisterForm — fields and validation", () => {
  it("renders the oracle's fields, and no error before submit", async () => {
    await renderReadyForm();

    expect(screen.getByTestId("register-email")).toBeInTheDocument();
    expect(screen.getByTestId("register-password")).toBeInTheDocument();
    expect(screen.getByTestId("register-confirm-password")).toBeInTheDocument();
    expect(screen.getByTestId("register-terms")).toBeInTheDocument();
    expect(screen.getByTestId("register-privacy")).toBeInTheDocument();
    expect(screen.getByTestId("register-submit")).toBeInTheDocument();
    expect(screen.queryByTestId("register-error")).toBeNull();
  });

  it("masks both password fields", async () => {
    await renderReadyForm();

    expect(screen.getByTestId("register-password")).toHaveAttribute("type", "password");
    expect(screen.getByTestId("register-confirm-password")).toHaveAttribute("type", "password");
  });

  it("refuses a blank email", async () => {
    // Oracle: "Please enter your email address."
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user, { email: "" });

    expect(await screen.findByTestId("register-error")).toHaveTextContent(/email/iu);
    expect(mocks.register).not.toHaveBeenCalled();
  });

  it("refuses a blank password when not passwordless", async () => {
    // Oracle: "Please enter a password."
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user, { password: "", confirmPassword: "" });

    expect(await screen.findByTestId("register-error")).toHaveTextContent(/password/iu);
    expect(mocks.register).not.toHaveBeenCalled();
  });

  it("refuses mismatched passwords before calling the API", async () => {
    // Oracle: "Passwords do not match." The server would also reject this
    // (400 passwords_do_not_match), but that 400 is indistinguishable from
    // email_taken at the seam (Finding 1) — so the local guard is what lets the
    // user see the real reason. Pinned.
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user, { password: PASSWORD, confirmPassword: "Different-1!" });

    expect(await screen.findByTestId("register-error")).toHaveTextContent(/do not match/iu);
    expect(mocks.register).not.toHaveBeenCalled();
  });

  it("refuses to submit without agreeing to the Terms of Service", async () => {
    // Oracle: "You must agree to the Terms of Service."
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user, { terms: false });

    expect(await screen.findByTestId("register-error")).toHaveTextContent(/terms of service/iu);
    expect(mocks.register).not.toHaveBeenCalled();
  });

  it("refuses to submit without agreeing to the Privacy Policy", async () => {
    // Oracle: "You must agree to the Privacy Policy."
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user, { privacy: false });

    expect(await screen.findByTestId("register-error")).toHaveTextContent(/privacy policy/iu);
    expect(mocks.register).not.toHaveBeenCalled();
  });

  it("links out to the Terms and the Privacy Policy", async () => {
    // The oracle's two inline consent links. No testids in the oracle, so
    // asserted by role + href.
    await renderReadyForm();

    expect(screen.getByRole("link", { name: /terms of service/iu })).toHaveAttribute(
      "href",
      "/terms",
    );
    expect(screen.getByRole("link", { name: /privacy policy/iu })).toHaveAttribute(
      "href",
      "/privacy",
    );
  });
});

describe("RegisterForm — passwordless toggle", () => {
  it("hides the password fields when passwordless is selected", async () => {
    // Oracle: `@if (!_isPasswordless)` wraps both password blocks.
    const user = userEvent.setup();
    await renderReadyForm();

    // Anchor: they are on screen first, so the disappearance is real.
    expect(screen.getByTestId("register-password")).toBeInTheDocument();

    await user.click(screen.getByTestId("register-passwordless-toggle"));

    expect(screen.queryByTestId("register-password")).toBeNull();
    expect(screen.queryByTestId("register-confirm-password")).toBeNull();
    expect(screen.getByTestId("register-submit")).toBeInTheDocument();
  });

  it("sends loginMethod 'passwordless' and skips the password guards", async () => {
    // Oracle: `string? loginMethod = _isPasswordless ? "passwordless" : null;`
    // and the password/mismatch guards sit inside `if (!_isPasswordless)`.
    const user = userEvent.setup();
    await renderReadyForm();

    await user.click(screen.getByTestId("register-passwordless-toggle"));
    await user.type(screen.getByTestId("register-email"), EMAIL);
    await user.click(screen.getByTestId("register-terms"));
    await user.click(screen.getByTestId("register-privacy"));
    await user.click(screen.getByTestId("register-submit"));

    await waitFor(() => {
      expect(mocks.register).toHaveBeenCalledWith(
        expect.objectContaining({ email: EMAIL, loginMethod: "passwordless" }),
      );
    });
  });

  it("sends no loginMethod when a password is used", async () => {
    // Oracle passes null for the password flow.
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user);

    await waitFor(() => {
      expect(mocks.register).toHaveBeenCalled();
    });
    const body: { loginMethod?: string | null } = mocks.register.mock.calls[0][0];
    expect(body.loginMethod ?? null).toBeNull();
  });
});

describe("RegisterForm — password strength meter", () => {
  /** Type a password and read back the meter's label. */
  async function strengthOf(
    user: ReturnType<typeof userEvent.setup>,
    password: string,
  ): Promise<HTMLElement> {
    await user.type(screen.getByTestId("register-password"), password);
    return screen.findByTestId("register-password-strength");
  }

  it("shows no meter until a password is typed", async () => {
    // Oracle: `@if (!string.IsNullOrEmpty(_password))` gates the BbProgress.
    await renderReadyForm();

    // Anchored: the field exists, so the meter's absence is about emptiness.
    expect(screen.getByTestId("register-password")).toHaveValue("");
    expect(screen.queryByTestId("register-password-strength")).toBeNull();
  });

  it("rates a short password Weak", async () => {
    // Oracle's `else` branch: length < 8 => 25 / "Weak".
    const user = userEvent.setup();
    await renderReadyForm();

    expect(await strengthOf(user, "abc")).toHaveTextContent(/weak/iu);
  });

  it("rates an 8-character password Fair", async () => {
    // Oracle: `_password.Length >= 8` => 50 / "Fair".
    const user = userEvent.setup();
    await renderReadyForm();

    expect(await strengthOf(user, "abcdefgh")).toHaveTextContent(/fair/iu);
  });

  it("rates a long mixed password Strong", async () => {
    // Oracle: `Length >= 12 && hasUpper && hasLower && (hasDigit || hasSpecial)`
    // => 100 / "Strong".
    const user = userEvent.setup();
    await renderReadyForm();

    expect(await strengthOf(user, "Abcdefgh1234")).toHaveTextContent(/strong/iu);
  });

  it("does not rate a long password Strong on length alone — the mix is required", async () => {
    // 12 chars but all lowercase: fails hasUpper, so the oracle falls through to
    // the `Length >= 8` branch => "Fair". Pins that the port ports the WHOLE
    // predicate and not just the length check.
    const user = userEvent.setup();
    await renderReadyForm();

    expect(await strengthOf(user, "abcdefghijkl")).toHaveTextContent(/fair/iu);
  });

  it("updates the rating live as the password grows", async () => {
    // Oracle recomputes on every ValueChanged, not just on submit.
    const user = userEvent.setup();
    await renderReadyForm();

    await user.type(screen.getByTestId("register-password"), "abc");
    expect(await screen.findByTestId("register-password-strength")).toHaveTextContent(/weak/iu);

    await user.type(screen.getByTestId("register-password"), "defgh");
    await waitFor(() => {
      expect(screen.getByTestId("register-password-strength")).toHaveTextContent(/fair/iu);
    });
  });
});

describe("RegisterForm — submission", () => {
  it("sends the typed credentials with the query's client_id and returnUrl", async () => {
    // Oracle: `new RegisterRequest(_email, _password, _confirmPassword, ClientId,
    // loginMethod, ReturnUrl)`.
    const user = userEvent.setup();
    await renderReadyForm({ clientId: CLIENT_ID, returnUrl: RETURN_URL });

    await fillAndSubmit(user);

    await waitFor(() => {
      expect(mocks.register).toHaveBeenCalledWith(
        expect.objectContaining({
          email: EMAIL,
          password: PASSWORD,
          confirmPassword: PASSWORD,
          clientId: CLIENT_ID,
          returnUrl: RETURN_URL,
        }),
      );
    });
  });

  it("disables the submit while the registration is in flight", async () => {
    // Oracle: `Loading="_isSubmitting" Disabled="_isSubmitting"`.
    const user = userEvent.setup();
    let release!: () => void;
    mocks.register.mockReturnValue(
      new Promise((resolve) => {
        release = () => {
          resolve({ succeeded: true });
        };
      }),
    );

    await renderReadyForm();
    await fillAndSubmit(user);

    await waitFor(() => {
      expect(screen.getByTestId("register-submit")).toBeDisabled();
    });

    release();
    await waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalled();
    });
  });

  it("tells the user their email is already registered", async () => {
    // REVISED (was 'surfaces a generic message when the email is already taken').
    // The API returns 400 {succeeded:false, error:"email_taken"}; as of
    // Wallow-vec7.7 that token reaches the screen as `code`, so the oracle's
    // duplicate-email branch is portable and the generic message would now be a
    // deliberate downgrade. This is the single most actionable failure this form
    // has: the user has an account and should sign in, not retry.
    const user = userEvent.setup();
    mocks.register.mockRejectedValue(rejection(400, "email_taken"));

    await renderReadyForm();
    await fillAndSubmit(user);

    expect(await screen.findByTestId("register-error")).toHaveTextContent(/already exists/iu);
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("tells the user the passwords do not match when the SERVER rejects them", async () => {
    // 400 `passwords_do_not_match` (controller line 648). The local guard catches
    // this first in the ordinary case, so this pins the path where the server is
    // the one that notices — and the message must name the real reason rather
    // than fall to the generic tail.
    const user = userEvent.setup();
    mocks.register.mockRejectedValue(rejection(400, "passwords_do_not_match"));

    await renderReadyForm();
    await fillAndSubmit(user);

    expect(await screen.findByTestId("register-error")).toHaveTextContent(/do not match/iu);
  });

  it("does not blame the user's input when the client_id is invalid", async () => {
    // 400 `invalid_client_id` (controller line 658). The `client_id` came off the
    // QUERY STRING, not the form: nothing the user typed is wrong and retyping it
    // cannot help. Pins that the copy points at the LINK, and specifically that
    // this failure is not mislabelled as a duplicate email — the mistake a
    // blanket `400 -> email_taken` rule would make.
    const user = userEvent.setup();
    mocks.register.mockRejectedValue(rejection(400, "invalid_client_id"));

    await renderReadyForm({ clientId: "not-a-real-client" });

    await fillAndSubmit(user);

    const banner: HTMLElement = await screen.findByTestId("register-error");
    expect(banner).toHaveTextContent(/link/iu);
    expect(banner).not.toHaveTextContent(/already exists/iu);
  });

  it("falls back to a generic message for a weak password, which has NO stable code", async () => {
    // The fourth 400 and the one Wallow-vec7.7 could not recover: the controller's
    // `_ => result.Errors.First().Description` fallback emits a raw sentence, not
    // a token, so there is nothing to key on and the generic branch stands. This
    // is the honest floor, not a downgrade.
    const user = userEvent.setup();
    mocks.register.mockRejectedValue(rejection(400, RAW_IDENTITY_SENTENCE));

    await renderReadyForm();
    await fillAndSubmit(user, { password: "weak", confirmPassword: "weak" });
    const banner: HTMLElement = await screen.findByTestId("register-error");
    const weakMessage: string = banner.textContent ?? "";

    expect(weakMessage).not.toBe("");
    expect(weakMessage).toMatch(/try again/iu);
    // Anchored both ways: a port that guessed from the shared 400 would print one
    // of the mapped messages here instead of the tail.
    expect(weakMessage).not.toMatch(/already exists/iu);
    expect(weakMessage).not.toMatch(/do not match/iu);
  });

  it("never leaks the API's raw password-rule sentence into the banner", async () => {
    // The oracle's `_ => result.Error` tail renders the server string VERBATIM, so
    // a Blazor user really can be shown Identity's own prose. `code` is a machine
    // member: matched against known tokens, never rendered. Not ported.
    const user = userEvent.setup();
    mocks.register.mockRejectedValue(rejection(400, RAW_IDENTITY_SENTENCE));

    await renderReadyForm();
    await fillAndSubmit(user, { password: "weak", confirmPassword: "weak" });

    const banner: HTMLElement = await screen.findByTestId("register-error");
    expect(banner).not.toHaveTextContent(RAW_IDENTITY_SENTENCE);
    expect(banner).not.toHaveTextContent(/'0'-'9'/u);
  });

  it("falls back to the generic message for an UNRECOGNISED code on the SAME 400", async () => {
    // THE TEST THAT BINDS THE MAPPING (bd memory `code-keyed-error-mapping-needs-
    // an-unrecognised-code-test-to-bind`). Every token this endpoint sends shares
    // a 400, so the per-token tests above ALL pass under a blanket `400 -> "email
    // already exists"` rule. This one does not: it pins that the screen matches
    // the CODE and guesses at nothing. A token the API adds tomorrow must read as
    // the generic tail rather than as a confident lie.
    const user = userEvent.setup();
    mocks.register.mockRejectedValue(rejection(400, "some_future_token"));

    await renderReadyForm();
    await fillAndSubmit(user);

    const banner: HTMLElement = await screen.findByTestId("register-error");
    expect(banner).toHaveTextContent(/try again/iu);
    expect(banner).not.toHaveTextContent(/already exists/iu);
    // ...and it is not leaked, either.
    expect(banner).not.toHaveTextContent(/some_future_token/u);
  });

  it("surfaces a generic message when the server errors outright", async () => {
    const user = userEvent.setup();
    mocks.register.mockRejectedValue(rejection(500, "UNKNOWN"));

    await renderReadyForm();
    await fillAndSubmit(user);

    expect(await screen.findByTestId("register-error")).toHaveTextContent(/try again/iu);
  });

  it("falls back to the generic message when the rejection carries no code at all", async () => {
    // A network-level failure — DNS, offline, CORS — reaches `onError` as a plain
    // Error with neither `code` nor `status`. Structural narrowing must tolerate
    // that rather than throw inside the error handler.
    const user = userEvent.setup();
    mocks.register.mockRejectedValue(new Error("Failed to fetch"));

    await renderReadyForm();
    await fillAndSubmit(user);

    expect(await screen.findByTestId("register-error")).toHaveTextContent(/try again/iu);
  });

  it("clears a stale error banner when a retry succeeds", async () => {
    // Oracle: `_errorMessage = null;` at the top of HandleRegister. A stale
    // failure sitting above a successful registration would be a lie.
    const user = userEvent.setup();
    mocks.register.mockRejectedValueOnce(rejection(400, "email_taken"));

    await renderReadyForm();
    await fillAndSubmit(user);
    // Anchor: the banner is genuinely there before the retry clears it.
    expect(await screen.findByTestId("register-error")).toBeInTheDocument();

    await user.click(screen.getByTestId("register-submit"));

    await waitFor(() => {
      expect(screen.queryByTestId("register-error")).toBeNull();
    });
  });
});

describe("RegisterForm — org-domain-match interstitial", () => {
  /** A verified match, shaped as the endpoint really answers it (Finding 2). */
  const MATCH = { organizationId: "org-1", domain: EMAIL_DOMAIN };

  it("asks for a domain match using the registered email", async () => {
    // Oracle: `GetMatchingOrganizationByDomainAsync(_email)` after success.
    const user = userEvent.setup();
    mocks.getMatchingOrgByDomain.mockResolvedValue(MATCH);

    await renderReadyForm();
    await fillAndSubmit(user);

    await waitFor(() => {
      expect(mocks.getMatchingOrgByDomain).toHaveBeenCalledWith(EMAIL);
    });
  });

  it("offers the interstitial when the email's domain matches an organisation", async () => {
    // Oracle: "It looks like your team uses @_suggestedOrgName." Keyed on
    // `domain`, the field the endpoint actually sends (Finding 2).
    const user = userEvent.setup();
    mocks.getMatchingOrgByDomain.mockResolvedValue(MATCH);

    await renderReadyForm();
    await fillAndSubmit(user);

    expect(await screen.findByTestId("register-org-match")).toHaveTextContent(EMAIL_DOMAIN);
    expect(screen.getByTestId("register-org-match-accept")).toBeInTheDocument();
    expect(screen.getByTestId("register-org-match-dismiss")).toBeInTheDocument();
    // The interstitial REPLACES the form (oracle returns before rendering it).
    expect(screen.queryByTestId("register-submit")).toBeNull();
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("ACCEPT path: requests membership for the email's domain and confirms it was sent", async () => {
    // Oracle: `_email.Split('@')[1]` -> RequestMembershipAsync -> "Request Sent".
    const user = userEvent.setup();
    mocks.getMatchingOrgByDomain.mockResolvedValue(MATCH);

    await renderReadyForm();
    await fillAndSubmit(user);
    await user.click(await screen.findByTestId("register-org-match-accept"));

    await waitFor(() => {
      expect(mocks.requestMembership).toHaveBeenCalledWith({ emailDomain: EMAIL_DOMAIN });
    });
    expect(await screen.findByTestId("register-org-match-accept")).toHaveTextContent(/sent/iu);
    expect(screen.getByTestId("register-org-match-accept")).toBeDisabled();
  });

  it("does not claim a membership request was sent when it fails", async () => {
    // Finding 3: the endpoint is [Authorize] and this user is anonymous, so 401
    // is the LIKELY real-world response. Oracle assigns the bool result, so a
    // failure silently leaves the button as-is; telling the user "Request Sent"
    // when nothing was recorded would be a lie.
    const user = userEvent.setup();
    mocks.getMatchingOrgByDomain.mockResolvedValue(MATCH);
    mocks.requestMembership.mockRejectedValue(rejection(401, "UNKNOWN"));

    await renderReadyForm();
    await fillAndSubmit(user);
    await user.click(await screen.findByTestId("register-org-match-accept"));

    await waitFor(() => {
      expect(mocks.requestMembership).toHaveBeenCalled();
    });
    // Anchored on the button still being actionable — not a bare "no 'sent'".
    const accept: HTMLElement = screen.getByTestId("register-org-match-accept");
    expect(accept).toHaveTextContent(/request access/iu);
    expect(accept).not.toHaveTextContent(/sent/iu);
    expect(accept).toBeEnabled();
  });

  it("SKIP path: 'No thanks' moves on to verify-email without requesting membership", async () => {
    // Oracle: DismissSuggestion() -> NavigateTo(VerifyEmailUrl).
    const user = userEvent.setup();
    mocks.getMatchingOrgByDomain.mockResolvedValue(MATCH);

    await renderReadyForm();
    await fillAndSubmit(user);
    await user.click(await screen.findByTestId("register-org-match-dismiss"));

    await waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: "/verify-email" });
    });
    expect(mocks.requestMembership).not.toHaveBeenCalled();
  });

  it("goes straight to verify-email when no organisation matches", async () => {
    // Finding 2: no-match is a 404 REJECTION here, not a null resolve. A port
    // that only handled `null` would surface an error banner to a user whose
    // registration actually SUCCEEDED.
    const user = userEvent.setup();
    mocks.getMatchingOrgByDomain.mockRejectedValue(notFoundRejection());

    await renderReadyForm();
    await fillAndSubmit(user);

    await waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: "/verify-email" });
    });
    expect(screen.queryByTestId("register-org-match")).toBeNull();
    expect(screen.queryByTestId("register-error")).toBeNull();
  });

  it("threads a safe returnUrl through to verify-email", async () => {
    // Oracle: `VerifyEmailUrl` = `/verify-email?returnUrl={escaped}`.
    const user = userEvent.setup();

    await renderReadyForm({ returnUrl: RETURN_URL });
    await fillAndSubmit(user);

    await waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({
        href: `/verify-email?returnUrl=${encodeURIComponent(RETURN_URL)}`,
      });
    });
  });
});

describe("RegisterForm — open-redirect guard", () => {
  it("REFUSES an unsafe returnUrl instead of sanitising it away", async () => {
    // bd memory `returnurl-guard-refuse-dont-sanitize`: on an unsafe returnUrl,
    // route to /error?reason=invalid_redirect_uri like the Blazor oracle. Do NOT
    // silently fall back to "/" — that is C# Sanitize() behaviour, deliberately
    // not ported.
    const user = userEvent.setup();

    await renderReadyForm({ returnUrl: "//evil.example.com/steal" });
    await fillAndSubmit(user);

    await waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: "/error?reason=invalid_redirect_uri" });
    });
    expect(mocks.navigate).not.toHaveBeenCalledWith({ href: "/verify-email" });
  });

  it("REFUSES an absolute returnUrl", async () => {
    const user = userEvent.setup();

    await renderReadyForm({ returnUrl: "https://evil.example.com/steal" });
    await fillAndSubmit(user);

    await waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: "/error?reason=invalid_redirect_uri" });
    });
  });

  it("lets a missing returnUrl through to the plain verify-email URL", async () => {
    // Only a NULLISH returnUrl gets the fallback; the guard runs on a PRESENT
    // one only. An absent param is not an attack.
    const user = userEvent.setup();

    await renderReadyForm();
    await fillAndSubmit(user);

    await waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: "/verify-email" });
    });
  });
});

/**
 * Route-level spec. Rendered through a real memory router rather than by poking
 * at `Route.options.component`, because the criterion under test — client_id and
 * returnUrl read off the query string — only exists once a URL is parsed. The
 * root here is a throwaway: the app's real `__root.tsx` renders `<html>`, and
 * `src/router.tsx` is off-limits to this task (Wallow-vec7.3.16).
 */
function renderRouteAt(url: string) {
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    registerRoute.update({
      id: "/register",
      path: "/register",
      getParentRoute: () => rootRoute,
    }),
  ]);
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [url] }),
  });

  return renderWithClient(<RouterProvider router={router} />);
}

describe("/register route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    // Wallow-vec7.3.16 registered this path against a placeholder component;
    // this task's job is to replace it. The path is the contract.
    renderRouteAt("/register");

    expect(await screen.findByTestId("register-email")).toBeInTheDocument();
    expect(screen.queryByTestId("route-placeholder")).toBeNull();
  });

  it("reads client_id and returnUrl off the query string and threads them into the register call", async () => {
    // The oracle's two `[SupplyParameterFromQuery]` properties — note `client_id`
    // is snake_case on the wire and `returnUrl` is not.
    const user = userEvent.setup();
    renderRouteAt(`/register?client_id=${CLIENT_ID}&returnUrl=${encodeURIComponent(RETURN_URL)}`);

    await screen.findByTestId("register-email");
    await fillAndSubmit(user);

    await waitFor(() => {
      expect(mocks.register).toHaveBeenCalledWith(
        expect.objectContaining({ clientId: CLIENT_ID, returnUrl: RETURN_URL }),
      );
    });
  });

  it("still renders for a bare /register with no query at all", async () => {
    // A mangled or bare link must render the form, not throw a search-validation
    // error at the user.
    renderRouteAt("/register");

    expect(await screen.findByTestId("register-submit")).toBeInTheDocument();
    expect(mocks.getClientTenant).not.toHaveBeenCalled();
  });
});
