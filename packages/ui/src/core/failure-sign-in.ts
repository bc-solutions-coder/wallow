import { ClientErrorCode, ErrorCode } from "@bc-solutions-coder/api-errors";

const SIGN_IN_CODES: ReadonlySet<string> = new Set([
  ErrorCode.AUTH_UNAUTHENTICATED,
  ClientErrorCode.BFF_SESSION_MISSING,
  ClientErrorCode.BFF_SESSION_REFRESH_FAILED,
]);

/** The sign-in destination for a recognized authentication failure, otherwise undefined. */
export function failureSignInHref(
  code: string,
  currentPath: string,
  signInHref?: string,
): string | undefined {
  if (!SIGN_IN_CODES.has(code)) {
    return undefined;
  }

  return signInHref ?? `/bff/login?returnTo=${encodeURIComponent(currentPath)}`;
}
