import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "@shared/testing/harness";
import { Route as registerRoute } from "@app/routes/register";
import { RegisterForm } from "./RegisterForm";

/**
 * Component spec for the Register screen (Wallow-vec7.3.8).
 *
 * Testids: `register-error`, `register-email`, `register-password`,
 * `register-confirm-password`, `register-terms`, `register-privacy`,
 * `register-submit` come VERBATIM from the oracle (scout inventory on
 * Wallow-vec7.3). The oracle ships no testid for the strength meter or the
 * passwordless toggle, so those are minted under the `{page}-{element}` rule the
 * scout authorised: `register-password-strength`, `register-passwordless-toggle`,
 * `register-loading`, `register-org-name`, `register-external-providers`.
 *
 * TEST SEAM: `@bc-solutions-coder/testing/sdk-harness` (Wallow-pu6a.5.1). The
 * SDK is the REAL one and only its `fetch` is faked, so this screen's whole
 * pipeline — generated `{op}Options()` -> request-scoped SDK -> generated
 * operations -> CSRF interceptor -> serialization -> WallowError shaping ->
 * React Query — runs here, and the assertions below read the outgoing REQUESTS
 * rather than spies on a stand-in double. `renderWithWallow` supplies the router
 * context the screen reads its SDK off, and `createAuthHarness()` pins the
 * harness origin to this app's root-mounted API surface (Wallow-pu6a.5.5).
 *
 * This screen issues THREE distinct requests — the provider list and the
 * client-tenant lookup on mount, then register on submit — so the transport is
 * driven by a per-path router ({@link routes}) rather than one blanket response.
 * Each test reprograms only the leg it is about.
 *
 * The `isSafeReturnUrl` stub is gone too. It used to restate the real rule, and a
 * second copy of a security rule is a second copy to get wrong — the screen now
 * reaches the shipped guard in `packages/sdk/src/auth-oidc.ts`. The
 * `@tanstack/react-router` `useNavigate` mock STAYS: navigation is a router
 * concern, not an SDK one, and there is no request to observe it through.
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
 * NOTE: the oracle's `"password_too_weak"` branch is DEAD CODE — the controller
 * never emits that string; it emits `result.Errors.First().Description`. Not
 * ported.
 *
 * ── THE ORG-DOMAIN-MATCH INTERSTITIAL WAS REMOVED (F6) ───────────────────────
 *
 * F6 deleted the anonymous org-membership feature from the API: both the
 * `GET /v1/identity/organization-domains/match` lookup and the register request's
 * `RequestOrgMembership` opt-in flag are gone (regenerated OpenAPI snapshot,
 * T7.1). With no endpoint left to consult, the pre-submit domain lookup and the
 * interstitial it fed are removed: the form now submits straight to register.
 * The informational client-tenant org-name banner (a separate, surviving lookup)
 * stays.
 *
 * ── FINDING 2: NO ApiBaseUrl PREPEND ────────────────────────────────────────
 *
 * The oracle builds external-login links as `{ApiBaseUrl}/v1/...` against a
 * cross-origin API. wallow-auth is same-origin behind an SDK passthrough proxy,
 * so the origin stays "" (per Wallow-vec7.3.4). Pinned below.
 */

/**
 * `navigate` is the ONE spy left. Navigation is a router concern with no request
 * behind it, so `@tanstack/react-router` is still mocked — the acceptance guard
 * (`src/sdk-test-seam.test.ts`) forbids mocking the SDK, not the router.
 */
const mocks = vi.hoisted(() => ({
  navigate: vi.fn(),
}));

vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const EMAIL = "ada@example.com";
const PASSWORD = "N3w-Passw0rd!";
const CLIENT_ID = "wallow-web";
const RETURN_URL = "/dashboard";

/**
 * The three endpoints this screen touches, as the generated operations spell
 * them. Named rather than inlined because every request assertion below
 * discriminates on `call.path`.
 */
const PROVIDERS_ENDPOINT = "/v1/identity/auth/external-providers";
const TENANT_ENDPOINT = "/v1/identity/auth/client-tenant";
const REGISTER_ENDPOINT = "/v1/identity/auth/register";

const BAD_REQUEST = 400;
const NOT_FOUND = 404;
const SERVER_ERROR = 500;

let harness: SdkHarness;

/** One leg of the transport router — reprogrammed per test. */
type Leg = () => Response | Promise<Response>;

/**
 * The per-path responses in force for the current test. Defaults are the happy
 * path; a test reassigns only the leg it is about.
 */
let routes: { providers: Leg; tenant: Leg; register: Leg };

