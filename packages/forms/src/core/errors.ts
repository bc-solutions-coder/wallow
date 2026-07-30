/**
 * Normalization of a TanStack field's `state.meta.errors` into the one string a
 * field's `Field.Error` renders.
 *
 * That array is deliberately loosely typed by the framework (`ValidationError =
 * unknown`) because its entries come from three different producers: function
 * validators push plain strings, standard-schema (zod) validators push
 * `{ message }` issue objects, and server errors arrive through
 * `form.setErrorMap({ onServer: ... })` as plain strings. Every catalog field
 * displays whichever of those landed first, so the unwrapping lives here once.
 *
 * Layer 0 of the package: `src/core/` imports nothing from `src/fields/` or
 * `src/form/`.
 */

/**
 * The first displayable message in a field's error list, or `undefined` when
 * there is none (no errors, or a shape carrying no string message).
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
