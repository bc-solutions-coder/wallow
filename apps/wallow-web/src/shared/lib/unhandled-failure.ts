/**
 * What wallow-web does with a failure no call site claimed: the query client's
 * `onUnhandledFailure` callback (`app/router.tsx`).
 *
 * Resolves the sentence through the app registry, toasts it with the
 * quotable reference when there is one, and records one warning through the
 * app logger: code, status, and reference as attributes, the failure itself in
 * the error slot (its message is the `[status code] title` log line) — never
 * the sentence, which is copy, and never the body, which may carry a person's
 * input.
 *
 * Two failures say nothing: an aborted request (`isSilentFailure` — the caller
 * walked away, so nobody is waiting on a sentence), and anything raised
 * without a document. A query with `toastFailure` meta can fail inside a
 * server-side loader, where there is no toaster mounted and no browser log
 * transport; the server's own request log already has that failure.
 */
import {
  type ApiFailure,
  type FailureReference,
  failureReference,
  isSilentFailure,
  resolveFailureMessage,
  toApiFailure,
} from "@bc-solutions-coder/api-errors";
import type { UnhandledFailure } from "@bc-solutions-coder/query";
import { toastFailure } from "@bc-solutions-coder/ui/failure-toast";

import { failureMessages } from "./failure-messages";
import { log } from "./log";

/** The log event one unhandled failure records. */
export const UNHANDLED_FAILURE_EVENT = "query.failure.unhandled";

export function reportUnhandledFailure({ kind, error }: UnhandledFailure): void {
  if (typeof document === "undefined") {
    return;
  }

  const failure: ApiFailure = toApiFailure(error);
  if (isSilentFailure(failure)) {
    return;
  }

  const reference: FailureReference | undefined = failureReference(failure);

  toastFailure(resolveFailureMessage(failure, { registry: failureMessages }), reference);
  log.warn(
    UNHANDLED_FAILURE_EVENT,
    {
      kind,
      code: failure.code,
      status: failure.status,
      reference: reference?.traceId ?? reference?.requestId,
    },
    failure,
  );
}
