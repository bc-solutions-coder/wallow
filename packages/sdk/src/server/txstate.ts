/**
 * Sealed login-transaction state cookie helpers.
 *
 * Stores the PKCE verifier, OAuth `state`, OIDC `nonce`, and post-login
 * `returnTo` path between the authorize redirect and the callback, using the
 * same iron-webcrypto sealed-cookie pattern as `session.ts`.
 */

import { defaults, seal, unseal } from "iron-webcrypto";

import { webCrypto } from "./webcrypto";

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
 * @param password Secret used to encrypt and sign the payload.
 * @returns The sealed cookie string.
 */
export function sealTx(tx: LoginTx, password: string): Promise<string> {
  return seal(webCrypto, tx, password, defaults);
}

/**
 * Unseal a cookie value back into a {@link LoginTx}.
 *
 * @param sealed The sealed cookie string.
 * @param password Secret used to decrypt and verify the payload.
 * @returns The decoded transaction, or `null` if the value is invalid.
 */
export async function unsealTx(sealed: string, password: string): Promise<LoginTx | null> {
  try {
    return (await unseal(webCrypto, sealed, password, defaults)) as LoginTx;
  } catch {
    return null;
  }
}
