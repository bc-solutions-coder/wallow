import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createPassthroughHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as registerRoute } from "@app/routes/register";
import { RegisterForm } from "./RegisterForm";

/**
 * Register screen: init, validation, the passwordless toggle, the strength
 * meter, submission and its error mapping, navigation, and the `/register` route.
 *
 * Runs the real SDK over a faked fetch (sdk-harness), so assertions read the
 * recorded request, not a spy. Three requests are in flight against the one
 * transport — providers and client-tenant on mount, register on submit — so it
 * is routed per path and each test reprograms only the leg it is about.
 */

/**
 * `navigate` is the ONE spy. Navigation is a router concern with no request
 * behind it to observe it through; the SDK itself is never mocked.
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

/** The three endpoints this screen touches; every request assertion discriminates on them. */
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
 * `{ succeeded: false, error }` object, NOT problem details. `api-errors`
 * parses that OAuth-shaped body into an `OAuth.<Token>` failure whose `title`
 * is the raw token, and `readErrorCode` hands the token back to the screen —
 * which is what the per-token tests key on.
 */
function failure(status: number, code: string): Leg {
  return () => Response.json({ succeeded: false, error: code }, { status });
}

/**
 * The client-tenant lookup answers a miss with a bare `NotFound()` — no body, so
 * the parser hands the screen `Client.UnrecognizedResponse` at 404.
 */
function notFound(): Leg {
  return () => Response.json(null, { status: NOT_FOUND });
}

