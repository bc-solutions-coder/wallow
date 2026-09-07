import { LIMITS } from "./limits.js";

const ROOT_DEPTH = 0;
const NEXT_DEPTH = 1;
const FIRST_FRAME = 1;
const START = 0;
const excluded =
  /(?:name|email|ip(?:address)?|body|form|cookie|token|secret|password|authorization|baggage|session|user|organization)/iu;
const identity = /^(?:service|deployment|wallow)[._]/u;
const encoder = new TextEncoder();

/** Remove URL credentials, query strings and fragments; retain an HTTP origin and path. */
export function sanitizeUrl(value: string): string {
  try {
    const url = new URL(value);
    return /^(?:http|https):$/u.test(url.protocol)
      ? `${url.hostname.includes(":") || /^(?:\d{1,3}\.){3}\d{1,3}$/u.test(url.hostname) ? `${url.protocol}//[redacted]` : url.origin}${url.pathname}`
      : "[redacted]";
  } catch {
    return "[redacted]";
  }
}

function text(value: string): string {
  if (value.length > LIMITS.valueBytes || encoder.encode(value).byteLength > LIMITS.valueBytes) {
    return "[oversize]";
  }
  const withoutEmail = value.includes("@")
    ? value.replaceAll(/[\w.+-]{1,64}@[\w.-]+\.[a-z]{2,}/giu, "[redacted]")
    : value;
  const scrubbed = withoutEmail
    .replaceAll(/https?:\/\/[^\s<>"']+/giu, sanitizeUrl)
    .replaceAll(/\b(?:\d{1,3}\.){3}\d{1,3}\b/gu, "[redacted]")
    .replaceAll(/(?<![\w:])(?:[a-f0-9]{0,4}:){2,}[a-f0-9:.]{0,39}(?![\w:])/giu, "[redacted]")
    .replaceAll(/\b(?:bearer|basic)\s+\S+/giu, "[redacted]")
    .replaceAll(/\beyJ[\w-]+\.[\w-]+\.[\w-]+\b/gu, "[redacted]");
  return encoder.encode(scrubbed).byteLength <= LIMITS.valueBytes ? scrubbed : "[oversize]";
}

/** Apply the same bounded recursive policy at producer and relay boundaries. */
export function sanitizeAttributes(input: unknown): Record<string, unknown> {
  const seen = new WeakSet<object>();
  let remaining = LIMITS.nodes;
  function visit(value: unknown, depth: number): unknown {
    remaining -= NEXT_DEPTH;
    if (depth > LIMITS.depth || remaining < ROOT_DEPTH) {
      return "[limit]";
    }
    if (typeof value === "string") {
      return text(value);
    }
    if (typeof value === "boolean" || value === null) {
      return value;
    }
    if (typeof value === "number") {
      return Number.isFinite(value) ? value : null;
    }
    if (typeof value !== "object") {
      return "[redacted]";
    }
    if (seen.has(value)) {
      return "[circular]";
    }
    seen.add(value);
    if (Array.isArray(value)) {
      return value
        .slice(START, LIMITS.attributes)
        .map((item: unknown) => visit(item, depth + NEXT_DEPTH));
    }
    return Object.fromEntries(
      Object.entries(value)
        .slice(START, LIMITS.attributes)
        .map(([key, item]) => [
          key.slice(START, LIMITS.identityLength),
          excluded.test(key) || identity.test(key) ? "[redacted]" : visit(item, depth + NEXT_DEPTH),
        ]),
    );
  }
  if (input === null || typeof input !== "object" || Array.isArray(input)) {
    return {};
  }
  const result = visit(input, ROOT_DEPTH);
  return result !== null && typeof result === "object" && !Array.isArray(result)
    ? Object.fromEntries(Object.entries(result))
    : {};
}

/** Exception messages may contain personal content. Keep only bounded code locations. */
export function sanitizeException(error: unknown): Record<string, unknown> {
  if (!(error instanceof Error)) {
    return { "exception.type": "UnknownError" };
  }
  const frames = (error.stack ?? "")
    .split("\n")
    .slice(FIRST_FRAME, FIRST_FRAME + LIMITS.stackFrames)
    .flatMap((line) => {
      const location = /(?:\/|\\)(?<location>[\w.-]+:\d+:\d+)\)?$/u.exec(line.trim());
      return location?.groups?.location === undefined ? [] : [location.groups.location];
    });
  return {
    "exception.type": /^(?:Type|Range|Syntax|Reference|URI|Eval)?Error$/u.test(error.name)
      ? error.name
      : "Error",
    "exception.stacktrace": frames.join("\n"),
  };
}
