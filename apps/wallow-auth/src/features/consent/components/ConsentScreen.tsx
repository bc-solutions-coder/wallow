import { buildConsentSubmitUrl, consentInfoArgs, isSafeReturnUrl } from "@bc-solutions-coder/sdk";
import { Button, Card, ErrorBanner, MutedText, Text } from "@bc-solutions-coder/ui";
import { useQuery } from "@bc-solutions-coder/query";
import { useNavigate, useRouteContext } from "@tanstack/react-router";
import { useEffect, useMemo, type ReactNode } from "react";
import { appsGetConsentInfoOptions } from "../api";
import { BASE_PATH } from "@shared/lib/base-path";

/**
 * The Consent screen (Wallow-vec7.3.4).
 *
 * `clientId`, `returnUrl` and `scope` arrive as props rather than being read
 * from the router inside the component: the route owns the query string (the
 * oracle's two `[SupplyParameterFromQuery]` properties, `ReturnUrl` and
 * `client_id`, plus the `scope` the authorize endpoint sends) and hands them
 * down, which keeps this component a pure function of its inputs and testable
 * without a router. This is the seam `ResetPasswordForm` established and
 * `VerifyEmailConfirm` followed.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `consent-error`, `consent-heading`, `consent-scopes`, `consent-approve`,
 * `consent-deny`.
 *
 * Mutations call the GENERATED operations and reads use the generated
 * `{op}Options()` factories, both bound to the request-scoped SDK off the router
 * context (`useRouteContext({ from: "__root__" })`). The OIDC URL builders are
 * pure and imported directly. There is no app-level facade (Wallow-pu6a.5.5).
 *
 * ── THE ORIGIN DIVERGENCE (the load-bearing port decision on this screen) ─────
 *
 * The oracle ends `AppendToReturnUrl` (Consent.razor:70-80) by prepending an
 * absolute API origin — `Configuration["ApiBaseUrl"] ?? "http://localhost:5001"`
 * — and its comment gives the reason: the Auth app origin "does not host
 * /connect/authorize".
 *
 * That premise is FALSE in this app, so the prepend is deliberately NOT ported.
 * apps/wallow-auth's API surface (`src/lib/api-passthrough.ts`) is a passthrough
 * reverse proxy mounting `/connect/**` and `/v1/**` at the ROOT — the same fact
 * behind the facade's `baseUrl: '/'` (bd memory
 * `wallow-auth-same-origin-baseurl-apps-wallow-auth`). This origin DOES host
 * `/connect/authorize`, so the submit URL is same-origin and the origin argument
 * is `""`.
 *
 * This is a security decision, not a style one. Prepending an API origin would
 * (a) send the browser cross-origin for a request the proxy exists to keep
 * same-origin, dropping the `SameSite` auth cookie `/connect/authorize` needs,
 * and (b) reintroduce an `ApiBaseUrl` knob this app deliberately lacks — its only
 * API URL, `WALLOW_API_INTERNAL_URL`, is a SERVER-side internal address
 * (`http://wallow-api` under Aspire) that the browser cannot resolve at all.
 *
 * `buildConsentSubmitUrl` (Wallow-vec7.2.2) owns the rest of `AppendToReturnUrl`
 * — the `ReturnUrl ?? "/"` fallback, the `Contains('?')` separator, and the
 * `consent_granted` / `consent_denied` parameter — under 67 tests of its own.
 *
 * ── THE OPEN-REDIRECT GUARD (this bead's criterion; NOT in the oracle) ────────
 *
 * The oracle applies NO guard here: it appends and navigates. The guard is the
 * gap this bead closes, and `buildConsentSubmitUrl` enforces it by THROWING on a
 * present-but-unsafe returnUrl rather than sanitizing (bd memory
 * `returnurl-guard-refuse-dont-sanitize`).
 *
 * The screen refuses EARLY — on mount, before rendering a prompt and before
 * fetching — following `Login.razor` L533-540, the one oracle call site that
 * checks `IsSafe` before building a navigation URL and bails to
 * `/error?reason=invalid_redirect_uri`. Refusing at click time would mean
 * rendering an Approve button whose destination we have already decided we will
 * refuse to build: asking the user to authorize a request we know is malformed,
 * and saying so only after they consent. Bailing before the fetch also keeps the
 * client's display name and scope list from being disclosed to an
 * attacker-crafted link.
 *
 * ── THE MISSING LOADING STATE (an oracle wart, deliberately not ported) ───────
 *
 * The oracle renders its error block on `_consentInfo is null`, which is ALSO
 * true while its own request is in flight — which would flash "Unable
 * to load consent information" at every user before the fetch resolves. This
 * port renders NOTHING in flight: no error, no prompt. That fixes the flash
 * without inventing a testid the oracle has no element for, and keeps
 * `consent-error` meaning "this failed" rather than "this failed or has not
 * happened yet".
 *
 * ── THE SCOPE LIST IS AN INPUT, NOT ONLY AN OUTPUT (Wallow-dzt4) ─────────────
 *
 * The oracle passes `Array.Empty<string>()` to its consent-info call, and this
 * port copied it on the reasoning that the scopes being consented to come back
 * FROM the call. They do not: the endpoint answers "describe THESE scopes for
 * this client", so asking with none returns none, and the oracle's own consent
 * screen rendered a blank list too. That defect is what this bead repairs — the
 * authorize endpoint now carries the requested scopes to `/consent` as a
 * space-delimited `scope` parameter, and the raw string is split here.
 *
 * ── ERROR STATE: `null` BECOMES A REJECTION AT THIS SEAM ─────────────────────
 *
 * The oracle's `_consentInfo` is null in two cases, both rendering the error:
 * (a) no `client_id`, so `OnInitializedAsync` skips the call; (b) the request
 * failed — `AuthApiClient.GetConsentInfoAsync` (AuthApiClient.cs:397-416) returns
 * null on ANY non-2xx. Case (b) arrives here as a REJECTION, because the facade's
 * `unwrap()` throws instead of returning null. No status narrowing is needed: this
 * oracle has exactly ONE message for every failure, so the `WallowError` code-loss
 * gotcha (bd memory `wallow-auth-auth-client-ts-wallowerror-code-loss`) costs this
 * screen nothing.
 */

