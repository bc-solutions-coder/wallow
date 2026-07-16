import { useId, useState, type ReactNode } from "react";

/**
 * The AcceptTerms screen (Wallow-vec7.3.10), ported from the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/AcceptTerms.razor`.
 *
 * This is the ToS/Privacy GATE in the external-login (social sign-up) flow — not
 * the static terms document, which is the separate `/terms` route
 * (Wallow-vec7.3.3). Testids come verbatim from the oracle (scout inventory on
 * Wallow-vec7.3): `accept-terms-heading`, `accept-terms-error`,
 * `accept-terms-checkbox`, `accept-terms-privacy-checkbox`,
 * `accept-terms-submit`.
 *
 * The four props are the oracle's four `[SupplyParameterFromQuery]` properties.
 * The route owns the query string and hands them down, keeping this component a
 * pure function of its inputs and testable without a router — the seam
 * `ResetPasswordForm` established and `ConsentScreen` followed.
 *
 * ── WHAT THIS SCREEN IS ──────────────────────────────────────────────────────
 *
 * `AccountController.external-login-callback` (api/src/Modules/Identity/
 * Wallow.Identity.Api/Controllers/AccountController.cs) data-protects the
 * external identity into the **ExternalLoginState** cookie (HttpOnly, Secure,
 * SameSite=Lax, 10 min) and redirects here. `complete-external-registration`
 * reads that cookie back, creates the user, signs them in, deletes the cookie,
 * and redirects to the validated returnUrl.
 *
 * The user's identity for that step lives ENTIRELY in a cookie this screen
 * cannot read. The screen holds no state beyond its two checkboxes, makes NO
 * request, and its only job is to hand the browser to the endpoint — the browser
 * attaches the cookie itself on the top-level same-origin GET (SameSite=Lax
 * permits exactly that). No relay, no session store, no token.
 *
 * ── NO `isSafeReturnUrl` GUARD (disclosed on the bead; adjudicated SAFE) ──────
 *
 * `returnUrl` is NOT guarded here, deliberately. `OpenIddictRedirectUriValidator.
 * IsAllowedAsync` bails unless `Uri.TryCreate(uri, UriKind.Absolute, …)` and then
 * origin-allow-lists; `external-login` refuses to start the flow unless it passes,
 * and `external-login-callback` re-validates. So the `returnUrl` arriving here is
 * ALWAYS absolute and allow-listed, while `isSafeReturnUrl` returns true only for
 * a RELATIVE single-'/' path. The two accept-sets are provably disjoint: wiring
 * the guard in would send every social sign-up to
 * `/error?reason=invalid_redirect_uri`.
 *
 * This is the `buildConnectLogoutUrl` precedent, not the `buildConsentSubmitUrl`
 * one — that builder documents the identical reasoning for
 * `post_logout_redirect_uri`. The rule: guard where the CLIENT picks the
 * destination; defer where the SERVER does. Here the destination is a same-origin
 * CONSTANT path and `returnUrl` is inert query cargo, which
 * `complete-external-registration` re-validates against the same allow-list
 * "early, before any user creation", falling back to `authUrl`. The open-redirect
 * decision is the API's, made server-side.
 *
 * What this screen DOES owe is that the cargo cannot break out of the query
 * string — that is `encodeURIComponent` below, the injection guard that actually
 * applies here.
 */

/** The endpoint the gate hands the browser to. */
const COMPLETE_REGISTRATION_PATH = "/v1/identity/auth/complete-external-registration";

/**
 * The origin the handoff is built against: this one. The oracle prepends
 * `Configuration["ApiBaseUrl"] ?? "http://localhost:5001"`; that prepend is NOT
 * ported, for the reason `ConsentScreen` documents at length. This origin DOES
 * host `/v1/**` — `src/lib/auth-server.ts` mounts the passthrough reverse proxy
 * at the ROOT — so going cross-origin would drop the `SameSite=Lax`
 * ExternalLoginState cookie, which is the user's whole identity here, and the
 * endpoint would bounce them to `/login?error=session_expired`. It would also
 * reintroduce an `ApiBaseUrl` knob this app deliberately lacks:
 * `WALLOW_API_INTERNAL_URL` is a SERVER-side address the browser cannot resolve
 * at all. Named rather than inlined so the `""` reads as a decision rather than a
 * forgotten argument.
 */
const SAME_ORIGIN = "";

/** The oracle's `_ =>` arm. */
const GENERIC_ERROR_MESSAGE = "An error occurred. Please try again.";

