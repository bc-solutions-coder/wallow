import {
  buildConsentSubmission,
  CONSENT_DECISION_FIELD,
  CONSENT_DENIED,
  CONSENT_GRANTED,
  type ConsentSubmission,
} from "@bc-solutions-coder/sdk";
import { Button, Card, ErrorBanner, MutedText, Text } from "@bc-solutions-coder/ui";
import { useQuery } from "@bc-solutions-coder/query";
import { useRouteContext } from "@tanstack/react-router";
import { useMemo, type ReactNode } from "react";
import { authorizeContextGetOptions } from "../api";
import { BASE_PATH } from "@shared/lib/base-path";
import { useReturnUrlGuard } from "@shared/hooks/use-return-url-guard";

/**
 * The Consent screen (Wallow-vec7.3.4).
 *
 * `returnUrl`, `scope` and `consentToken` arrive as props rather than being
 * read from the router inside the component: the route owns the query string
 * and hands them down, which keeps this component a pure function of its
 * inputs and testable without a router. This is the seam `ResetPasswordForm`
 * established and `VerifyEmailConfirm` followed.
 *
 * ── THE DECISION IS A POST ───────────────────────────────────────────────────
 *
 * The two answers are submit buttons on a real `<form method="post">` aimed at
 * the authorize endpoint, not links: the endpoint honours a decision only when
 * it arrives in a request body together with the token it issued, so a
 * decision smuggled onto a GET link grants nothing. The
 * authorize request's own parameters ride along as hidden fields, because a
 * POST body is where OpenIddict reads them from.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `consent-error`, `consent-heading`, `consent-scopes`, `consent-approve`,
 * `consent-deny`.
 *
 * ── THE READ IS THE TRANSACTION CONTEXT (issue #142) ─────────────────────────
 *
 * Who is asking, and for which scopes, comes from the transaction-scoped
 * authorize-context endpoint — keyed by the pending request's `returnUrl`,
 * never by a bare `client_id`. The old anonymous consent-info lookup let any
 * crafted link enumerate a client's display name and scope descriptions; this
 * endpoint validates the returnUrl against a real pending authorize request
 * and 404s anything else. It is the SAME query the root loader resolves for
 * the branded chrome, sharing one cache entry, so this read is normally a hit.
 *
 * ── THE ORIGIN DIVERGENCE (the load-bearing port decision on this screen) ─────
 *
 * The oracle ends `AppendToReturnUrl` (Consent.razor:70-80) by prepending an
 * absolute API origin — `Configuration["ApiBaseUrl"] ?? "http://localhost:5001"`
 * — and its comment gives the reason: the Auth app origin "does not host
 * /connect/authorize".
 *
 * That premise is FALSE in this app, so the prepend is deliberately NOT ported.
 * apps/wallow-auth's API surface (`src/shared/lib/api-passthrough.server.ts`) is a passthrough
 * reverse proxy mounting `/connect/**` and `/v1/**` at the ROOT — the same fact
 * behind the facade's `baseUrl: '/'` (bd memory
 * `wallow-auth-same-origin-baseurl-apps-wallow-auth`). This origin DOES host
 * `/connect/authorize`, so the form's action is same-origin and the origin
 * argument is `""`.
 *
 * This is a security decision, not a style one. Prepending an API origin would
 * (a) send the browser cross-origin for a request the proxy exists to keep
 * same-origin, dropping the `SameSite` auth cookie `/connect/authorize` needs,
 * and (b) reintroduce an `ApiBaseUrl` knob this app deliberately lacks — its only
 * API URL, `WALLOW_API_INTERNAL_URL`, is a SERVER-side internal address
 * (`http://wallow-api` under Aspire) that the browser cannot resolve at all.
 *
 * `buildConsentSubmission` owns the rest — the `ReturnUrl ?? "/"` fallback and
 * the split of the returnUrl into an action and its fields — under tests of its
 * own.
 *
 * ── THE OPEN-REDIRECT GUARD (NOT in the oracle) ──────────────────────────────
 *
 * The oracle applies NO guard here: it appends and navigates. The guard is the
 * gap this port closes, and `buildConsentSubmission` enforces it by THROWING on
 * a present-but-unsafe returnUrl rather than sanitizing (bd memory
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
 * ── ERROR STATE: EVERY FAILED LOOKUP IS ONE MESSAGE ──────────────────────────
 *
 * The oracle has exactly ONE message for every failure, and the endpoint keeps
 * that true here: an expired transaction, an unknown client and a malformed
 * returnUrl all come back as a 404 the SDK surfaces as a REJECTION, and the
 * screen renders the one message for all of them.
 */

/** The oracle's single error message, covering every failure it can have. */
const LOAD_FAILURE_MESSAGE = "Unable to load consent information. Please try again.";

/**
 * The same-origin base the consent form's action is built against: this app. See the
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
  readonly displayName: string;
  readonly requestedScopes: readonly RequestedScope[];
}

/** The oracle's `data-testid="consent-error"` block. */
function ErrorState() {
  return <ErrorBanner data-testid="consent-error">{LOAD_FAILURE_MESSAGE}</ErrorBanner>;
}

/**
 * The oracle's `<h2>@_consentInfo.DisplayName is requesting access</h2>`. The
 * context endpoint's `displayName` is non-nullable — the server falls back to
 * the client id itself before answering — so consent is never asked for an
 * unnamed party.
 */
