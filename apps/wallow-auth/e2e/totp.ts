// oxlint-disable no-magic-numbers, unicorn/number-literal-case -- the numeric and
// hex literals below (5/8-bit base32 regrouping, the 0x7f/0xff/0x0f dynamic-truncation
// masks, 24/16/8 shifts) ARE the RFC 6238 / RFC 4648 algorithm; naming each one
// (BITS_PER_BYTE = 8, …) would obscure the spec rather than clarify it.
import { createHmac } from "node:crypto";

/**
 * RFC 6238 TOTP generator for the backend-dependent MFA specs (mfa.spec.ts). NOT
 * a spec file — its name is outside Playwright's `*.spec.ts` glob, so the runner
 * never treats it as a test.
 *
 * The parameters mirror the server exactly (MfaService.cs): HMAC-SHA1, 6 digits,
 * a 30-second step, over a base32 secret using the standard RFC 4648 alphabet
 * (`ABCDEFGHIJKLMNOPQRSTUVWXYZ234567`). The server validates with a +/-1 step
 * window, so a code minted here at submit time still verifies if the window rolls
 * between the click and the server's check.
 *
 * There is no otplib/otpauth dependency in this workspace, and adding one to run
 * a single test would be out of scope for the E2E task — so the ~30 lines it
 * takes to compute a TOTP by hand live here instead.
 */

const BASE32_ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
const TOTP_STEP_SECONDS = 30;
const TOTP_DIGITS = 6;
const TOTP_MODULUS = 1_000_000;

/** Decode an unpadded, standard-alphabet base32 string into its bytes. */
function base32Decode(secret: string): Buffer {
  const cleaned: string = secret.replace(/=+$/u, "").toUpperCase();
  const bytes: number[] = [];
  let buffer = 0;
  let bitsLeft = 0;

  for (const char of cleaned) {
    const value: number = BASE32_ALPHABET.indexOf(char);

    if (value === -1) {
      throw new Error(`invalid base32 character: ${char}`);
    }

    buffer = (buffer << 5) | value;
    bitsLeft += 5;

    if (bitsLeft >= 8) {
      bitsLeft -= 8;
      bytes.push((buffer >> bitsLeft) & 0xff);
    }
  }

  return Buffer.from(bytes);
}

/**
 * Compute the 6-digit TOTP for `base32Secret` at `atMs` (default now), matching
 * the server's ComputeTotp: HMAC-SHA1 over the big-endian step counter, dynamic
 * truncation, then modulo 10^6 zero-padded to six digits.
 */
export function generateTotp(base32Secret: string, atMs: number = Date.now()): string {
  const step: bigint = BigInt(Math.floor(atMs / 1000 / TOTP_STEP_SECONDS));
  const counter: Buffer = Buffer.alloc(8);
  counter.writeBigUInt64BE(step);

  const hmac: Buffer = createHmac("sha1", base32Decode(base32Secret)).update(counter).digest();
  const offset: number = (hmac.at(-1) ?? 0) & 0x0f;
  const binaryCode: number =
    ((hmac[offset] & 0x7f) << 24) |
    ((hmac[offset + 1] & 0xff) << 16) |
    ((hmac[offset + 2] & 0xff) << 8) |
    (hmac[offset + 3] & 0xff);

  return (binaryCode % TOTP_MODULUS).toString().padStart(TOTP_DIGITS, "0");
}
