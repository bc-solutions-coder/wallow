/**
 * PKCE (Proof Key for Code Exchange) helpers for the BFF authorization flow.
 *
 * Uses the Web Crypto API so the same code runs in Node 18+, Deno, and edge
 * runtimes without a Node-specific `crypto` import.
 */

/**
 * A PKCE verifier/challenge pair for a single authorization request.
 */
export interface PkcePair {
  /** The high-entropy code verifier, sent on the token exchange. */
  verifier: string;
  /** The S256 code challenge derived from the verifier, sent on authorize. */
  challenge: string;
}

/**
 * Generate a URL-safe, base64url-encoded random string.
 *
 * @param bytes Number of random bytes to draw. Defaults to 32.
 * @returns A base64url string (no padding) of the random bytes.
 */
export function randomUrlSafe(bytes: number = 32): string {
  const buffer: Uint8Array<ArrayBuffer> = new Uint8Array(bytes);
  globalThis.crypto.getRandomValues(buffer);
  return base64UrlEncode(buffer);
}

/**
 * Compute the S256 PKCE challenge for a given verifier.
 *
 * @param verifier The PKCE code verifier.
 * @returns The base64url-encoded SHA-256 digest of the verifier.
 */
export async function sha256Challenge(verifier: string): Promise<string> {
  const encoded: Uint8Array = new TextEncoder().encode(verifier);
  const data: ArrayBuffer = new ArrayBuffer(encoded.byteLength);
  new Uint8Array(data).set(encoded);
  const digest: ArrayBuffer = await globalThis.crypto.subtle.digest("SHA-256", data);
  return base64UrlEncode(new Uint8Array(digest));
}

/**
 * Create a fresh PKCE verifier/challenge pair.
 *
 * @returns A {@link PkcePair} with a 48-byte verifier and its S256 challenge.
 */
export async function createPkcePair(): Promise<PkcePair> {
  const verifier: string = randomUrlSafe(48);
  const challenge: string = await sha256Challenge(verifier);
  return { verifier, challenge };
}

/**
 * Encode raw bytes as a base64url string with no padding.
 */
function base64UrlEncode(bytes: Uint8Array): string {
  let binary: string = "";
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=/g, "");
}
