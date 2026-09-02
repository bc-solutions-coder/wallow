/**
 * PROTOTYPE — wayfinder ticket #168 (map #163). Throwaway stand-ins for what
 * `@bc-solutions-coder/api-errors` will export (ticket #166/#167). Duck-typed on
 * purpose: the real package does not exist yet and this file is deleted with the
 * branch.
 */

/** The opt-out marker a mutation sets when a screen shows its own failure. */
export const FAILURE_HANDLED_META = { failureHandled: true } as const;

/** `meta` helper for a mutation whose failure is rendered by the screen itself. */
export function handledFailure<TMeta extends Record<string, unknown> | undefined>(
  meta?: TMeta,
): Record<string, unknown> {
  return { ...meta, ...FAILURE_HANDLED_META };
}

interface FailureShape {
  readonly status?: number;
  readonly code?: string;
  readonly detail?: string;
  readonly traceId?: string;
  readonly requestId?: string;
}

function asFailure(error: unknown): FailureShape | undefined {
  return typeof error === "object" && error !== null && "status" in error && "code" in error
    ? (error as FailureShape)
    : undefined;
}

const STATUS_DEFAULTS: Readonly<Record<number, string>> = {
  401: "Your session has expired. Please sign in again.",
  403: "You don't have permission to do that.",
  404: "That could not be found.",
  409: "That change conflicts with a newer one. Refresh and try again.",
  429: "Too many requests. Please wait a moment and try again.",
};

const GENERIC = "Something went wrong on our side. Please try again later.";

/** The #167 precedence, minus registries: code defaults → detail (4xx) → status → generic. */
export function resolveFailureMessagePrototype(error: unknown): string {
  const f = asFailure(error);
  if (f === undefined) {return GENERIC;}
  if (f.code === "NETWORK_ERROR" || f.code === "Transport.NetworkError") {
    return "Unable to reach the server. Check your connection and try again.";
  }
  const status = f.status ?? 500;
  if (status >= 500 || f.code === "UNKNOWN") {return GENERIC;}
  if (f.detail !== undefined && f.detail !== "") {return f.detail;}
  return STATUS_DEFAULTS[status] ?? GENERIC;
}

/** The reference a toast shows so a user can quote it to support. */
export function failureReference(error: unknown): { traceId?: string; requestId?: string } {
  const f = asFailure(error);
  return { traceId: f?.traceId, requestId: f?.requestId };
}