/** The oracle's single error message, covering every failure it can have. */
const LOAD_FAILURE_MESSAGE = "Unable to load consent information. Please try again.";

/**
 * The bail target for an unsafe returnUrl. `href` (a raw location) rather than
 * `to` + `search`: bd memory
 * `tanstack-router-redirect-to-an-unregistered-route-use-href-not-to`, and here
 * also because `/error`'s `validateSearch` is owned by the in-flight
 * Wallow-vec7.3.3 — `href` keeps this screen from coupling to that shape.
 */
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/**
 * The same-origin base the consent submit URL is built against: this app. See the
 * origin divergence note above — named rather than inlined so the empty default
 * reads as a decision rather than a forgotten argument. It carries the base path
 * because under a based build the passthrough answers under that prefix, not at
 * the site root.
 */
const SAME_ORIGIN_BASE: string = BASE_PATH;

/**
 * One requested scope, typed structurally against the generated `ScopeInfo`
 * rather than importing it: screens may not import from the SDK, and this shape
 * is all the list below needs.
 */
interface RequestedScope {
  readonly name: string;
  readonly description: string | null;
}

/** The consent info the prompt renders, narrowed to what it uses. */
interface ConsentPrompt {
  readonly clientId: string;
  readonly displayName: string | null;
  readonly requestedScopes: readonly RequestedScope[];
}

/** The oracle's `data-testid="consent-error"` block. */
function ErrorState() {
  return <ErrorBanner data-testid="consent-error">{LOAD_FAILURE_MESSAGE}</ErrorBanner>;
}