/** A 2xx JSON body. */
function ok(data: unknown): Leg {
  return () => Response.json(data, { status: 200 });
}

/**
 * A failure exactly as these endpoints really send it: a bare
 * `{ succeeded: false, error }` anonymous object, NOT problem details. The SDK's
 * `readCode` probes `extensions.code > code > error`, so the API's own token
 * arrives on the screen as `code` (Finding 1) — which is what makes the
 * per-token tests below real rather than a restatement of a fixture.
 */
function failure(status: number, code: string): Leg {
  return () => Response.json({ succeeded: false, error: code }, { status });
}

/**
 * The client-tenant lookup answers a miss with a bare `NotFound()` — no body, so
 * nothing for `readCode` to find and the screen gets `code: "UNKNOWN"`.
 */
function notFound(): Leg {
  return () => Response.json(null, { status: NOT_FOUND });
}

/**
 * The weak-password rejection, shaped as it REALLY arrives: the controller's
 * `_ => result.Errors.First().Description` fallback puts a raw English sentence
 * where a token belongs, and `readCode` faithfully surfaces it as `code`. This
 * fixture is the reason the no-leak test below is not theoretical.
 */
const RAW_IDENTITY_SENTENCE = "Passwords must have at least one digit ('0'-'9').";

/** Every recorded request to one endpoint, in order. */
function callsTo(endpoint: string) {
  return harness.calls.filter((call) => call.path.startsWith(endpoint));
}

/** The body the screen posted to register, or `undefined` if it never did. */
function registerBody(): Record<string, unknown> | undefined {
  return callsTo(REGISTER_ENDPOINT).at(-1)?.body as Record<string, unknown> | undefined;
}

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

function renderForm(props: Partial<{ clientId?: string; returnUrl?: string }> = {}) {
  return renderWithClient(<RegisterForm {...props} />);
}

/** Wait out the concurrent init so the form is on screen. */
async function renderReadyForm(props: Partial<{ clientId?: string; returnUrl?: string }> = {}) {
  const result = renderForm(props);
  await expect.element(page.getByTestId("register-email")).toBeInTheDocument();
  return result;
}

/**
 * Toggle a checkbox by CLICKING it, the way a pointer user does.
 *
 * This helper used to focus the root and press Space instead (Wallow-m5aq.5.2),
 * for a reason that no longer holds: the catalog's `Checkbox` renders its root
 * as a `<span role="checkbox">` sized purely by Tailwind utilities, this app's
 * browser vitest project compiled no Tailwind, so the root measured ZERO wide
 * and Playwright's actionability check never settled on a click. That project
 * now loads the app's real stylesheet (Wallow-8ytl), so the box has its real box
 * and the click says exactly what the user does.
 */
async function toggleCheckbox(
  user: ReturnType<typeof userEvent.setup>,
  testId: string,
): Promise<void> {
  const box = page.getByTestId(testId);

  await expect.element(box).toBeInTheDocument();
  await user.click(box);
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
    await user.type(page.getByTestId("register-email"), email);
  }
  if (password !== "") {
    await user.type(page.getByTestId("register-password"), password);
  }
  if (confirmPassword !== "") {
    await user.type(page.getByTestId("register-confirm-password"), confirmPassword);
  }
  if (terms) {
    await toggleCheckbox(user, "register-terms");
  }
  if (privacy) {
    await toggleCheckbox(user, "register-privacy");
  }
  await user.click(page.getByTestId("register-submit"));
}

beforeEach(() => {
  vi.clearAllMocks();
  harness = createAuthHarness();
  routes = {
    providers: ok([]),
    tenant: ok({ tenantId: "t-1", orgName: "Acme Inc" }),
    register: ok({ succeeded: true }),
  };
  // One responder, routed by path: the two init reads and the register write are
  // in flight against the same transport, so a blanket response could not tell
  // them apart.
  harness.respond((call) => {
    if (call.path.startsWith(REGISTER_ENDPOINT)) {
      return routes.register();
    }
    if (call.path.startsWith(TENANT_ENDPOINT)) {
      return routes.tenant();
    }
    if (call.path.startsWith(PROVIDERS_ENDPOINT)) {
      return routes.providers();
    }

    // An unrouted path is a bug in the spec, not a scenario: fail loudly rather
    // than hand the screen a plausible-looking empty success.
    throw new Error(`Unexpected request: ${call.method} ${call.path}`);
  });
});