/**
 * The weak-password rejection, shaped as it REALLY arrives: the API puts a raw
 * English sentence where a token belongs, and `readCode` faithfully surfaces it
 * as `code`. This is why the no-leak test below is not theoretical.
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

/** Toggle a checkbox by CLICKING it, the way a pointer user does. */
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
  harness = createPassthroughHarness();
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
    // A sequential implementation still passes a both-were-called assertion, so
    // this pins that the SECOND call is issued before the FIRST has resolved.
    let releaseProviders!: () => void;
    routes.providers = async () =>
      await new Promise<Response>((resolve) => {
        releaseProviders = () => {
          resolve(Response.json([], { status: 200 }));
        };
      });

    renderForm({ clientId: CLIENT_ID });

    // The tenant request reaches the wire while the provider request is still
    // un-answered.
    await vi.waitFor(() => {
      expect(callsTo(`${TENANT_ENDPOINT}/${CLIENT_ID}`)).toHaveLength(1);
    });
    expect(callsTo(PROVIDERS_ENDPOINT)).toHaveLength(1);

    releaseProviders();
    await expect.element(page.getByTestId("register-email")).toBeInTheDocument();
  });

  it("skips the client-tenant lookup when no client_id is supplied", async () => {
    await renderReadyForm();

    expect(callsTo(TENANT_ENDPOINT)).toHaveLength(0);
    // Anchor: init genuinely ran, so the negative above is about the gate.
    expect(callsTo(PROVIDERS_ENDPOINT)).toHaveLength(1);
    expect(page.getByTestId("register-org-name").query()).toBeNull();
  });

  it("announces the resolved organisation when a client_id maps to one", async () => {
    await renderReadyForm({ clientId: CLIENT_ID });

    await expect.element(page.getByTestId("register-org-name")).toHaveTextContent(/acme inc/iu);
  });

  it("ignores a failed client-tenant lookup — the org name is informational only", async () => {
    // The endpoint 404s for an unknown client, and a registration form must not
    // be blocked by a cosmetic lookup.
    routes.tenant = notFound();

    await renderReadyForm({ clientId: CLIENT_ID });

    // Anchored: the form is usable and no error banner was raised.
    await expect.element(page.getByTestId("register-submit")).toBeInTheDocument();
    expect(page.getByTestId("register-org-name").query()).toBeNull();
    expect(page.getByTestId("register-error").query()).toBeNull();
  });

  it("links external providers same-origin, WITHOUT the oracle's ApiBaseUrl prepend", async () => {
    // This app is same-origin behind the SDK passthrough proxy, so the link's
    // origin stays empty — an absolute API origin is not reachable from here.
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
    // Base UI stamps `role="button"` on every non-native element it substitutes
    // — so the composed anchor has to reclaim the link role. The consent-link
    // assertions below cover PLAIN anchors and never reach this.
    routes.providers = ok(["Google"]);

    await renderReadyForm();

    const link = page.getByTestId("register-external-google");
    await expect.element(link).toBeInTheDocument();

    expect(page.getByRole("link", { name: "Google" }).query()).toBe(link.element());
    expect(page.getByRole("button", { name: "Google" }).query()).toBeNull();
  });

  it("renders no provider section when the API offers none", async () => {
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
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user, { email: "" });

    await expect.element(page.getByTestId("register-error")).toHaveTextContent(/email/iu);
    expect(callsTo(REGISTER_ENDPOINT)).toHaveLength(0);
  });

  it("refuses a blank password when not passwordless", async () => {
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user, { password: "", confirmPassword: "" });

    await expect.element(page.getByTestId("register-error")).toHaveTextContent(/password/iu);
    expect(callsTo(REGISTER_ENDPOINT)).toHaveLength(0);
  });

  it("refuses mismatched passwords before calling the API", async () => {
    // The server rejects this too, but its 400 is shared across four reasons —
    // the local guard is what lets the user see the real one straight away.
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user, { password: PASSWORD, confirmPassword: "Different-1!" });

    await expect.element(page.getByTestId("register-error")).toHaveTextContent(/do not match/iu);
    expect(callsTo(REGISTER_ENDPOINT)).toHaveLength(0);
  });

  it("refuses to submit without agreeing to the Terms of Service", async () => {
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user, { terms: false });

    await expect
      .element(page.getByTestId("register-error"))
      .toHaveTextContent(/terms of service/iu);
    expect(callsTo(REGISTER_ENDPOINT)).toHaveLength(0);
  });

  it("refuses to submit without agreeing to the Privacy Policy", async () => {
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user, { privacy: false });

    await expect.element(page.getByTestId("register-error")).toHaveTextContent(/privacy policy/iu);
    expect(callsTo(REGISTER_ENDPOINT)).toHaveLength(0);
  });

  it("links out to the Terms and the Privacy Policy", async () => {
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
  it("publishes each consent box's checked state as aria-checked", async () => {
    // A raw `<input type="checkbox">` keeps its state in the `checked` PROPERTY,
    // which no attribute reflects and no screen reader can be told about; the
    // catalog's Checkbox publishes it as `aria-checked`.
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
    // Checkbox or Switch — this pins only what BOTH publish: the toggled state,
    // as an attribute, on the tagged element.
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
    // The `htmlFor`/`id` pairing asserted through what it buys: without it the
    // two boxes are unnamed controls differing only in DOM order.
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
    const user = userEvent.setup();
    await renderReadyForm();

    await fillAndSubmit(user);

    await vi.waitFor(() => {
      expect(callsTo(REGISTER_ENDPOINT)).toHaveLength(1);
    });
    // Read off the SERIALISED body: a `null` the serializer dropped and an
    // omitted key are the same answer here.
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
    await renderReadyForm();

    // Anchored: the field exists, so the meter's absence is about emptiness.
    await expect.element(page.getByTestId("register-password")).toHaveValue("");
    expect(page.getByTestId("register-password-strength").query()).toBeNull();
  });

  it("rates a short password Weak", async () => {
    const user = userEvent.setup();
    await renderReadyForm();

    await expect.element(await strengthOf(user, "abc")).toHaveTextContent(/weak/iu);
  });

  it("rates an 8-character password Fair", async () => {
    const user = userEvent.setup();
    await renderReadyForm();

    await expect.element(await strengthOf(user, "abcdefgh")).toHaveTextContent(/fair/iu);
  });

  it("rates a long mixed password Strong", async () => {
    const user = userEvent.setup();
    await renderReadyForm();

    await expect.element(await strengthOf(user, "Abcdefgh1234")).toHaveTextContent(/strong/iu);
  });

  it("does not rate a long password Strong on length alone — the mix is required", async () => {
    // 12 chars but all lowercase, so it falls back to Fair — pinning that the
    // rating reads the WHOLE character-mix predicate, not just the length.
    const user = userEvent.setup();
    await renderReadyForm();

    await expect.element(await strengthOf(user, "abcdefghijkl")).toHaveTextContent(/fair/iu);
  });

  it("updates the rating live as the password grows", async () => {
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
    // The most actionable failure this form has: the user already has an
    // account and should sign in rather than retry.
    const user = userEvent.setup();
    routes.register = failure(BAD_REQUEST, "email_taken");

    await renderReadyForm();
    await fillAndSubmit(user);

    await expect.element(page.getByTestId("register-error")).toHaveTextContent(/already exists/iu);
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("tells the user the passwords do not match when the SERVER rejects them", async () => {
    // The local guard catches this first in the ordinary case, so this is the
    // path where the SERVER is the one that notices — and the message must still
    // name the real reason rather than fall to the generic tail.
    const user = userEvent.setup();
    routes.register = failure(BAD_REQUEST, "passwords_do_not_match");

    await renderReadyForm();
    await fillAndSubmit(user);

    await expect.element(page.getByTestId("register-error")).toHaveTextContent(/do not match/iu);
  });

  it("does not blame the user's input when the client_id is invalid", async () => {
    // The `client_id` came off the QUERY STRING, not the form: nothing the user
    // typed is wrong and retyping cannot help, so the copy must point at the
    // LINK — and must not mislabel this as a duplicate email.
    const user = userEvent.setup();
    routes.register = failure(BAD_REQUEST, "invalid_client_id");

    await renderReadyForm({ clientId: "not-a-real-client" });

    await fillAndSubmit(user);

    const banner = page.getByTestId("register-error");
    await expect.element(banner).toHaveTextContent(/link/iu);
    await expect.element(banner).not.toHaveTextContent(/already exists/iu);
  });

  it("falls back to a generic message for a weak password, which has NO stable code", async () => {
    // The one 400 with no recoverable reason: the API emits a raw sentence here
    // rather than a token, so there is nothing to key on and the generic branch
    // stands. That is the honest floor, not a downgrade.
    const user = userEvent.setup();
    routes.register = failure(BAD_REQUEST, RAW_IDENTITY_SENTENCE);

    await renderReadyForm();
    await fillAndSubmit(user, { password: "weak", confirmPassword: "weak" });
    const banner = page.getByTestId("register-error");
    await expect.element(banner).toBeInTheDocument();
    const weakMessage: string = banner.element().textContent ?? "";

    expect(weakMessage).not.toBe("");
    expect(weakMessage).toMatch(/try again/iu);
    // Anchored both ways: an implementation guessing from the shared 400 would
    // print one of the mapped messages here instead of the tail.
    expect(weakMessage).not.toMatch(/already exists/iu);
    expect(weakMessage).not.toMatch(/do not match/iu);
  });

  it("never leaks the API's raw password-rule sentence into the banner", async () => {
    // `code` is a machine member: matched against known tokens, never rendered.
    // Echoing it would show the user Identity's own internal prose.
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
    // THE TEST THAT BINDS THE MAPPING. Every token this endpoint sends shares a
    // 400, so the per-token tests above ALL pass under a blanket `400 -> "email
    // already exists"` rule. This one does not: a token the API adds tomorrow
    // must read as the generic tail rather than as a confident lie.
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
    // A network-level failure — DNS, offline, CORS — reaches `onError` as a
    // plain Error with neither `code` nor `status`, and the narrowing must
    // tolerate that rather than throw inside the error handler. A transport that
    // THROWS is the honest way to produce one: `fetch` rejecting IS the failure,
    // and the SDK's error interceptor has no response to read a status off.
    const user = userEvent.setup();
    routes.register = () => {
      throw new Error("Failed to fetch");
    };

    await renderReadyForm();
    await fillAndSubmit(user);

    await expect.element(page.getByTestId("register-error")).toHaveTextContent(/try again/iu);
  });

  it("clears a stale error banner when a retry succeeds", async () => {
    // A stale failure sitting above a successful registration is a lie.
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
  it("submits straight to register, with no org-match interstitial", async () => {
    // A passing local guard set creates the account directly: no interstitial
    // stands between the click and register.
    const user = userEvent.setup();

    await renderReadyForm();
    await fillAndSubmit(user);

    await vi.waitFor(() => {
      expect(registerBody()).toMatchObject({ email: EMAIL });
    });
    expect(page.getByTestId("register-org-match").query()).toBeNull();
  });

  it("threads a safe returnUrl through to verify-email", async () => {
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
    // REFUSE, do not sanitise: an unsafe returnUrl routes to
    // /error?reason=invalid_redirect_uri rather than falling back to "/".
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
    // The guard runs on a PRESENT returnUrl only — an absent one is not an
    // attack, so it gets the plain fallback rather than /error.
    const user = userEvent.setup();

    await renderReadyForm();
    await fillAndSubmit(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: "/verify-email" });
    });
  });
});

/**
 * Route-level spec, rendered through a real memory router rather than by poking
 * at `Route.options.component`, because the criterion under test — client_id and
 * returnUrl read off the query string — only exists once a URL is parsed. The
 * root is a throwaway; the app's real `__root.tsx` renders `<html>`.
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
    renderRouteAt("/register");

    await expect.element(page.getByTestId("register-email")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
  });

  it("reads client_id and returnUrl off the query string and threads them into the register call", async () => {
    // `client_id` is snake_case on the wire and `returnUrl` is not.
    const user = userEvent.setup();
    renderRouteAt(`/register?client_id=${CLIENT_ID}&returnUrl=${encodeURIComponent(RETURN_URL)}`);

    await expect.element(page.getByTestId("register-email")).toBeInTheDocument();
    await fillAndSubmit(user);

    await vi.waitFor(() => {
      expect(registerBody()).toMatchObject({ clientId: CLIENT_ID, returnUrl: RETURN_URL });
    });
  });

  it("still renders for a bare /register with no query at all", async () => {
    // A bare link must render the form, not throw a search-validation error.
    renderRouteAt("/register");

    await expect.element(page.getByTestId("register-submit")).toBeInTheDocument();
    expect(callsTo(TENANT_ENDPOINT)).toHaveLength(0);
  });
});