/**
 * The oracle's `<h2>@_consentInfo.DisplayName is requesting access</h2>`, with
 * one deviation: `displayName` is `null | string` on the generated
 * `ConsentInfoResponse`, and the oracle interpolates it unguarded — so a null
 * renders " is requesting access", a consent prompt that does not say WHO is
 * asking. The client id is the fallback: consent to an unnamed party is not
 * consent.
 */
function ConsentHeading({ info }: { readonly info: ConsentPrompt }) {
  const name: string = info.displayName ?? info.clientId;

  return (
    <Text as="h2" variant="subheading" color="onCard" data-testid="consent-heading">
      {name} is requesting access
    </Text>
  );
}

/**
 * The oracle's `@foreach (ConsentScopeInfo scope in _consentInfo.RequestedScopes)`.
 * The list is the server's and only the server's — it is the entire substance of
 * the decision the user is being asked to make.
 *
 * The oracle renders `@scope.Name` alone; this also renders the scope's
 * `description` when the server sends one. `openid` and `profile` are protocol
 * identifiers, not English, and the DTO carries a description precisely so the
 * user can be told what they are granting.
 */
function ScopeList({ scopes }: { readonly scopes: readonly RequestedScope[] }) {
  return (
    <ul className="space-y-2" data-testid="consent-scopes">
      {scopes.map((scope: RequestedScope) => (
        <li key={scope.name} className="space-y-0.5">
          <Text as="p" variant="bodySm" weight="medium">
            {scope.name}
          </Text>
          {scope.description === null ? null : <MutedText>{scope.description}</MutedText>}
        </li>
      ))}
    </ul>
  );
}

/**
 * The oracle's two `BbButton`s. Deny is not optional: a consent screen with only
 * an approve path is not a consent screen, and the denial has to be DELIVERED to
 * the authorize endpoint rather than leaving the relying party's request hanging.
 */
function ConsentActions(props: { readonly onApprove: () => void; readonly onDeny: () => void }) {
  const { onApprove, onDeny } = props;

  return (
    <div className="space-y-2">
      <Button type="button" data-testid="consent-approve" onClick={onApprove}>
        Approve
      </Button>
      {/*
        `outline`, the variant F3.T1 added for exactly this: a deny that paints
        the same solid surface as approve gives the two answers equal weight.
      */}
      <Button type="button" variant="outline" data-testid="consent-deny" onClick={onDeny}>
        Deny
      </Button>
    </div>
  );
}

/** The oracle's `else` branch: who is asking, for what, and the two answers. */
function ConsentPromptState(props: {
  readonly info: ConsentPrompt;
  readonly onSubmit: (granted: boolean) => void;
}) {
  const { info, onSubmit } = props;

  return (
    <div className="space-y-4">
      <ConsentHeading info={info} />
      <ScopeList scopes={info.requestedScopes} />
      <ConsentActions
        onApprove={() => {
          onSubmit(true);
        }}
        onDeny={() => {
          onSubmit(false);
        }}
      />
    </div>
  );
}

/**
 * The screen's states, in the order their guards must be applied.
 *
 * The unsafe-returnUrl check comes first and renders NOTHING: the effect in
 * `ConsentScreen` is already routing the user to `/error`, and flashing "Unable
 * to load consent information" on the way out would misreport an open-redirect
 * attempt as a transient server problem. It also must not be absorbed by the
 * missing-client branch — a hostile returnUrl on a link that ALSO omits
 * `client_id` is still a hostile returnUrl.
 *
 * `isPending` is checked after the two refusals because it is also true for a
 * disabled query: neither refusal has a request to wait on.
 */