function ConsentHeading({ info }: { readonly info: ConsentPrompt }) {
  return (
    <Text as="h2" variant="subheading" color="onCard" data-testid="consent-heading">
      {info.displayName} is requesting access
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
 * The oracle's two `BbButton`s, as the submit buttons of the form that delivers
 * the answer. Deny is not optional: a consent screen with only an approve path
 * is not a consent screen, and the denial has to be DELIVERED to the authorize
 * endpoint rather than leaving the relying party's request hanging.
 *
 * Which answer was given is the submitter's own name and value — the browser
 * appends exactly one of the two to the body — so the form carries no decision
 * field of its own.
 */
function ConsentForm(props: { readonly submission: ConsentSubmission }) {
  const { submission } = props;

  return (
    <form method="post" action={submission.action} className="space-y-2">
      {submission.fields.map(([name, value]: readonly [string, string], index: number) => (
        // Indexed on purpose: a parameter may repeat with the same value, and
        // the list is never reordered.
        // oxlint-disable-next-line react/no-array-index-key
        <input key={`${index}:${name}`} type="hidden" name={name} value={value} />
      ))}
      <Button
        type="submit"
        name={CONSENT_DECISION_FIELD}
        value={CONSENT_GRANTED}
        data-testid="consent-approve"
      >
        Approve
      </Button>
      {/*
        `outline`, the variant F3.T1 added for exactly this: a deny that paints
        the same solid surface as approve gives the two answers equal weight.
      */}
      <Button
        type="submit"
        variant="outline"
        name={CONSENT_DECISION_FIELD}
        value={CONSENT_DENIED}
        data-testid="consent-deny"
      >
        Deny
      </Button>
    </form>
  );
}

/** The oracle's `else` branch: who is asking, for what, and the two answers. */
function ConsentPromptState(props: {
  readonly info: ConsentPrompt;
  readonly submission: ConsentSubmission;
}) {
  const { info, submission } = props;

  return (
    <div className="space-y-4">
      <ConsentHeading info={info} />
      <ScopeList scopes={info.requestedScopes} />
      <ConsentForm submission={submission} />
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
 * missing-transaction branch — a hostile returnUrl is still a hostile
 * returnUrl.
 *
 * `isPending` is checked after the two refusals because it is also true for a
 * disabled query: neither refusal has a request to wait on.
 */
function ConsentState(props: {
  readonly transactionIsKnown: boolean;
  readonly returnUrlIsUnsafe: boolean;
  readonly info: ConsentPrompt | null;
  readonly isPending: boolean;
  readonly isError: boolean;
  /** `null` exactly when the returnUrl is unsafe: there is no form to build for it. */
  readonly submission: ConsentSubmission | null;
}) {
  const { transactionIsKnown, returnUrlIsUnsafe, info, isPending, isError, submission } = props;

  if (returnUrlIsUnsafe || submission === null) {
    return null;
  }

  if (!transactionIsKnown) {
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

  return <ConsentPromptState info={info} submission={submission} />;
}

export interface ConsentScreenProps {
  /** The `returnUrl` query parameter — `undefined` when the link omits it. */
  readonly returnUrl?: string;
  /**
   * The `scope` query parameter — ONE space-delimited string of the scopes the
   * relying party asked for, as the authorize endpoint sends them. Passed to
   * the context lookup raw — OAuth's own delimiter is the wire format both
   * ends already speak. `undefined` when the link omits it.
   */
  readonly scope?: string;
  /**
   * The `consent_token` query parameter — the single-use token the authorize
   * endpoint minted for this request, posted back with the answer. `undefined`
   * when the link omits it; the endpoint then refuses the answer and asks again
   * with a fresh one.
   */
  readonly consentToken?: string;
}

export function ConsentScreen({ returnUrl, scope, consentToken }: ConsentScreenProps): ReactNode {
  const { sdk } = useRouteContext({ from: "__root__" });

  // Evaluated before anything else happens; the hook owns the bail navigation.
  // The nullish case is the builder's `ReturnUrl ?? "/"` and is not hostile.
  const returnUrlIsUnsafe: boolean = useReturnUrlGuard(returnUrl) === "refuse";

  // The old missing-`client_id` branch, transposed: with no returnUrl there is
  // no pending transaction to describe, so there is nothing to ask the server
  // and the screen renders its error. An empty string is a malformed link, not
  // a transaction to look up.
  const transactionIsKnown: boolean = returnUrl !== undefined && returnUrl !== "";

  // The `?? ""` is unreachable — `enabled` gates the read on
  // `transactionIsKnown` — and is present only to narrow the prop to a
  // `string` without a cast.
  const query = useQuery({
    ...authorizeContextGetOptions({
      client: sdk.client,
      query: { returnUrl: returnUrl ?? "", scope },
    }),
    // Both refusals carried to React Query, so neither path reaches the network.
    enabled: transactionIsKnown && !returnUrlIsUnsafe,
  });

  // The generated response carries branding fields and the org attribution too;
  // this screen renders only the consent substance, so it narrows to that.
  const info: ConsentPrompt | null = useMemo(
    () =>
      query.data === undefined
        ? null
        : { displayName: query.data.displayName, requestedScopes: query.data.scopes },
    [query.data],
  );

  // A FULL-PAGE form post, not `navigate()`: `/connect/authorize` is served by
  // the passthrough reverse proxy, not by the client-side route tree, which
  // would 404 in-app. Built only for a returnUrl the guard let through — the
  // builder throws on the ones it refuses, and the screen is already leaving.
  const submission: ConsentSubmission | null = useMemo(
    () =>
      returnUrlIsUnsafe ? null : buildConsentSubmission(SAME_ORIGIN_BASE, returnUrl, consentToken),
    [returnUrlIsUnsafe, returnUrl, consentToken],
  );

  return (
    <Card>
      <ConsentState
        transactionIsKnown={transactionIsKnown}
        returnUrlIsUnsafe={returnUrlIsUnsafe}
        info={info}
        isPending={query.isPending}
        isError={query.isError}
        submission={submission}
      />
    </Card>
  );
}