describe("RegisterForm — concurrent init", () => {
  it("shows a loading state until init settles, then the form", async () => {
    // The oracle awaits both calls in OnInitializedAsync with prerender:false,
    // so nothing renders until they finish. React needs an explicit loading state.
    let release!: () => void;
    routes.providers = async () =>
      await new Promise<Response>((resolve) => {
        release = () => {
          resolve(Response.json([], { status: 200 }));
        };
      });

    renderForm({ clientId: CLIENT_ID });

    await expect.element(page.getByTestId("register-loading")).toBeInTheDocument();
    expect(page.getByTestId("register-email").query()).toBeNull();

    // Wait for the request to REACH the transport before releasing it: the
    // responder is only installed once, so releasing into the gap before `fetch`
    // is called would leave the never-settling promise in place forever.
    await vi.waitFor(() => {
      expect(callsTo(PROVIDERS_ENDPOINT)).toHaveLength(1);
    });
    release();

    // Anchors the negative above: the field really does appear once init lands.
    await expect.element(page.getByTestId("register-email")).toBeInTheDocument();
    expect(page.getByTestId("register-loading").query()).toBeNull();
  });

  it("fires getExternalProviders and getClientTenant CONCURRENTLY, not in sequence", async () => {
    // The oracle's whole point: "These two calls have no data dependency on each
    // other, so run them concurrently to collapse two sequential API round-trips
    // into one latency unit." A sequential port would still pass a
    // both-were-called assertion, so this pins that the SECOND call is issued
    // before the FIRST has resolved.
    let releaseProviders!: () => void;
    routes.providers = async () =>
      await new Promise<Response>((resolve) => {
        releaseProviders = () => {
          resolve(Response.json([], { status: 200 }));
        };
      });

    renderForm({ clientId: CLIENT_ID });

    // The tenant request reaches the wire while the provider request is still
    // un-answered. Read off the transport, this is now a statement about two
    // real in-flight requests rather than about two spies.
    await vi.waitFor(() => {
      expect(callsTo(`${TENANT_ENDPOINT}/${CLIENT_ID}`)).toHaveLength(1);
    });
    expect(callsTo(PROVIDERS_ENDPOINT)).toHaveLength(1);

    releaseProviders();
    await expect.element(page.getByTestId("register-email")).toBeInTheDocument();
  });

  it("skips the client-tenant lookup when no client_id is supplied", async () => {
    // Oracle: `if (!string.IsNullOrEmpty(ClientId))` gates ResolveOrgNameAsync.
    await renderReadyForm();

    expect(callsTo(TENANT_ENDPOINT)).toHaveLength(0);
    // Anchor: init genuinely ran, so the negative above is about the gate.
    expect(callsTo(PROVIDERS_ENDPOINT)).toHaveLength(1);
    expect(page.getByTestId("register-org-name").query()).toBeNull();
  });

  it("announces the resolved organisation when a client_id maps to one", async () => {
    // Oracle: "You're registering for @_orgName".
    await renderReadyForm({ clientId: CLIENT_ID });

    await expect.element(page.getByTestId("register-org-name")).toHaveTextContent(/acme inc/iu);
  });

  it("ignores a failed client-tenant lookup — the org name is informational only", async () => {
    // Oracle swallows HttpRequestException; the endpoint 404s for an unknown
    // client. A registration form must not be blocked by a cosmetic lookup.
    routes.tenant = notFound();

    await renderReadyForm({ clientId: CLIENT_ID });

    // Anchored: the form is usable and no error banner was raised.
    await expect.element(page.getByTestId("register-submit")).toBeInTheDocument();
    expect(page.getByTestId("register-org-name").query()).toBeNull();
    expect(page.getByTestId("register-error").query()).toBeNull();
  });

  it("links external providers same-origin, WITHOUT the oracle's ApiBaseUrl prepend", async () => {
    // Finding 4. The oracle builds `{ApiBaseUrl}/v1/...` for a cross-origin API;
    // wallow-auth is same-origin behind the SDK passthrough, so the origin stays "".
    routes.providers = ok(["Google"]);

    await renderReadyForm();

    const link = page.getByTestId("register-external-google");
    await expect.element(link).toBeInTheDocument();
    const href: string = link.element().getAttribute("href") ?? "";

    expect(href.startsWith("/v1/identity/auth/external-login")).toBe(true);
    expect(href).toContain("provider=Google");
    expect(href).not.toContain("http");
    expect(href).not.toContain("5001");
  });

  it("announces each provider challenge as a link, not a button", async () => {
    // The provider control is the catalog Button composed onto `<a href>`, and
    // Base UI stamps `role="button"` on every non-native element it substitutes.
    // The catalog supplies the link role itself now (Wallow-lrlm.12); this call
    // site used to pass `role="link"` by hand, unasserted. The consent-link
    // assertions further down cover PLAIN anchors, so they never covered this.
    routes.providers = ok(["Google"]);

    await renderReadyForm();

    const link = page.getByTestId("register-external-google");
    await expect.element(link).toBeInTheDocument();

    expect(page.getByRole("link", { name: "Google" }).query()).toBe(link.element());
    expect(page.getByRole("button", { name: "Google" }).query()).toBeNull();
  });

  it("renders no provider section when the API offers none", async () => {
    // Oracle: `@if (_externalProviders.Count > 0)`.
    await renderReadyForm();

    await expect.element(page.getByTestId("register-submit")).toBeInTheDocument();
    expect(page.getByTestId("register-external-providers").query()).toBeNull();
  });
});