function ConsentState(props: {
  readonly clientIsKnown: boolean;
  readonly returnUrlIsUnsafe: boolean;
  readonly info: ConsentPrompt | null;
  readonly isPending: boolean;
  readonly isError: boolean;
  readonly onSubmit: (granted: boolean) => void;
}) {
  const { clientIsKnown, returnUrlIsUnsafe, info, isPending, isError, onSubmit } = props;

  if (returnUrlIsUnsafe) {
    return null;
  }

  if (!clientIsKnown) {
    return <ErrorState />;
  }

  // In flight: no error, no prompt. See the missing-loading-state note above.
  if (isPending) {
    return null;
  }

  // `info === null` is the unreachable queryFn narrow; treated as a failure
  // rather than crashed on, since there is no prompt to render without it.
  if (isError || info === null) {
    return <ErrorState />;
  }

  return <ConsentPromptState info={info} onSubmit={onSubmit} />;
}

export interface ConsentScreenProps {
  /** The `client_id` query parameter — `undefined` when the link omits it. */
  readonly clientId?: string;
  /** The `returnUrl` query parameter — `undefined` when the link omits it. */
  readonly returnUrl?: string;
  /**
   * The `scope` query parameter — ONE space-delimited string of the scopes the
   * relying party asked for, as the authorize endpoint sends them. `undefined`
   * when the link omits it.
   */
  readonly scope?: string;
}

export function ConsentScreen({ clientId, returnUrl, scope }: ConsentScreenProps): ReactNode {
  const { sdk } = useRouteContext({ from: "__root__" });
  const navigate = useNavigate();

  // Split on whitespace, dropping empty segments: repeated or trailing spaces in
  // the parameter are not empty scope names. A link with no `scope` asks with
  // none — the screen never fabricates a list the relying party did not request.
  const requestedScopes: readonly string[] = useMemo(
    () => (scope === undefined ? [] : scope.split(/\s+/u).filter((s: string) => s !== "")),
    [scope],
  );

  // The guard, evaluated before anything else happens. A NULLISH returnUrl is not
  // hostile — the builder's `ReturnUrl ?? "/"` covers it — so only a PRESENT value
  // is checked. An empty string IS present: `IsNullOrWhiteSpace` fails it, so it
  // is the unsafe case and not the nullish-fallback one.
  const returnUrlIsUnsafe: boolean = returnUrl !== undefined && !isSafeReturnUrl(returnUrl);

  // The oracle's `if (ClientId is not null)`. An empty string is a malformed
  // link, not a client to look up: a screen that "helpfully" sent `client_id=`
  // would 404 and blame the server for the link's own defect.
  const clientIsKnown: boolean = clientId !== undefined && clientId !== "";

  useEffect(() => {
    if (returnUrlIsUnsafe) {
      void navigate({ href: ERROR_HREF });
    }
  }, [returnUrlIsUnsafe, navigate]);

  // The requested scopes are an INPUT to this lookup — see the note above. The
  // `?? ""` is unreachable — `enabled` gates the read on `clientIsKnown` — and is
  // present only to narrow the prop to the `string` the factory takes, without a
  // cast.
  const query = useQuery({
    ...appsGetConsentInfoOptions({
      client: sdk.client,
      ...consentInfoArgs(clientId ?? "", requestedScopes),
    }),
    // Both refusals carried to React Query, so neither path reaches the network.
    enabled: clientIsKnown && !returnUrlIsUnsafe,
  });

  const submitConsent = (granted: boolean): void => {
    // A FULL navigation, not `navigate()`: `/connect/authorize` is served by the
    // passthrough reverse proxy, not by the client-side route tree, which would 404 in-app.
    globalThis.location.href = buildConsentSubmitUrl(SAME_ORIGIN_BASE, returnUrl, granted);
  };

  return (
    <Card>
      <ConsentState
        clientIsKnown={clientIsKnown}
        returnUrlIsUnsafe={returnUrlIsUnsafe}
        info={query.data ?? null}
        isPending={query.isPending}
        isError={query.isError}
        onSubmit={submitConsent}
      />
    </Card>
  );
}
