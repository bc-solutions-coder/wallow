/**
 * Read the first field error, accepting a string or an issue with a string message.
 */

/**
 * Return the first error if it is a string or has a string message. An unrecognized first entry
 * returns undefined even when a later entry contains a message.
 */
export function firstErrorMessage(errors: readonly unknown[]): string | undefined {
  const first: unknown = errors[0];

  if (typeof first === "string") {
    return first;
  }

  if (typeof first === "object" && first !== null && "message" in first) {
    const message: unknown = (first as { message: unknown }).message;

    if (typeof message === "string") {
      return message;
    }
  }

  return undefined;
}
