/**
 * Server-side BFF configuration for the OIDC tunnel.
 *
 * These values are read once at startup (typically from environment variables)
 * and passed to the OIDC and session helpers. Nothing here is safe to expose to
 * the browser — the client secret and cookie password are confidential.
 */
/**
 * The cookie secrets in play during a password rotation.
 *
 * `keys` maps a key ID to its secret; every entry can UNSEAL an existing cookie,
 * while only `activeKeyId` SEALS new ones. Deploying a new key alongside the old
 * one therefore keeps every live session readable, and the old key can be
 * dropped once its cookies have aged out — rotating the single secret instead
 * 401s every signed-in browser at once.
 *
 * iron-webcrypto embeds the key ID inside the sealed blob and picks the matching
 * entry on unseal, so a caller never tries the keys itself.
 */
export interface CookiePasswordSet {
  /** Key ID whose secret seals newly issued cookies. Must exist in {@link keys}. */
  activeKeyId: string;
  /** Every accepted key ID mapped to its secret, including the active one. */
  keys: Record<string, string>;
}

/**
 * A cookie secret in either accepted form: a bare string (the single-secret
 * path, unchanged) or a keyed {@link CookiePasswordSet} for rotation.
 */
export type CookieSecret = string | CookiePasswordSet;

/**
 * Key ID used when only the single `COOKIE_PASSWORD` is configured.
 *
 * The value is load-bearing and cannot be an arbitrary label like `"1"`:
 * iron-webcrypto seals a bare-string password with an EMPTY id and normalizes
 * that empty id back to the literal `"default"` when unsealing against a key
 * map. A map keyed by anything else fails every cookie sealed by an earlier
 * build with `Cannot find password: default` — i.e. the mass session
 * invalidation this whole feature exists to avoid.
 */
export const DEFAULT_COOKIE_KEY_ID: string = "default";

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
  /**
   * The ACTIVE password used to seal the session and transaction cookies —
   * unchanged in shape and meaning, and still the whole story when no rotation
   * is in progress. Prefer {@link cookiePasswords} when unsealing, so cookies
   * sealed under a retired key are still accepted.
   */
  cookiePassword: string;
  /**
   * Every cookie secret currently accepted, and which one seals new cookies.
   * Built from `COOKIE_PASSWORDS` when set, otherwise a single entry wrapping
   * {@link cookiePassword}.
   *
   * {@link loadBffConfigFromEnv} always populates it. It stays OPTIONAL because
   * `BffConfig` is public API a fork may build by hand, and requiring a new
   * field would break every such caller; those callers keep working on
   * {@link cookiePassword} alone. Seal/unseal sites therefore pass
   * `config.cookiePasswords ?? config.cookiePassword`, which is exactly a
   * {@link CookieSecret}.
   */
  cookiePasswords?: CookiePasswordSet;
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
 * The shortest `COOKIE_PASSWORD` iron-webcrypto will seal a session with.
 *
 * Below this the seal happens to fail inside the OIDC callback rather than at
 * boot, surfacing as a 500 mid-login with nothing pointing at the password, so
 * the length is part of the environment contract validated here.
 */
const MIN_COOKIE_PASSWORD_LENGTH = 32;

/** A clean environment contract: no collected problems to report. */
const NO_PROBLEMS = 0;

/** A `COOKIE_PASSWORDS` object naming no keys at all, which could seal nothing. */
const NO_KEYS = 0;

/**
 * The key IDs iron-webcrypto will seal with. Anything outside this throws
 * `Invalid password id` inside `seal()`, which without this check surfaces as a
 * 500 in the login callback instead of a boot failure.
 */
const COOKIE_KEY_ID_PATTERN = /^\w+$/u;

/**
 * Key IDs that JavaScript would reorder, which `COOKIE_PASSWORDS` cannot allow.
 *
 * The active key is the FIRST one the JSON names, but an object's integer-like
 * keys are enumerated in ascending numeric order ahead of every string key
 * regardless of where they were written. `{"2": new, "1": old}` therefore makes
 * the OLD secret active and silently seals new cookies with the key the operator
 * is retiring — so an all-digit ID is rejected rather than quietly misread.
 */
const NUMERIC_COOKIE_KEY_ID = /^\d+$/u;

/** Shared prefix for every `COOKIE_PASSWORDS` problem, so they read as one group. */
const COOKIE_PASSWORDS_PROBLEM: string = "Invalid environment variable COOKIE_PASSWORDS";

/**
 * The reason `keyId` cannot name a rotation key, or `undefined` if it can.
 */
function cookieKeyIdProblem(keyId: string): string | undefined {
  if (!COOKIE_KEY_ID_PATTERN.test(keyId)) {
    return `${COOKIE_PASSWORDS_PROBLEM}: key ID "${keyId}" must be letters, digits or underscores, which is all iron-webcrypto will seal with`;
  }
  if (NUMERIC_COOKIE_KEY_ID.test(keyId)) {
    return `${COOKIE_PASSWORDS_PROBLEM}: key ID "${keyId}" must not be all digits, because such keys are reordered ahead of the others and would change which key is active`;
  }
  return undefined;
}

