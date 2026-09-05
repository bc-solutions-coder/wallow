import { isApiFailure } from "@bc-solutions-coder/api-errors";
import { notFound } from "@tanstack/react-router";

/** The one status a record-by-id read answers when the record does not exist. */
const NOT_FOUND_STATUS = 404;

/**
 * Turn an API 404 from a loader read into the router's own not-found signal.
 *
 * A loader that rejects with an `ApiFailure` lands on the root error boundary,
 * which already renders the not-found screen for a 404 — but router-core sets
 * the SSR response status to 404 only for `notFound()`, so a missing record
 * otherwise answers 500 with the right screen. Every other failure is rethrown
 * untouched and keeps its `FailureBanner` branch in the boundary.
 */
export async function notFoundOn404<T>(read: Promise<T>): Promise<T> {
  try {
    return await read;
  } catch (error: unknown) {
    if (isApiFailure(error) && error.status === NOT_FOUND_STATUS) {
      throw notFound();
    }
    throw error;
  }
}
