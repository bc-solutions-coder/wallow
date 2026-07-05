/**
 * Server-side BFF configuration for the OIDC tunnel.
 *
 * These values are read once at startup (typically from environment variables)
 * and passed to the OIDC and session helpers. Nothing here is safe to expose to
 * the browser — the client secret and cookie password are confidential.
 */
export interface BffConfig {
  /** OIDC issuer base URL, e.g. `https://auth.example.com`. */
  issuer: string;
  /** Confidential client identifier registered with the issuer. */
  clientId: string;
  /** Confidential client secret registered with the issuer. */
  clientSecret: string;
  /** Absolute callback URL the issuer redirects to after authorization. */
  redirectUri: string;
  /** Absolute URL the issuer redirects to after end-session (logout). */
  postLogoutRedirectUri: string;
  /** Requested scopes. Defaults to `openid profile email offline_access`. */
  scopes: string[];
  /** Base URL of the downstream API the BFF proxies to. */
  apiBaseUrl: string;
  /** Name of the sealed session cookie. Defaults to `wallow_bff`. */
  cookieName: string;
  /** Password used to seal/unseal the session and transaction cookies. */
  cookiePassword: string;
  /**
   * Optional server-side discovery/metadata URL, used when the OP is reachable
   * from the browser and the server under different hostnames (split-horizon
   * DNS, reverse proxies, container networks). When set, the server fetches the
   * OpenID configuration from this URL and uses its `token_endpoint` for the
   * backchannel, while the browser-facing `authorization_endpoint` and
   * `end_session_endpoint` are pinned to the public {@link issuer} origin so the
   * user agent can follow those redirects. Defaults to
   * `${issuer}/.well-known/openid-configuration`.
   */
  metadataUrl?: string;
}

/**
 * Build a {@link BffConfig} from environment variables.
 *
 * Required keys (throws when missing): `OIDC_ISSUER`, `OIDC_CLIENT_ID`,
 * `OIDC_CLIENT_SECRET`, `OIDC_REDIRECT_URI`, `OIDC_POST_LOGOUT_REDIRECT_URI`,
 * `BFF_API_BASE_URL`, `COOKIE_PASSWORD`. `OIDC_SCOPES` (space-separated),
 * `COOKIE_NAME`, and `OIDC_METADATA_URL` are optional with defaults.
 *
 * @param env Environment source. Defaults to `process.env`.
 */
export function loadBffConfigFromEnv(
  env: NodeJS.ProcessEnv = process.env,
): BffConfig {
  const require = (key: string): string => {
    const value: string | undefined = env[key];
    if (value === undefined || value === "") {
      throw new Error(`Missing required environment variable: ${key}`);
    }
    return value;
  };

  const scopesRaw: string | undefined = env.OIDC_SCOPES;
  const scopes: string[] =
    scopesRaw !== undefined && scopesRaw.trim() !== ""
      ? scopesRaw.trim().split(/\s+/)
      : ["openid", "profile", "email", "offline_access"];

  return {
    issuer: require("OIDC_ISSUER"),
    clientId: require("OIDC_CLIENT_ID"),
    clientSecret: require("OIDC_CLIENT_SECRET"),
    redirectUri: require("OIDC_REDIRECT_URI"),
    postLogoutRedirectUri: require("OIDC_POST_LOGOUT_REDIRECT_URI"),
    scopes,
    apiBaseUrl: require("BFF_API_BASE_URL"),
    cookieName: env.COOKIE_NAME ?? "wallow_bff",
    cookiePassword: require("COOKIE_PASSWORD"),
    metadataUrl:
      env.OIDC_METADATA_URL !== undefined && env.OIDC_METADATA_URL !== ""
        ? env.OIDC_METADATA_URL
        : undefined,
  };
}