/**
 * Parse the `COOKIE_PASSWORDS` JSON object into a {@link CookiePasswordSet}.
 *
 * Every problem found is pushed onto `problems` rather than thrown, so a bad key
 * map is reported in the same aggregated error as every other misconfiguration.
 * Returns `undefined` when the value cannot yield a usable set, in which case
 * the caller has already collected the reason.
 */
function parseCookiePasswords(raw: string, problems: string[]): CookiePasswordSet | undefined {
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    problems.push(`${COOKIE_PASSWORDS_PROBLEM}: expected a JSON object of key ID to secret`);
    return undefined;
  }

  if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) {
    problems.push(`${COOKIE_PASSWORDS_PROBLEM}: expected a JSON object of key ID to secret`);
    return undefined;
  }

  const entries: [string, unknown][] = Object.entries(parsed as Record<string, unknown>);
  if (entries.length === NO_KEYS) {
    problems.push(`${COOKIE_PASSWORDS_PROBLEM}: expected at least one key ID, got an empty object`);
    return undefined;
  }

  const keys: Record<string, string> = {};
  for (const [keyId, secret] of entries) {
    const idProblem: string | undefined = cookieKeyIdProblem(keyId);
    if (idProblem !== undefined) {
      problems.push(idProblem);
    } else if (typeof secret !== "string" || secret.length < MIN_COOKIE_PASSWORD_LENGTH) {
      const got: string = typeof secret === "string" ? `${secret.length}` : typeof secret;
      problems.push(
        `${COOKIE_PASSWORDS_PROBLEM}: expected key "${keyId}" to be a secret of at least ${MIN_COOKIE_PASSWORD_LENGTH} characters, got ${got}`,
      );
    } else {
      keys[keyId] = secret;
    }
  }

  // The first key the JSON names seals new cookies; the rest stay valid for
  // unsealing until their cookies age out.
  const [activeKeyId] = Object.keys(keys);
  if (activeKeyId === undefined) {
    return undefined;
  }

  return { activeKeyId, keys };
}

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
 * `COOKIE_PASSWORD` must also be at least 32 characters — the minimum
 * iron-webcrypto seals a session with — so a too-short secret fails here at
 * boot instead of inside the first OIDC callback.
 *
 * `COOKIE_PASSWORDS` is the optional rotation form: a JSON object of key ID to
 * secret, e.g. `{"v2":"<32+ chars>","v1":"<32+ chars>"}`. The FIRST key seals new
 * cookies and every key stays valid for unsealing, which is what lets a secret be
 * replaced without 401ing every live session. It makes `COOKIE_PASSWORD`
 * unnecessary (and overrides it when both are set), and each of its secrets is
 * held to the same 32-character minimum. When it is unset the single password is
 * wrapped under {@link DEFAULT_COOKIE_KEY_ID}, so both paths produce the same
 * shape.
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

  const requireEnv = (key: string): string => {
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

  const issuer: string = requireEnv("OIDC_ISSUER");
  const clientId: string = requireEnv("OIDC_CLIENT_ID");
  const clientSecret: string = requireEnv("OIDC_CLIENT_SECRET");
  const redirectUri: string = requireEnv("OIDC_REDIRECT_URI");
  const postLogoutRedirectUri: string = requireEnv("OIDC_POST_LOGOUT_REDIRECT_URI");
  const apiBaseUrl: string = requireEnv("BFF_API_BASE_URL");
  // A key map carries the active secret itself, so COOKIE_PASSWORD is required
  // only on the single-secret path. Setting both is allowed and the map wins —
  // that is what lets an operator add COOKIE_PASSWORDS to a running deployment
  // without first removing the variable the old build still needs on rollback.
  const passwordsRaw: string = (env.COOKIE_PASSWORDS ?? "").trim();
  const cookiePasswords: CookiePasswordSet | undefined =
    passwordsRaw === "" ? undefined : parseCookiePasswords(passwordsRaw, problems);

  let cookiePassword: string = passwordsRaw === "" ? requireEnv("COOKIE_PASSWORD") : "";
  if (passwordsRaw === "") {
    // A missing password is already reported by requireEnv(), which returns ""; the
    // length guard keeps it from being reported a second time as "too short".
    if (cookiePassword !== "" && cookiePassword.length < MIN_COOKIE_PASSWORD_LENGTH) {
      problems.push(
        `Invalid environment variable COOKIE_PASSWORD: expected at least ${MIN_COOKIE_PASSWORD_LENGTH} characters, got ${cookiePassword.length}`,
      );
    }
  } else if (cookiePasswords !== undefined) {
    // Keep the back-compat field meaning exactly what it always did: the secret
    // new cookies are sealed with. Seal-only call sites therefore need no change.
    cookiePassword = cookiePasswords.keys[cookiePasswords.activeKeyId] ?? "";
  }

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
    // Always populated: the parsed key map, or the single password wrapped under
    // the one ID iron gives an unkeyed seal, so every unseal site can take the
    // same shape whether or not a rotation is in progress.
    cookiePasswords: cookiePasswords ?? {
      activeKeyId: DEFAULT_COOKIE_KEY_ID,
      keys: { [DEFAULT_COOKIE_KEY_ID]: cookiePassword },
    },
    metadataUrl:
      env.OIDC_METADATA_URL !== undefined && env.OIDC_METADATA_URL !== ""
        ? env.OIDC_METADATA_URL
        : undefined,
    sessionTtlSeconds,
    cookieSecure,
  };
}
