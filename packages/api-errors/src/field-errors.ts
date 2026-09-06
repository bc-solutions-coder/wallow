/**
 * Distributing a problem's field errors across the fields a form has.
 *
 * ASP.NET Core keys validation messages by property path — `DisplayName`,
 * `Branding.DisplayName` — while a form names its fields in camelCase, flat or
 * folded (`brandingDisplayName`). The split tries the exact key, then the
 * camelCase key, then the folded key, and hands what matches nothing back as
 * plain messages for the form to show as a banner.
 */

import type { ApiFailure } from "./failure";

/** The two surfaces a failed submit is split across. */
export interface SplitFieldErrors {
  /** Form field name → messages, for the keys that matched a known field. */
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;
  /** The messages of every key that matched no field, in the problem's order. */
  readonly unmatched: readonly string[];
}

const PATH_SEPARATOR: string = ".";
const FIRST_CHARACTER: number = 0;
const REST_OF_KEY: number = 1;

/**
 * Splits `failure.fieldErrors` across `knownFields`. A failure without field
 * errors yields an empty split rather than throwing.
 */
export function splitFieldErrors(
  failure: ApiFailure,
  knownFields: readonly string[],
): SplitFieldErrors {
  const known: ReadonlySet<string> = new Set(knownFields);
  const fieldErrors: Record<string, readonly string[]> = {};
  const unmatched: string[] = [];

  for (const [key, messages] of Object.entries(failure.fieldErrors ?? {})) {
    const field: string | undefined = [key, toCamelCase(key), toFoldedFieldName(key)].find(
      (candidate: string) => known.has(candidate),
    );

    if (field === undefined) {
      unmatched.push(...messages);
    } else {
      fieldErrors[field] = messages;
    }
  }

  return { fieldErrors, unmatched };
}

/** `DisplayName` → `displayName`; a camelCase key is unchanged. */
function toCamelCase(key: string): string {
  return key.charAt(FIRST_CHARACTER).toLowerCase() + key.slice(REST_OF_KEY);
}

/** `Branding.DisplayName` → `brandingDisplayName`; an undotted key folds to its camelCase. */
function toFoldedFieldName(key: string): string {
  return key
    .split(PATH_SEPARATOR)
    .map((segment: string, index: number) =>
      index === FIRST_CHARACTER
        ? toCamelCase(segment)
        : segment.charAt(FIRST_CHARACTER).toUpperCase() + segment.slice(REST_OF_KEY),
    )
    .join("");
}