/**
 * The oracle's `Error switch`. A `ReadonlyMap` + `.get()`, never a `Record` +
 * bracket lookup (bd memory `attacker-supplied-query-key-lookups-use-map-not-
 * record`): `?error=toString` is a URL anyone can send a victim, and an object
 * literal would resolve `Object.prototype.toString` — a FUNCTION handed to the
 * renderer. A Map only ever sees the keys put in it.
 *
 * `session_expired` is not reachable from the wire — `complete-external-
 * registration` sends every session-expired path to `/login?error=session_expired`,
 * and the only redirect back to THIS screen is `?error=terms_required`. The branch
 * is kept anyway: it is static copy, it costs nothing, and `?error=` is a query
 * string anyone can construct, so it deserves deliberate handling rather than
 * falling to the generic arm by accident.
 */
const ERROR_MESSAGES: ReadonlyMap<string, string> = new Map([
  ["terms_required", "You must accept the terms to continue."],
  ["session_expired", "Your session has expired. Please try signing in again."],
]);

/** The oracle's `data-testid="accept-terms-error"` alert block. */
function ErrorAlert({ code }: { readonly code: string }) {
  return (
    <div
      className="rounded-md border border-destructive bg-destructive/10 p-3"
      data-testid="accept-terms-error"
    >
      <p className="text-sm text-destructive">
        {ERROR_MESSAGES.get(code) ?? GENERIC_ERROR_MESSAGE}
      </p>
    </div>
  );
}

/**
 * The oracle's `@if (!string.IsNullOrEmpty(Email))` block — the user's only
 * chance to notice the provider handed over the wrong account BEFORE one gets
 * created. Gated on the email, so a link carrying no address renders nothing here
 * rather than an empty identity card.
 */
function SigningUpAs({ email, name }: { readonly email: string; readonly name?: string }) {
  return (
    <div className="rounded-md border border-border p-3 text-sm">
      <p className="text-muted-foreground">Signing up as</p>
      {name === undefined || name === "" ? null : (
        <p className="font-medium text-foreground">{name}</p>
      )}
      <p className="text-muted-foreground">{email}</p>
    </div>
  );
}

/**
 * One consent box: the oracle's `BbCheckbox` + `BbLabel` pair.
 *
 * The testid sits on the `<input>`. The oracle puts it on the wrapping `<div>`
 * (L44, L52), which cannot be clicked to toggle the box it wraps and which an E2E
 * `.check()` cannot reach; the NAME is preserved verbatim.
 *
 * The document link is `target="_blank"` (the oracle's): reading the terms must
 * not abandon the sign-up.
 */
function ConsentCheckbox(props: {
  readonly testId: string;
  readonly checked: boolean;
  readonly onChange: (checked: boolean) => void;
  readonly href: string;
  readonly documentName: string;
}) {
  const { testId, checked, onChange, href, documentName } = props;
  const inputId: string = useId();

  return (
    <div className="flex items-start space-x-2">
      <input
        id={inputId}
        type="checkbox"
        data-testid={testId}
        checked={checked}
        onChange={(event) => {
          onChange(event.target.checked);
        }}
        className="mt-0.5 size-4 rounded border-border"
      />
      <label htmlFor={inputId} className="text-sm font-normal leading-snug text-foreground">
        I agree to the{" "}
        <a
          href={href}
          target="_blank"
          rel="noopener noreferrer"
          className="text-primary underline-offset-4 hover:underline"
        >
          {documentName}
        </a>
      </label>
    </div>
  );
}

/**
 * The oracle's two-box consent group. The document links are DIVERGENCE 2: the
 * oracle points at `/terms-of-service` and `/privacy-policy` (L48, L56), and
 * neither route exists — the pages are `@page "/terms"` (Terms.razor:1) and
 * `@page "/privacy"` (Privacy.razor:1), so both links 404 in Blazor today. On a
 * screen whose entire purpose is informed consent to those two documents, 404s
 * are not parity worth keeping: the port links to the real routes, which
 * Wallow-vec7.3.16 has already registered.
 */
function ConsentBoxes(props: {
  readonly termsAccepted: boolean;
  readonly privacyAccepted: boolean;
  readonly onTermsChange: (checked: boolean) => void;
  readonly onPrivacyChange: (checked: boolean) => void;
}) {
  const { termsAccepted, privacyAccepted, onTermsChange, onPrivacyChange } = props;

  return (
    <div className="space-y-3">
      <ConsentCheckbox
        testId="accept-terms-checkbox"
        checked={termsAccepted}
        onChange={onTermsChange}
        href="/terms"
        documentName="Terms of Service"
      />
      <ConsentCheckbox
        testId="accept-terms-privacy-checkbox"
        checked={privacyAccepted}
        onChange={onPrivacyChange}
        href="/privacy"
        documentName="Privacy Policy"
      />
    </div>
  );
}

