/**
 * Native-fetch OIDC helpers for the BFF: discovery, authorization URL building,
 * authorization-code exchange, and refresh-token rotation.
 *
 * No external OIDC client library is used — only the global `fetch`.
 */
import type { BffConfig } from "./config";

/** Subset of the OpenID Connect discovery document the BFF depends on. */
export interface DiscoveryDoc {
  authorization_endpoint: string;
  token_endpoint: string;
  end_session_endpoint?: string;
  userinfo_endpoint?: string;
}

/** Token endpoint response for authorization-code and refresh grants. */
export interface TokenResponse {
  access_token: string;
  refresh_token?: string;
  id_token?: string;
  expires_in: number;
  token_type?: string;
}

/** Parameters required to build an authorization request URL. */
export interface AuthorizeParams {
  state: string;
  codeChallenge: string;
  nonce: string;
}

/** Parameters required to exchange an authorization code for tokens. */
export interface ExchangeCodeParams {
  code: string;
  codeVerifier: string;
}

/** Module-level cache of discovery documents keyed on issuer URL. */
const discoveryCache: Map<string, DiscoveryDoc> = new Map();

/**
 * Rewrite the origin (protocol + host) of an absolute URL, preserving its path
 * and query. Used to pin the browser-facing endpoints to the public issuer
 * origin when discovery is fetched from a server-reachable internal host.
 */
function rewriteOrigin(endpoint: string, targetOrigin: string): string {
  const url: URL = new URL(endpoint);
  const target: URL = new URL(targetOrigin);
  url.protocol = target.protocol;
  url.host = target.host;
  return url.toString();
}

/**
 * Fetch and cache the issuer's discovery document.
 *
 * When {@link BffConfig.metadataUrl} is set, discovery is fetched from that
 * server-reachable URL. The backchannel `token_endpoint` is used exactly as the
 * metadata advertises it (reachable from the server), while the browser-facing
 * `authorization_endpoint` and `end_session_endpoint` are pinned to the public
 * {@link BffConfig.issuer} origin so the user agent can follow the redirects.
 *
 * This handles OpenID providers (such as OpenIddict) that derive their endpoint
 * URIs from the incoming request host rather than the configured issuer: when
 * discovery is fetched from the internal host, every advertised endpoint points
 * at that internal host, so the interactive endpoints must be re-pinned to the
 * public origin.
 *
 * @param config BFF configuration providing the issuer and optional metadata URL.
 */
export async function discover(config: BffConfig): Promise<DiscoveryDoc> {
  const metadataUrl: string =
    config.metadataUrl ?? `${config.issuer}/.well-known/openid-configuration`;

  const cached: DiscoveryDoc | undefined = discoveryCache.get(metadataUrl);
  if (cached !== undefined) {
    return cached;
  }

  const response: Response = await fetch(metadataUrl);
  if (!response.ok) {
    throw new Error(
      `OIDC discovery failed with status ${response.status} for ${metadataUrl}`,
    );
  }

  const raw: DiscoveryDoc = (await response.json()) as DiscoveryDoc;
  let doc: DiscoveryDoc = raw;
  if (config.metadataUrl !== undefined) {
    const issuerOrigin: string = new URL(config.issuer).origin;
    doc = {
      ...raw,
      authorization_endpoint: rewriteOrigin(
        raw.authorization_endpoint,
        issuerOrigin,
      ),
      end_session_endpoint:
        raw.end_session_endpoint !== undefined
          ? rewriteOrigin(raw.end_session_endpoint, issuerOrigin)
          : raw.end_session_endpoint,
    };
  }

  discoveryCache.set(metadataUrl, doc);
  return doc;
}

/**
 * Build the authorization request URL including PKCE, state, nonce, and scopes.
 */
export function buildAuthorizeUrl(
  config: BffConfig,
  doc: DiscoveryDoc,
  params: AuthorizeParams,
): string {
  const url: URL = new URL(doc.authorization_endpoint);
  url.searchParams.set("response_type", "code");
  url.searchParams.set("client_id", config.clientId);
  url.searchParams.set("redirect_uri", config.redirectUri);
  url.searchParams.set("scope", config.scopes.join(" "));
  url.searchParams.set("state", params.state);
  url.searchParams.set("code_challenge", params.codeChallenge);
  url.searchParams.set("code_challenge_method", "S256");
  url.searchParams.set("nonce", params.nonce);
  return url.toString();
}

/**
 * POST a form-encoded body to the token endpoint and return the parsed tokens.
 *
 * Always includes the confidential client credentials. Throws on any non-2xx
 * response.
 */
async function postToken(
  config: BffConfig,
  doc: DiscoveryDoc,
  body: URLSearchParams,
): Promise<TokenResponse> {
  body.set("client_id", config.clientId);
  body.set("client_secret", config.clientSecret);

  const response: Response = await fetch(doc.token_endpoint, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: body.toString(),
  });

  if (!response.ok) {
    const detail: string = await response.text().catch(() => "");
    throw new Error(
      `Token request failed with status ${response.status}: ${detail}`,
    );
  }

  return (await response.json()) as TokenResponse;
}

/**
 * Exchange an authorization code (plus PKCE verifier) for tokens.
 */
export async function exchangeCode(
  config: BffConfig,
  doc: DiscoveryDoc,
  params: ExchangeCodeParams,
): Promise<TokenResponse> {
  const body: URLSearchParams = new URLSearchParams({
    grant_type: "authorization_code",
    code: params.code,
    code_verifier: params.codeVerifier,
    redirect_uri: config.redirectUri,
  });
  return postToken(config, doc, body);
}

/**
 * Exchange a refresh token for a fresh set of tokens.
 */
export async function refreshTokens(
  config: BffConfig,
  doc: DiscoveryDoc,
  refreshToken: string,
): Promise<TokenResponse> {
  const body: URLSearchParams = new URLSearchParams({
    grant_type: "refresh_token",
    refresh_token: refreshToken,
  });
  return postToken(config, doc, body);
}

/**
 * Fetch the resolved identity claims from the issuer's userinfo endpoint.
 *
 * This is a backchannel call made with the confidential BFF's access token, so
 * the endpoint is used exactly as the metadata advertises it (server-reachable,
 * not rewritten to the browser-facing origin). Providers such as OpenIddict may
 * only emit standard identity claims (`email`, `name`, ...) to userinfo rather
 * than the id_token, so the BFF resolves the user identity from here.
 *
 * @param doc Discovery document providing the userinfo endpoint.
 * @param accessToken Bearer access token authorizing the userinfo request.
 * @returns The parsed claims object, or `null` when no userinfo endpoint is
 *          advertised or the request fails.
 */
export async function fetchUserInfo(
  doc: DiscoveryDoc,
  accessToken: string,
): Promise<Record<string, unknown> | null> {
  if (doc.userinfo_endpoint === undefined || doc.userinfo_endpoint === "") {
    return null;
  }

  const response: Response = await fetch(doc.userinfo_endpoint, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
      accept: "application/json",
    },
  });

  if (!response.ok) {
    return null;
  }

  return (await response.json()) as Record<string, unknown>;
}