describe("RegisterForm — fields and validation", () => {
  it("renders the oracle's fields, and no error before submit", async () => {
    await renderReadyForm();

    await expect.element(page.getByTestId("register-email")).toBeInTheDocument();
    await expect.element(page.getByTestId("register-password")).toBeInTheDocument();
    await expect.element(page.getByTestId("register-confirm-password")).toBeInTheDocument();
    await expect.element(page.getByTestId("register-terms")).toBeInTheDocument();
    await expect.element(page.getByTestId("register-privacy")).toBeInTheDocument();
    await expect.element(page.getByTestId("register-submit")).toBeInTheDocument();
    expect(page.getByTestId("register-error").query()).toBeNull();
  });

  it("masks both password fields", async () => {
    await renderReadyForm();

    await expect.element(page.getByTestId("register-password")).toHaveAttribute("type", "password");
    await expect
      .element(page.getByTestId("register-confirm-password"))
      .toHaveAttribute("type", "password");
  });

  it("refuses a blank email", async () => {
    // Oracle: "Please enter your email address."
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user, { email: "" });

    await expect.element(page.getByTestId("register-error")).toHaveTextContent(/email/iu);
    expect(callsTo(REGISTER_ENDPOINT)).toHaveLength(0);
  });

  it("refuses a blank password when not passwordless", async () => {
    // Oracle: "Please enter a password."
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user, { password: "", confirmPassword: "" });

    await expect.element(page.getByTestId("register-error")).toHaveTextContent(/password/iu);
    expect(callsTo(REGISTER_ENDPOINT)).toHaveLength(0);
  });

  it("refuses mismatched passwords before calling the API", async () => {
    // Oracle: "Passwords do not match." The server would also reject this
    // (400 passwords_do_not_match), but that 400 is indistinguishable from
    // email_taken at the seam (Finding 1) — so the local guard is what lets the
    // user see the real reason. Pinned.
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user, { password: PASSWORD, confirmPassword: "Different-1!" });

    await expect.element(page.getByTestId("register-error")).toHaveTextContent(/do not match/iu);
    expect(callsTo(REGISTER_ENDPOINT)).toHaveLength(0);
  });

  it("refuses to submit without agreeing to the Terms of Service", async () => {
    // Oracle: "You must agree to the Terms of Service."
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user, { terms: false });

    await expect
      .element(page.getByTestId("register-error"))
      .toHaveTextContent(/terms of service/iu);
    expect(callsTo(REGISTER_ENDPOINT)).toHaveLength(0);
  });

  it("refuses to submit without agreeing to the Privacy Policy", async () => {
    // Oracle: "You must agree to the Privacy Policy."
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user, { privacy: false });

    await expect.element(page.getByTestId("register-error")).toHaveTextContent(/privacy policy/iu);
    expect(callsTo(REGISTER_ENDPOINT)).toHaveLength(0);
  });

  it("links out to the Terms and the Privacy Policy", async () => {
    // The oracle's two inline consent links. No testids in the oracle, so
    // asserted by role + href.
    await renderReadyForm();

    await expect
      .element(page.getByRole("link", { name: /terms of service/iu }))
      .toHaveAttribute("href", "/terms");
    await expect
      .element(page.getByRole("link", { name: /privacy policy/iu }))
      .toHaveAttribute("href", "/privacy");
  });
});

