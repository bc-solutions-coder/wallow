/**
 * How an `ApiFailure` crosses the SSR seam.
 *
 * A loader that fails on the server leaves its error on the match, and Start
 * dehydrates that match into the document for the client to hydrate. The
 * default error serializer keeps only the message and rebuilds a bare `Error`,
 * which loses the api-errors brand along with the status, code, and ids — so
 * the hydrated boundary would see a render bug where the server saw a 404 or a
 * 503, and paint different copy from the HTML it hydrates. This adapter carries
 * the failure's own fields across and rebuilds the same class on the client.
 * `cause` stays on the server: it is the transport's error, not the browser's.
 */
import { ApiFailure, isApiFailure } from "@bc-solutions-coder/api-errors";
import { createSerializationAdapter } from "@tanstack/react-router";

/** The fields that make an `ApiFailure`, as plain data. */
export interface SerializedApiFailure {
  readonly status: number;
  readonly code: string;
  readonly title: string;
  readonly detail?: string;
  readonly traceId?: string;
  readonly requestId?: string;
  readonly fieldErrors?: Readonly<Record<string, readonly string[]>>;
  readonly retryAfter?: number;
}

/** Only the fields a failure carries, so the document does not spell out absent ones. */
function present<T extends object>(fields: T): T {
  return Object.fromEntries(Object.entries(fields).filter(([, value]) => value !== undefined)) as T;
}

export const apiFailureSerialization = createSerializationAdapter({
  key: "wallow:api-failure",
  test: isApiFailure,
  toSerializable: (failure: ApiFailure): SerializedApiFailure =>
    present({
      status: failure.status,
      code: failure.code,
      title: failure.title,
      detail: failure.detail,
      traceId: failure.traceId,
      requestId: failure.requestId,
      fieldErrors: failure.fieldErrors,
      retryAfter: failure.retryAfter,
    }),
  fromSerializable: (fields: SerializedApiFailure): ApiFailure => new ApiFailure(fields),
});