export interface AcceptTermsScreenProps {
  /** The `returnUrl` query parameter — `undefined` when the link omits it. */
  readonly returnUrl?: string;
  /** The `email` query parameter — the address the external provider vouched for. */
  readonly email?: string;
  /** The `name` query parameter — the provider's display name for the user. */
  readonly name?: string;
  /** The `error` query parameter — a bounce-back reason code. */
  readonly error?: string;
}

export function AcceptTermsScreen({
  returnUrl,
  email,
  name,
  error,
}: AcceptTermsScreenProps): ReactNode {
  const [termsAccepted, setTermsAccepted] = useState(false);
  const [privacyAccepted, setPrivacyAccepted] = useState(false);

  // The oracle's `Disabled="@(!_termsAccepted || !_privacyAccepted)"`. Consent is
  // revocable right up to the click: this is derived from the boxes on every
  // render, never latched forward.
  const bothAccepted: boolean = termsAccepted && privacyAccepted;

  const handleAcceptTerms = (): void => {
    // The oracle re-checks inside its handler rather than trusting the disabled
    // attribute, and so does this: declining is simply not accepting, and the
    // click must be inert rather than merely unclickable. The screen never sends
    // `acceptedTerms=false` — the endpoint's `!acceptedTerms` branch exists, but
    // it is not ours to drive; there is no "no thanks" round trip.
    if (!bothAccepted) {
      return;
    }

    // The oracle's `Uri.EscapeDataString(ReturnUrl ?? "/")` — `encodeURIComponent`,
    // not form encoding. Only a NULLISH returnUrl falls back (bd memory
    // `returnurl-guard-refuse-dont-sanitize`); "/" fails the API's absolute-URI
    // check, so the endpoint substitutes authUrl: the fallback means "send me
    // home", and the API decides where home is.
    //
    // The encoding is load-bearing, not cosmetic. `returnUrl` is attacker-supplied
    // cargo in a URL built by concatenation: unencoded, it would smuggle a second
    // `acceptedTerms` in, and ASP.NET binds a duplicated `[FromQuery] bool` key as
    // "true,false", which fails to parse and lands on the !acceptedTerms branch.
    const encodedReturnUrl: string = encodeURIComponent(returnUrl ?? "/");

    // A FULL navigation — the oracle's `NavigateTo(completeUrl, forceLoad: true)`,
    // never `router.navigate`: `/v1/**` is served by the h3 reverse proxy, not by
    // the client-side route tree, which would 404 in-app. It must also be a real
    // top-level navigation for the browser to attach the SameSite=Lax
    // ExternalLoginState cookie the endpoint needs (bd memory
    // `full-navigation-seam-for-wallow-auth-screens-that`).
    globalThis.location.href = `${SAME_ORIGIN}${COMPLETE_REGISTRATION_PATH}?acceptedTerms=true&returnUrl=${encodedReturnUrl}`;
  };

  return (
    <div className="rounded-lg border border-border bg-card p-6 space-y-6">
      <div className="space-y-1 text-center">
        <h1
          className="text-lg font-semibold text-card-foreground"
          data-testid="accept-terms-heading"
        >
          Almost there!
        </h1>
        <p className="text-sm text-muted-foreground">
          Please accept our terms to create your account
        </p>
      </div>

      <div className="space-y-4">
        {error === undefined || error === "" ? null : <ErrorAlert code={error} />}

        {email === undefined || email === "" ? null : <SigningUpAs email={email} name={name} />}

        <ConsentBoxes
          termsAccepted={termsAccepted}
          privacyAccepted={privacyAccepted}
          onTermsChange={setTermsAccepted}
          onPrivacyChange={setPrivacyAccepted}
        />

        <button
          type="button"
          data-testid="accept-terms-submit"
          disabled={!bothAccepted}
          onClick={handleAcceptTerms}
          className="w-full rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground disabled:opacity-50"
        >
          Create Account
        </button>
      </div>

      {/*
        The oracle's card footer, and the only "decline" affordance the screen
        has. Walking away creates no account and leaves the ExternalLoginState
        cookie to expire on its own (10 min); there is nothing to clean up
        client-side.
      */}
      <p className="text-center text-sm text-muted-foreground">
        Changed your mind?{" "}
        <a href="/login" className="text-primary underline-offset-4 hover:underline">
          Back to sign in
        </a>
      </p>
    </div>
  );
}
