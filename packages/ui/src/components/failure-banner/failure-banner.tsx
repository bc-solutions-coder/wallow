import {
  type ApiFailure,
  ClientErrorCode,
  ErrorCode,
  type FailureMessageRegistry,
  failureReference,
  toApiFailure,
} from "@bc-solutions-coder/api-errors";
import { type ReactElement, type ReactNode, useState, useSyncExternalStore } from "react";

import { Button } from "../button/button";
import { ErrorBanner, type ErrorBannerProps } from "../error-banner/error-banner";
import { useFailureMessage } from "../failure-messages/failure-messages";

export interface FailureBannerProps extends Omit<ErrorBannerProps, "children"> {
  /**
   * The failure to show; anything not already an `ApiFailure` is classified
   * first. Nullish renders nothing, so a query's `error` passes straight in.
   */
  readonly error: unknown;
  /** Sentences for this call site alone; they win over the registry. */
  readonly messages?: FailureMessageRegistry | undefined;
  /** The call site's own last resort, ahead of the generic sentence. */
  readonly fallback?: string | undefined;
  /** When given, the banner offers "Try again" and calls this. */
  readonly onRetry?: (() => void) | undefined;
  /**
   * Where "Sign in" goes on a 401 code. Defaults to the BFF login with the
   * current path to return to; an app without a BFF passes its own route.
   */
  readonly signInHref?: string | undefined;
  /** Phrasing content rendered after the sentence and its actions. */
  readonly children?: ReactNode;
}

/** The BFF login route; the banner links to it directly, so `ui` needs no SDK. */
const BFF_LOGIN_PATH = "/bff/login";

/** The codes that mean "sign in again", whatever the copy says. */
const SIGN_IN_CODES: ReadonlySet<string> = new Set([
  ErrorCode.AUTH_UNAUTHENTICATED,
  ClientErrorCode.BFF_SESSION_MISSING,
  ClientErrorCode.BFF_SESSION_REFRESH_FAILED,
]);

/** The inline action style: a link-variant button with the box padding removed. */
const ACTION_CLASS = "h-auto p-0 text-sm";

/** The location never notifies; a route change remounts the banner anyway. */
function subscribeToNothing(): () => void {
  return subscribeToNothing;
}

/** The current path and query, `"/"` on the server so hydration never mismatches. */
function useCurrentPath(): string {
  return useSyncExternalStore(
    subscribeToNothing,
    () => `${globalThis.location.pathname}${globalThis.location.search}`,
    () => "/",
  );
}

/** A denied clipboard has no second channel to report through; the label stays. */
function ignoreClipboardDenial(): void {
  // Deliberately empty — see the doc comment.
}

function RetryAction({ onRetry }: { readonly onRetry: () => void }): ReactElement {
  return (
    <Button variant="link" size="sm" className={ACTION_CLASS} onClick={onRetry}>
      Try again
    </Button>
  );
}

function SignInLink({ href }: { readonly href: string }): ReactElement {
  return (
    <Button
      variant="link"
      size="sm"
      className={ACTION_CLASS}
      nativeButton={false}
      render={<a href={href} />}
    >
      Sign in
    </Button>
  );
}

function ReferenceLine({ reference }: { readonly reference: string }): ReactElement {
  const [copied, setCopied] = useState(false);

  return (
    <span className="mt-1 block text-xs">
      Reference {reference}{" "}
      <Button
        variant="link"
        size="sm"
        className="h-auto p-0 text-xs"
        onClick={() => {
          // Absent on a plain-http origin, where the label simply never changes.
          navigator.clipboard?.writeText(reference).then(() => {
            setCopied(true);
          }, ignoreClipboardDenial);
        }}
      >
        {copied ? "Copied" : "Copy reference"}
      </Button>
    </span>
  );
}

/**
 * The inline failure surface: `ErrorBanner` around the sentence
 * `useFailureMessage` resolves, plus the affordances the failure's status rule
 * allows. A 401 code gets a "Sign in" link back through the BFF with the
 * current path to return to; a transport or 5xx failure gets its reference and
 * a copy action; `onRetry` adds "Try again" to any of them.
 */
export function FailureBanner({
  error,
  messages,
  fallback,
  onRetry,
  signInHref,
  children,
  ...rest
}: FailureBannerProps): ReactElement | null {
  const message: string | null = useFailureMessage(error, { messages, fallback });
  const currentPath: string = useCurrentPath();

  if (message === null) {
    return null;
  }

  const failure: ApiFailure = toApiFailure(error);
  const ids = failureReference(failure);
  const reference: string | undefined = ids?.traceId ?? ids?.requestId;
  const signIn: boolean = SIGN_IN_CODES.has(failure.code);

  return (
    <ErrorBanner {...rest}>
      {message}
      {onRetry === undefined ? null : " "}
      {onRetry === undefined ? null : <RetryAction onRetry={onRetry} />}
      {signIn ? " " : null}
      {signIn ? (
        <SignInLink
          href={signInHref ?? `${BFF_LOGIN_PATH}?returnTo=${encodeURIComponent(currentPath)}`}
        />
      ) : null}
      {reference === undefined ? null : <ReferenceLine key={reference} reference={reference} />}
      {children}
    </ErrorBanner>
  );
}
