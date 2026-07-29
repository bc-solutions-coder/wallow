/**
 * Translating a {@link CookieSecret} into the password shapes iron-webcrypto
 * expects, for both sealed-cookie kinds (`session.ts` and `txstate.ts`).
 *
 * The two directions are deliberately asymmetric, because iron's own API is:
 * sealing names ONE key and embeds that name in the blob, while unsealing is
 * handed the WHOLE map and lets iron pick the entry the blob names. That is what
 * makes rotation work without the caller ever trying keys in a loop.
 */

import { type Password, type RawPassword } from "iron-webcrypto";

import { type CookieSecret } from "./config";

/**
 * The secret iron seals with: the active key and its ID, so the ID travels
 * inside the blob and identifies the key that has to unseal it later.
 *
 * A bare string is passed straight through — iron seals it with an EMPTY id,
 * which is exactly what every pre-rotation build wrote and what
 * `DEFAULT_COOKIE_KEY_ID` exists to match.
 */
export function sealPassword(secret: CookieSecret): RawPassword {
  if (typeof secret === "string") {
    return secret;
  }

  const active: string | undefined = secret.keys[secret.activeKeyId];
  if (active === undefined) {
    // Unreachable through `loadBffConfigFromEnv`, which only ever names a key it
    // just built. A hand-built config can still get here, and iron's own error
    // for an undefined secret says nothing about which key is missing.
    throw new Error(
      `Cookie password set has no secret for its active key ID "${secret.activeKeyId}"`,
    );
  }

  return { id: secret.activeKeyId, secret: active };
}

/**
 * The secret iron unseals with: the WHOLE key map, from which iron selects the
 * entry named by the ID embedded in the blob.
 *
 * Every key in the set stays valid for unsealing — that is the whole point of a
 * rotation window, and why this returns the map rather than just the active key.
 */
export function unsealPassword(secret: CookieSecret): Password | Record<string, Password> {
  return typeof secret === "string" ? secret : secret.keys;
}
