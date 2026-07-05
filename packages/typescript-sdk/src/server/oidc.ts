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
 * Fetch and cache the issuer's discovery document.
 *
 * @param config BFF configuration providing the issuer URL.
 */
export async function discover(config: BffConfig): Promise<DiscoveryDoc> {
  const cached: DiscoveryDoc | undefined = discoveryCache.get(config.issuer);
  if (cached !== undefined) {
    return cached;
  }

  const response: Response = await fetch(
    `${config.issuer}/.well-known/openid-configuration`,
  );
  if (!response.ok) {
    throw new Error(
      `OIDC discovery failed with status ${response.status} for ${config.issuer}`,
    );
  }

  const doc: DiscoveryDoc = (await response.json()) as DiscoveryDoc;
  discoveryCache.set(config.issuer, doc);
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
