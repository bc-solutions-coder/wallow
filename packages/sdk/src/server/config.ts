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
  /**
   * Lifetime of the sealed session cookie in seconds, written as its `Max-Age`.
   * Read from `SESSION_TTL_SECONDS`. Defaults to `86400` (24 hours).
   */
  sessionTtlSeconds: number;
  /**
   * Whether the session, transaction, and CSRF cookies carry the `Secure` flag.
   * Read from `COOKIE_SECURE`; set it to `false` for plain-HTTP local
   * development. Defaults to `true`.
   */
  cookieSecure: boolean;
}

/** Session cookie lifetime used when `SESSION_TTL_SECONDS` is not set: 24 hours. */
export const DEFAULT_SESSION_TTL_SECONDS: number = 86_400;

/** The smallest accepted `SESSION_TTL_SECONDS`; the value must be positive. */
const MIN_SESSION_TTL_SECONDS = 1;

/**
 * Build a {@link BffConfig} from environment variables.
 *
 * Required keys (throws when missing): `OIDC_ISSUER`, `OIDC_CLIENT_ID`,
 * `OIDC_CLIENT_SECRET`, `OIDC_REDIRECT_URI`, `OIDC_POST_LOGOUT_REDIRECT_URI`,
 * `BFF_API_BASE_URL`, `COOKIE_PASSWORD`. `OIDC_SCOPES` (space-separated),
 * `COOKIE_NAME`, `OIDC_METADATA_URL`, `SESSION_TTL_SECONDS`, and
 * `COOKIE_SECURE` are optional with defaults.
 *
 * A malformed `SESSION_TTL_SECONDS` throws rather than silently falling back to
 * the default, so a startup misconfiguration fails loudly. `COOKIE_SECURE`
 * instead fails secure: only the literal `false` clears the flag.
 *
 * @param env Environment source. Defaults to `process.env`.
 */
export function loadBffConfigFromEnv(env: NodeJS.ProcessEnv = process.env): BffConfig {
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
      ? scopesRaw.trim().split(/\s+/u)
      : ["openid", "profile", "email", "offline_access"];

  const ttlRaw: string = (env.SESSION_TTL_SECONDS ?? "").trim();
  let sessionTtlSeconds: number = DEFAULT_SESSION_TTL_SECONDS;
  if (ttlRaw !== "") {
    const parsed: number = Number(ttlRaw);
    if (!Number.isInteger(parsed) || parsed < MIN_SESSION_TTL_SECONDS) {
      throw new Error(
        `Invalid environment variable SESSION_TTL_SECONDS: expected a positive whole number of seconds, got "${ttlRaw}"`,
      );
    }
    sessionTtlSeconds = parsed;
  }

  const secureRaw: string = (env.COOKIE_SECURE ?? "").trim().toLowerCase();
  const cookieSecure: boolean = secureRaw !== "false";

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
    sessionTtlSeconds,
    cookieSecure,
  };
}