describe("RegisterForm — the consent boxes' accessible state", () => {
  // Wallow-m5aq.5.2 — the catalog sweep. Both consent boxes and the passwordless
  // toggle are raw `<input type="checkbox">` today; the ui catalog covers them.
  // These tests ask for the swap in the only terms a screen reader can observe.

  it("publishes each consent box's checked state as aria-checked", async () => {
    // A raw `<input type="checkbox">` keeps its state in the `checked` PROPERTY,
    // which no attribute reflects; the catalog's Checkbox publishes it as
    // `aria-checked`.
    const user = userEvent.setup();
    await renderReadyForm();

    await expect
      .element(page.getByTestId("register-terms"))
      .toHaveAttribute("aria-checked", "false");
    await expect
      .element(page.getByTestId("register-privacy"))
      .toHaveAttribute("aria-checked", "false");

    await toggleCheckbox(user, "register-terms");

    await expect
      .element(page.getByTestId("register-terms"))
      .toHaveAttribute("aria-checked", "true");
    await expect
      .element(page.getByTestId("register-privacy"))
      .toHaveAttribute("aria-checked", "false");
  });

  it("publishes the passwordless toggle's checked state as aria-checked", async () => {
    // The inventory leaves Checkbox-vs-Switch open for this one, so this pins only
    // what BOTH publish: the toggled state, as an attribute, on the tagged element.
    const user = userEvent.setup();
    await renderReadyForm();

    await expect
      .element(page.getByTestId("register-passwordless-toggle"))
      .toHaveAttribute("aria-checked", "false");

    await toggleCheckbox(user, "register-passwordless-toggle");

    await expect
      .element(page.getByTestId("register-passwordless-toggle"))
      .toHaveAttribute("aria-checked", "true");
  });

  it("keeps each consent box named by its own label", async () => {
    // The `htmlFor`/`id` pairing asserted through what it buys: two boxes a user
    // can tell apart. A migration that drops the pairing leaves two unnamed
    // controls that differ only in DOM order.
    await renderReadyForm();

    await expect
      .element(page.getByRole("checkbox", { name: "I agree to the Terms of Service" }))
      .toBeInTheDocument();
    await expect
      .element(page.getByRole("checkbox", { name: "I agree to the Privacy Policy" }))
      .toBeInTheDocument();
  });
});

describe("RegisterForm — passwordless toggle", () => {
  it("hides the password fields when passwordless is selected", async () => {
    // Oracle: `@if (!_isPasswordless)` wraps both password blocks.
    const user = userEvent.setup();
    await renderReadyForm();

    // Anchor: they are on screen first, so the disappearance is real.
    await expect.element(page.getByTestId("register-password")).toBeInTheDocument();

    await toggleCheckbox(user, "register-passwordless-toggle");

    expect(page.getByTestId("register-password").query()).toBeNull();
    expect(page.getByTestId("register-confirm-password").query()).toBeNull();
    await expect.element(page.getByTestId("register-submit")).toBeInTheDocument();
  });

  it("sends loginMethod 'passwordless' and skips the password guards", async () => {
    // Oracle: `string? loginMethod = _isPasswordless ? "passwordless" : null;`
    // and the password/mismatch guards sit inside `if (!_isPasswordless)`.
    const user = userEvent.setup();
    await renderReadyForm();

    await toggleCheckbox(user, "register-passwordless-toggle");
    await user.type(page.getByTestId("register-email"), EMAIL);
    await toggleCheckbox(user, "register-terms");
    await toggleCheckbox(user, "register-privacy");
    await user.click(page.getByTestId("register-submit"));

    await vi.waitFor(() => {
      expect(registerBody()).toMatchObject({ email: EMAIL, loginMethod: "passwordless" });
    });
  });

  it("sends no loginMethod when a password is used", async () => {
    // Oracle passes null for the password flow.
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user);

    await vi.waitFor(() => {
      expect(callsTo(REGISTER_ENDPOINT)).toHaveLength(1);
    });
    // Read off the SERIALISED body, so this covers the wire too: a `null` that
    // the serializer dropped and an omitted key are the same answer here.
    expect(registerBody()?.["loginMethod"] ?? null).toBeNull();
  });
});

