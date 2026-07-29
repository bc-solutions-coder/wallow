/**
 * Sealed login-transaction state cookie helpers.
 *
 * Stores the PKCE verifier, OAuth `state`, OIDC `nonce`, and post-login
 * `returnTo` path between the authorize redirect and the callback, using the
 * same iron-webcrypto sealed-cookie pattern as `session.ts`.
 */

import { defaults, seal, unseal } from "iron-webcrypto";

import { type CookieSecret } from "./config";
import { sealPassword, unsealPassword } from "./cookie-secret";
import { webCrypto } from "./webcrypto";

/*
 * The tx cookie rotates on the same keys as the session cookie: it is sealed at
 * the authorize redirect and unsealed at the callback seconds later, so leaving
 * it on the single-secret path would abort every in-flight login the moment a
 * rotation deployed.
 */

/**
 * The transient state carried across the OIDC authorize/callback round-trip.
 */
export interface LoginTx {
  /** OAuth `state` parameter used for CSRF protection. */
  state: string;
  /** OIDC `nonce` bound to the id_token. */
  nonce: string;
  /** PKCE code verifier exchanged for tokens at the callback. */
  verifier: string;
  /** Path to return the user to after a successful login. */
  returnTo: string;
}

/**
 * Seal a {@link LoginTx} into an opaque cookie value.
 *
 * @param tx The login transaction state to seal.
 * @param password Secret used to encrypt and sign the payload, or a keyed
 *        {@link CookieSecret} set whose ACTIVE key seals the cookie.
 * @returns The sealed cookie string.
 */
export function sealTx(tx: LoginTx, password: CookieSecret): Promise<string> {
  return seal(webCrypto, tx, sealPassword(password), defaults);
}

/**
 * Unseal a cookie value back into a {@link LoginTx}.
 *
 * @param sealed The sealed cookie string.
 * @param password Secret used to decrypt and verify the payload, or a keyed
 *        {@link CookieSecret} set — any key in it may have sealed the cookie.
 * @returns The decoded transaction, or `null` if the value is invalid.
 */
export async function unsealTx(sealed: string, password: CookieSecret): Promise<LoginTx | null> {
  try {
    return (await unseal(webCrypto, sealed, unsealPassword(password), defaults)) as LoginTx;
  } catch {
    return null;
  }
}
