/**
 * Expose the runtime Web Crypto instance with the type expected by iron-webcrypto. The assertion
 * bridges incompatible library declarations without changing the runtime object.
 */

import type { seal } from "iron-webcrypto";

// The `[0]` is a type-level tuple index (iron-webcrypto's first seal parameter),
// not a runtime literal, so no-magic-numbers is a false positive here and there
// is no named constant a type position can reference.
// eslint-disable-next-line no-magic-numbers
type SealCrypto = Parameters<typeof seal>[0];

/** The runtime Web Crypto instance, typed for iron-webcrypto's seal/unseal. */
export const webCrypto: SealCrypto = globalThis.crypto as unknown as SealCrypto;
