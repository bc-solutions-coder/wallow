/** HTTP methods the BFF does not gate on CSRF, per RFC 9110 safe methods. */
const safeMethods: ReadonlySet<string> = new Set(["GET", "HEAD", "OPTIONS"]);

/** True when the method is CSRF-exempt (safe), matched case-insensitively. */
export function isSafeMethod(method: string): boolean {
  return safeMethods.has(method.toUpperCase());
}
