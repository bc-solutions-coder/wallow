import type { QueryClient } from "@bc-solutions-coder/query";
import type { AuthorizeContextResponse, WallowSdk } from "@bc-solutions-coder/sdk";
import { authorizeContextGetOptions } from "@bc-solutions-coder/sdk/query";
import { stripBasePath } from "@bc-solutions-coder/env/base-path";
import {
  forkBranding,
  mergeClientBranding,
  type ResolvedBranding,
} from "@bc-solutions-coder/styles";

import { BASE_PATH } from "./base-path";
import { decideReturnUrl } from "./return-url";

/**
 * The auth host's client context, resolved ONCE at the root of the route tree.
 *
 * Screens inside a pending authorize transaction — the user arrived from
 * `/connect/authorize` and will be sent back to it — wear the requesting
 * client's branding. The transaction is identified by its `returnUrl`, which
 * every in-transaction screen already threads through, so the root loader asks
 * the transaction-scoped endpoint (`/v1/identity/auth/authorize-context`) to
 * resolve it and every screen reads the ONE answer. There is no anonymous
 * lookup by bare `client_id` any more: the endpoint validates the returnUrl
 * against the pending request, so an attacker cannot enumerate branding or
 * scope descriptions by crafting a link.
 *
 * Screens reached from an email link or directly (error, reset-password,
 * verify-email/confirm, invitation, setup, privacy, terms) are not in a
 * transaction and render the fork's own branding; the gate below is what keeps
 * the root loader from even asking on their behalf.
 */

/** The screens that render inside a pending authorize transaction. */
const TRANSACTION_PATHS: ReadonlySet<string> = new Set([
  "/login",
  "/register",
  "/consent",
  "/accept-terms",
  "/mfa/challenge",
  "/mfa/enroll",
  "/verify-email",
  "/forgot-password",
]);

/**
 * The prefix a transaction returnUrl must carry: the OIDC authorize endpoint,
 * which is what the server validates it against. Checking it here keeps the
 * loader from sending arbitrary (but locally-safe) returnUrls to an endpoint
 * that will refuse them anyway.
 */
const AUTHORIZE_PATH = "/connect/authorize";

/** The two search parameters the context lookup is keyed by. */
export interface AuthorizeTransactionSearch {
  readonly returnUrl?: string;
  readonly scope?: string;
}

/** The identified transaction: a safe authorize returnUrl plus its scopes. */
export interface AuthorizeTransaction {
  readonly returnUrl: string;
  readonly scope?: string;
}

/**
 * Decide whether a location is inside an authorize transaction.
 *
 * Three gates, all pure: the pathname must be one of the transaction screens
 * (base-path-stripped, trailing slash ignored — `/verify-email/` is the index
 * route's canonical spelling); the returnUrl must be present and pass the
 * shared open-redirect guard (`refuse-empty` — the mount-guard reading); and it
 * must point at the authorize endpoint. Anything else is `null`: not an error,
 * just a screen that renders the fork's own branding.
 */
export function resolveAuthorizeTransaction(
  pathname: string,
  search: AuthorizeTransactionSearch,
): AuthorizeTransaction | null {
  const stripped: string = stripBasePath(pathname, BASE_PATH);
  const normalized: string =
    stripped.length > 1 && stripped.endsWith("/") ? stripped.replace(/\/$/u, "") : stripped;

  if (!TRANSACTION_PATHS.has(normalized)) {
    return null;
  }

  const decision = decideReturnUrl(search.returnUrl, "refuse-empty");
  if (decision.verdict !== "accept") {
    return null;
  }

  const { returnUrl } = decision;
  if (returnUrl !== AUTHORIZE_PATH && !returnUrl.startsWith(`${AUTHORIZE_PATH}?`)) {
    return null;
  }

  return search.scope === undefined ? { returnUrl } : { returnUrl, scope: search.scope };
}

/**
 * Fetch the transaction's client context through the query cache.
 *
 * `ensureQueryData` keys on (returnUrl, scope), so the whole transaction — the
 * same returnUrl threaded from login to consent — resolves the context ONCE and
 * every later screen is a cache hit. Failures collapse to `null` rather than
 * throwing: branding is chrome, and an unknown client, an expired transaction
 * or an unreachable API must degrade to the fork's own branding, never block a
 * sign-in screen.
 */
export async function fetchAuthorizeContext(options: {
  readonly queryClient: QueryClient;
  readonly client: WallowSdk["client"];
  readonly pathname: string;
  readonly search: AuthorizeTransactionSearch;
}): Promise<AuthorizeContextResponse | null> {
  const transaction: AuthorizeTransaction | null = resolveAuthorizeTransaction(
    options.pathname,
    options.search,
  );

  if (transaction === null) {
    return null;
  }

  try {
    return await options.queryClient.ensureQueryData(
      authorizeContextGetOptions({
        client: options.client,
        query: { returnUrl: transaction.returnUrl, scope: transaction.scope },
      }),
    );
  } catch {
    return null;
  }
}

/** What a branded screen renders: the client's chrome and its attribution. */
export interface TransactionBranding {
  readonly branding: ResolvedBranding;
  readonly organizationName: string | null;
}

/**
 * Turn a resolved context into the branding a screen wears — or `null` for the
 * fork's own chrome.
 *
 * First-party clients deliberately collapse to `null`: the fork's own apps ARE
 * the fork, so they keep its branding and carry no "by <organization>" line —
 * attribution exists to tell the user a third party is asking.
 */
export function resolveTransactionBranding(
  context: AuthorizeContextResponse | null | undefined,
): TransactionBranding | null {
  if (context === null || context === undefined || context.firstParty) {
    return null;
  }

  return {
    branding: mergeClientBranding(forkBranding, context, BASE_PATH),
    organizationName: context.organizationName,
  };
}
