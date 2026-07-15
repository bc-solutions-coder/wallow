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

// The `[0]` is a type-level tuple index (iron-webcrypto's first seal parameter),
// not a runtime literal, so no-magic-numbers is a false positive here and there
// is no named constant a type position can reference.
// eslint-disable-next-line no-magic-numbers
type SealCrypto = Parameters<typeof seal>[0];

/** The runtime Web Crypto instance, typed for iron-webcrypto's seal/unseal. */
export const webCrypto: SealCrypto = globalThis.crypto as unknown as SealCrypto;
