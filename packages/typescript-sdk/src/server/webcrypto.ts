/**
 * Shared Web Crypto handle for iron-webcrypto's `seal`/`unseal`.
 *
 * iron-webcrypto's internal `_Crypto` parameter type differs from the DOM
 * `Crypto` lib type only in typed-array buffer variance (its `subtle` methods
 * accept `Uint8Array<ArrayBuffer>` where the DOM lib uses `BufferSource`).
 * Under TypeScript 5.7's generic typed arrays these are structurally
 * incompatible, so we cast `globalThis.crypto` once here and reuse it across
 * every sealed-cookie module rather than repeating the cast at each call site.
 */

import type { seal } from "iron-webcrypto";

/** The runtime Web Crypto instance, typed for iron-webcrypto's seal/unseal. */
export const webCrypto: Parameters<typeof seal>[0] =
  globalThis.crypto as unknown as Parameters<typeof seal>[0];