describe("RegisterForm — password strength meter", () => {
  /** Type a password and read back the meter's label. */
  async function strengthOf(user: ReturnType<typeof userEvent.setup>, password: string) {
    await user.type(page.getByTestId("register-password"), password);
    return page.getByTestId("register-password-strength");
  }

  it("shows no meter until a password is typed", async () => {
    // Oracle: `@if (!string.IsNullOrEmpty(_password))` gates the BbProgress.
    await renderReadyForm();

    // Anchored: the field exists, so the meter's absence is about emptiness.
    await expect.element(page.getByTestId("register-password")).toHaveValue("");
    expect(page.getByTestId("register-password-strength").query()).toBeNull();
  });

  it("rates a short password Weak", async () => {
    // Oracle's `else` branch: length < 8 => 25 / "Weak".
    const user = userEvent.setup();
    await renderReadyForm();

    await expect.element(await strengthOf(user, "abc")).toHaveTextContent(/weak/iu);
  });

  it("rates an 8-character password Fair", async () => {
    // Oracle: `_password.Length >= 8` => 50 / "Fair".
    const user = userEvent.setup();
    await renderReadyForm();

    await expect.element(await strengthOf(user, "abcdefgh")).toHaveTextContent(/fair/iu);
  });

  it("rates a long mixed password Strong", async () => {
    // Oracle: `Length >= 12 && hasUpper && hasLower && (hasDigit || hasSpecial)`
    // => 100 / "Strong".
    const user = userEvent.setup();
    await renderReadyForm();

    await expect.element(await strengthOf(user, "Abcdefgh1234")).toHaveTextContent(/strong/iu);
  });

  it("does not rate a long password Strong on length alone — the mix is required", async () => {
    // 12 chars but all lowercase: fails hasUpper, so the oracle falls through to
    // the `Length >= 8` branch => "Fair". Pins that the port ports the WHOLE
    // predicate and not just the length check.
    const user = userEvent.setup();
    await renderReadyForm();

    await expect.element(await strengthOf(user, "abcdefghijkl")).toHaveTextContent(/fair/iu);
  });

  it("updates the rating live as the password grows", async () => {
    // Oracle recomputes on every ValueChanged, not just on submit.
    const user = userEvent.setup();
    await renderReadyForm();

    await user.type(page.getByTestId("register-password"), "abc");
    await expect
      .element(page.getByTestId("register-password-strength"))
      .toHaveTextContent(/weak/iu);

    await user.type(page.getByTestId("register-password"), "defgh");
    await expect
      .element(page.getByTestId("register-password-strength"))
      .toHaveTextContent(/fair/iu);
  });
});

