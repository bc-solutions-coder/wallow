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
  /**
   * Name of the sealed session cookie. Defaults to `__Host-wallow_bff` when
   * {@link cookieSecure} is set, and to plain `wallow_bff` otherwise — the
   * `__Host-` prefix is only honoured over HTTPS, and a browser silently
   * DROPS such a cookie on a plain-http origin.
   */
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

/** A clean environment contract: no collected problems to report. */
const NO_PROBLEMS = 0;

/** Unprefixed session-cookie name, used when the `__Host-` prefix is not viable. */
const DEFAULT_COOKIE_NAME: string = "wallow_bff";

/**
 * RFC 6265bis cookie-name prefix that binds a cookie to the exact host that set
 * it: a browser only accepts it when the cookie is `Secure`, `Path=/`, and
 * carries no `Domain`, so a sibling subdomain cannot overwrite the BFF session
 * cookie of the app's own origin.
 */
const HOST_COOKIE_PREFIX: string = "__Host-";

/**
 * The default session-cookie name for the given cookie settings.
 *
 * The prefix requires HTTPS, so it follows `cookieSecure`: on a plain-http dev
 * origin a `__Host-` cookie is dropped by the browser and login fails with no
 * error anywhere. `COOKIE_HOST_PREFIX=false` is the explicit opt-out for a
 * deployment that terminates TLS but cannot satisfy the prefix's other
 * requirements; it relaxes the NAME only, never `Secure`. Like `COOKIE_SECURE`,
 * it fails secure — only the literal `false` opts out.
 */
function defaultCookieName(hostPrefixRaw: string, cookieSecure: boolean): string {
  const hostPrefix: boolean = hostPrefixRaw !== "false" && cookieSecure;
  return hostPrefix ? `${HOST_COOKIE_PREFIX}${DEFAULT_COOKIE_NAME}` : DEFAULT_COOKIE_NAME;
}

/**
 * Build a {@link BffConfig} from environment variables.
 *
 * Required keys (throws when missing): `OIDC_ISSUER`, `OIDC_CLIENT_ID`,
 * `OIDC_CLIENT_SECRET`, `OIDC_REDIRECT_URI`, `OIDC_POST_LOGOUT_REDIRECT_URI`,
 * `BFF_API_BASE_URL`, `COOKIE_PASSWORD`. `OIDC_SCOPES` (space-separated),
 * `COOKIE_NAME`, `OIDC_METADATA_URL`, `SESSION_TTL_SECONDS`, `COOKIE_SECURE`,
 * and `COOKIE_HOST_PREFIX` are optional with defaults.
 *
 * A malformed `SESSION_TTL_SECONDS` throws rather than silently falling back to
 * the default, so a startup misconfiguration fails loudly. `COOKIE_SECURE` and
 * `COOKIE_HOST_PREFIX` instead fail secure: only the literal `false` clears
 * the flag. An explicit `COOKIE_NAME` is taken verbatim and never prefixed.
 *
 * The whole contract is validated before anything throws, and every problem is
 * reported in ONE error (Wallow-pu6a.3.7). Throwing on the first missing name
 * costs a fork one restart per variable when bringing up a new environment, and
 * a loader that runs lazily turns a boot-time misconfiguration into a 500 on the
 * first request instead.
 *
 * @param env Environment source. Defaults to `process.env`.
 */
export function loadBffConfigFromEnv(env: NodeJS.ProcessEnv = process.env): BffConfig {
  const problems: string[] = [];

  const require = (key: string): string => {
    const value: string | undefined = env[key];
    if (value === undefined || value === "") {
      problems.push(`Missing required environment variable: ${key}`);
      return "";
    }
    return value;
  };

  const scopesRaw: string | undefined = env.OIDC_SCOPES;
  const scopes: string[] =
    scopesRaw !== undefined && scopesRaw.trim() !== ""
      ? scopesRaw.trim().split(/\s+/u)
      : ["openid", "profile", "email", "offline_access"];

  const issuer: string = require("OIDC_ISSUER");
  const clientId: string = require("OIDC_CLIENT_ID");
  const clientSecret: string = require("OIDC_CLIENT_SECRET");
  const redirectUri: string = require("OIDC_REDIRECT_URI");
  const postLogoutRedirectUri: string = require("OIDC_POST_LOGOUT_REDIRECT_URI");
  const apiBaseUrl: string = require("BFF_API_BASE_URL");
  const cookiePassword: string = require("COOKIE_PASSWORD");

  const ttlRaw: string = (env.SESSION_TTL_SECONDS ?? "").trim();
  let sessionTtlSeconds: number = DEFAULT_SESSION_TTL_SECONDS;
  if (ttlRaw !== "") {
    const parsed: number = Number(ttlRaw);
    if (!Number.isInteger(parsed) || parsed < MIN_SESSION_TTL_SECONDS) {
      problems.push(
        `Invalid environment variable SESSION_TTL_SECONDS: expected a positive whole number of seconds, got "${ttlRaw}"`,
      );
    } else {
      sessionTtlSeconds = parsed;
    }
  }

  if (problems.length !== NO_PROBLEMS) {
    throw new Error(`Invalid BFF environment configuration:\n  - ${problems.join("\n  - ")}`);
  }

  const secureRaw: string = (env.COOKIE_SECURE ?? "").trim().toLowerCase();
  const cookieSecure: boolean = secureRaw !== "false";

  const hostPrefixRaw: string = (env.COOKIE_HOST_PREFIX ?? "").trim().toLowerCase();

  return {
    issuer,
    clientId,
    clientSecret,
    redirectUri,
    postLogoutRedirectUri,
    scopes,
    apiBaseUrl,
    cookieName: env.COOKIE_NAME ?? defaultCookieName(hostPrefixRaw, cookieSecure),
    cookiePassword,
    metadataUrl:
      env.OIDC_METADATA_URL !== undefined && env.OIDC_METADATA_URL !== ""
        ? env.OIDC_METADATA_URL
        : undefined,
    sessionTtlSeconds,
    cookieSecure,
  };
}