describe("RegisterForm — submission", () => {
  it("sends the typed credentials with the query's client_id and returnUrl", async () => {
    // Oracle: `new RegisterRequest(_email, _password, _confirmPassword, ClientId,
    // loginMethod, ReturnUrl)`.
    const user = userEvent.setup();
    await renderReadyForm({ clientId: CLIENT_ID, returnUrl: RETURN_URL });

    await fillAndSubmit(user);

    await vi.waitFor(() => {
      expect(registerBody()).toMatchObject({
        email: EMAIL,
        password: PASSWORD,
        confirmPassword: PASSWORD,
        clientId: CLIENT_ID,
        returnUrl: RETURN_URL,
      });
    });
    expect(callsTo(REGISTER_ENDPOINT).at(-1)?.method).toBe("POST");
  });

  it("disables the submit while the registration is in flight", async () => {
    // Oracle: `Loading="_isSubmitting" Disabled="_isSubmitting"`.
    const user = userEvent.setup();
    let release!: () => void;
    routes.register = async () =>
      await new Promise<Response>((resolve) => {
        release = () => {
          resolve(Response.json({ succeeded: true }, { status: 200 }));
        };
      });

    await renderReadyForm();
    await fillAndSubmit(user);

    // Wait for the POST to REACH the transport before releasing it: the button
    // goes disabled the moment the mutation starts, a tick or two before `fetch`
    // is called, and releasing into that gap would leave the never-settling
    // promise installed forever.
    await vi.waitFor(() => {
      expect(callsTo(REGISTER_ENDPOINT)).toHaveLength(1);
    });
    await expect.element(page.getByTestId("register-submit")).toBeDisabled();

    release();
    await vi.waitFor(() => {
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
    routes.register = failure(BAD_REQUEST, "email_taken");

    await renderReadyForm();
    await fillAndSubmit(user);

    await expect.element(page.getByTestId("register-error")).toHaveTextContent(/already exists/iu);
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("tells the user the passwords do not match when the SERVER rejects them", async () => {
    // 400 `passwords_do_not_match` (controller line 648). The local guard catches
    // this first in the ordinary case, so this pins the path where the server is
    // the one that notices — and the message must name the real reason rather
    // than fall to the generic tail.
    const user = userEvent.setup();
    routes.register = failure(BAD_REQUEST, "passwords_do_not_match");

    await renderReadyForm();
    await fillAndSubmit(user);

    await expect.element(page.getByTestId("register-error")).toHaveTextContent(/do not match/iu);
  });

  it("does not blame the user's input when the client_id is invalid", async () => {
    // 400 `invalid_client_id` (controller line 658). The `client_id` came off the
    // QUERY STRING, not the form: nothing the user typed is wrong and retyping it
    // cannot help. Pins that the copy points at the LINK, and specifically that
    // this failure is not mislabelled as a duplicate email — the mistake a
    // blanket `400 -> email_taken` rule would make.
    const user = userEvent.setup();
    routes.register = failure(BAD_REQUEST, "invalid_client_id");

    await renderReadyForm({ clientId: "not-a-real-client" });

    await fillAndSubmit(user);

    const banner = page.getByTestId("register-error");
    await expect.element(banner).toHaveTextContent(/link/iu);
    await expect.element(banner).not.toHaveTextContent(/already exists/iu);
  });

  it("falls back to a generic message for a weak password, which has NO stable code", async () => {
    // The fourth 400 and the one Wallow-vec7.7 could not recover: the controller's
    // `_ => result.Errors.First().Description` fallback emits a raw sentence, not
    // a token, so there is nothing to key on and the generic branch stands. This
    // is the honest floor, not a downgrade.
    const user = userEvent.setup();
    routes.register = failure(BAD_REQUEST, RAW_IDENTITY_SENTENCE);

    await renderReadyForm();
    await fillAndSubmit(user, { password: "weak", confirmPassword: "weak" });
    const banner = page.getByTestId("register-error");
    await expect.element(banner).toBeInTheDocument();
    const weakMessage: string = banner.element().textContent ?? "";

    expect(weakMessage).not.toBe("");
    expect(weakMessage).toMatch(/try again/iu);
    // Anchored both ways: a port that guessed from the shared 400 would print one
    // of the mapped messages here instead of the tail.
    expect(weakMessage).not.toMatch(/already exists/iu);
    expect(weakMessage).not.toMatch(/do not match/iu);
  });

  it("never leaks the API's raw password-rule sentence into the banner", async () => {
    // The API's `_ => result.Error` tail renders the server string VERBATIM, so
    // a user really can be shown Identity's own prose. `code` is a machine
    // member: matched against known tokens, never rendered. Not ported.
    const user = userEvent.setup();
    routes.register = failure(BAD_REQUEST, RAW_IDENTITY_SENTENCE);

    await renderReadyForm();
    await fillAndSubmit(user, { password: "weak", confirmPassword: "weak" });

    const banner = page.getByTestId("register-error");
    await expect.element(banner).toBeInTheDocument();
    await expect.element(banner).not.toHaveTextContent(RAW_IDENTITY_SENTENCE);
    await expect.element(banner).not.toHaveTextContent(/'0'-'9'/u);
  });

  it("falls back to the generic message for an UNRECOGNISED code on the SAME 400", async () => {
    // THE TEST THAT BINDS THE MAPPING (bd memory `code-keyed-error-mapping-needs-
    // an-unrecognised-code-test-to-bind`). Every token this endpoint sends shares
    // a 400, so the per-token tests above ALL pass under a blanket `400 -> "email
    // already exists"` rule. This one does not: it pins that the screen matches
    // the CODE and guesses at nothing. A token the API adds tomorrow must read as
    // the generic tail rather than as a confident lie.
    const user = userEvent.setup();
    routes.register = failure(BAD_REQUEST, "some_future_token");

    await renderReadyForm();
    await fillAndSubmit(user);

    const banner = page.getByTestId("register-error");
    await expect.element(banner).toHaveTextContent(/try again/iu);
    await expect.element(banner).not.toHaveTextContent(/already exists/iu);
    // ...and it is not leaked, either.
    await expect.element(banner).not.toHaveTextContent(/some_future_token/u);
  });

  it("surfaces a generic message when the server errors outright", async () => {
    const user = userEvent.setup();
    routes.register = () => Response.json({}, { status: SERVER_ERROR });

    await renderReadyForm();
    await fillAndSubmit(user);

    await expect.element(page.getByTestId("register-error")).toHaveTextContent(/try again/iu);
  });

  it("falls back to the generic message when the rejection carries no code at all", async () => {
    // A network-level failure — DNS, offline, CORS — reaches `onError` as a plain
    // Error with neither `code` nor `status`. Structural narrowing must tolerate
    // that rather than throw inside the error handler.
    const user = userEvent.setup();
    // A transport that THROWS is the honest way to produce one: `fetch`
    // rejecting IS the network failure, and the SDK's error interceptor has no
    // response to read a status off.
    routes.register = () => {
      throw new Error("Failed to fetch");
    };

    await renderReadyForm();
    await fillAndSubmit(user);

    await expect.element(page.getByTestId("register-error")).toHaveTextContent(/try again/iu);
  });

  it("clears a stale error banner when a retry succeeds", async () => {
    // Oracle: `_errorMessage = null;` at the top of HandleRegister. A stale
    // failure sitting above a successful registration would be a lie.
    const user = userEvent.setup();
    // ONCE: the first attempt fails, the retry succeeds. The leg reprograms
    // itself so the second POST lands on the default happy response.
    routes.register = () => {
      routes.register = ok({ succeeded: true });
      return Response.json({ succeeded: false, error: "email_taken" }, { status: BAD_REQUEST });
    };

    await renderReadyForm();
    await fillAndSubmit(user);
    // Anchor: the banner is genuinely there before the retry clears it.
    await expect.element(page.getByTestId("register-error")).toBeInTheDocument();

    await user.click(page.getByTestId("register-submit"));

    await expect.element(page.getByTestId("register-error")).not.toBeInTheDocument();
  });
});

describe("RegisterForm — post-submit navigation", () => {
  it("submits straight to register, with no org-match interstitial (F6 removed it)", async () => {
    // F6 deleted the anonymous org-domain-match lookup and the register
    // opt-in flag, so a passing local guard set now creates the account
    // directly — no interstitial ever stands between the click and register.
    const user = userEvent.setup();

    await renderReadyForm();
    await fillAndSubmit(user);

    await vi.waitFor(() => {
      expect(registerBody()).toMatchObject({ email: EMAIL });
    });
    expect(page.getByTestId("register-org-match").query()).toBeNull();
  });

  it("threads a safe returnUrl through to verify-email", async () => {
    // Oracle: `VerifyEmailUrl` = `/verify-email?returnUrl={escaped}`.
    const user = userEvent.setup();

    await renderReadyForm({ returnUrl: RETURN_URL });
    await fillAndSubmit(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({
        href: `/verify-email?returnUrl=${encodeURIComponent(RETURN_URL)}`,
      });
    });
  });
});

describe("RegisterForm — open-redirect guard", () => {
  it("REFUSES an unsafe returnUrl instead of sanitising it away", async () => {
    // bd memory `returnurl-guard-refuse-dont-sanitize`: on an unsafe returnUrl,
    // route to /error?reason=invalid_redirect_uri. Do NOT silently fall back to
    // "/" — sanitising the value away is deliberately not done.
    const user = userEvent.setup();

    await renderReadyForm({ returnUrl: "//evil.example.com/steal" });
    await fillAndSubmit(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: "/error?reason=invalid_redirect_uri" });
    });
    expect(mocks.navigate).not.toHaveBeenCalledWith({ href: "/verify-email" });
  });

  it("REFUSES an absolute returnUrl", async () => {
    const user = userEvent.setup();

    await renderReadyForm({ returnUrl: "https://evil.example.com/steal" });
    await fillAndSubmit(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: "/error?reason=invalid_redirect_uri" });
    });
  });

  it("lets a missing returnUrl through to the plain verify-email URL", async () => {
    // Only a NULLISH returnUrl gets the fallback; the guard runs on a PRESENT
    // one only. An absent param is not an attack.
    const user = userEvent.setup();

    await renderReadyForm();
    await fillAndSubmit(user);

    await vi.waitFor(() => {
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
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/register", route: registerRoute }],
  });
}

describe("/register route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    // Wallow-vec7.3.16 registered this path against a placeholder component;
    // this task's job is to replace it. The path is the contract.
    renderRouteAt("/register");

    await expect.element(page.getByTestId("register-email")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
  });

  it("reads client_id and returnUrl off the query string and threads them into the register call", async () => {
    // The oracle's two `[SupplyParameterFromQuery]` properties — note `client_id`
    // is snake_case on the wire and `returnUrl` is not.
    const user = userEvent.setup();
    renderRouteAt(`/register?client_id=${CLIENT_ID}&returnUrl=${encodeURIComponent(RETURN_URL)}`);

    await expect.element(page.getByTestId("register-email")).toBeInTheDocument();
    await fillAndSubmit(user);

    await vi.waitFor(() => {
      expect(registerBody()).toMatchObject({ clientId: CLIENT_ID, returnUrl: RETURN_URL });
    });
  });

  it("still renders for a bare /register with no query at all", async () => {
    // A mangled or bare link must render the form, not throw a search-validation
    // error at the user.
    renderRouteAt("/register");

    await expect.element(page.getByTestId("register-submit")).toBeInTheDocument();
    expect(callsTo(TENANT_ENDPOINT)).toHaveLength(0);
  });
});
