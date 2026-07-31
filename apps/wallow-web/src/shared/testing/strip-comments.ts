/**
 * The comment stripper the source-reading guard specs judge through
 * (Wallow-lrlm.14).
 *
 * Every sweep in this app that reads a file as TEXT — the two typography sweeps
 * above all — asserts against the file with its comments removed, so prose that
 * merely mentions `<h1>` or `text-foreground/60` does not read as a violation.
 * That makes the stripper the thing deciding what those sweeps are allowed to
 * see, and its contract is one sentence: **comments are removed and nothing else
 * is.**
 *
 * Over-deletion is the dangerous direction. A stripper that eats real source
 * turns a sweep green over code it never read, which is indistinguishable from
 * an app that is genuinely clean; under-deletion only produces a noisy red,
 * which fixes itself. So every ambiguous call below resolves toward keeping
 * text.
 *
 * The two-pass regex this replaces (block comments, then line comments) could
 * not hold that contract, and no regex can: recognising a comment requires
 * knowing you are not inside a string, a template literal or a regex literal,
 * which is state a `replaceAll` has no way to carry. `// /v1/**` in a line
 * comment opened a block comment that ran to the file's next block-comment
 * close; `"https://x"` in an `href` truncated the rest of its line — in JSX,
 * routinely a whole element. So this is a left-to-right scan instead.
 *
 * It is NOT a JavaScript parser and must not grow into one. It recognises the
 * handful of constructs a comment delimiter can hide inside, and nothing else.
 */

/** What `String.prototype.indexOf` answers when there is no next occurrence. */
const NOT_FOUND = -1;

/** `/*` and its closer are two characters each. */
const DELIMITER_LENGTH = 2;

/** A backslash and the character it escapes. */
const ESCAPE_LENGTH = 2;

/** The quotes that open a literal a comment delimiter can hide inside. */
const QUOTES: ReadonlySet<string> = new Set(['"', "'", "`"]);

/**
 * The tokens after which a `/` opens a REGEX literal rather than dividing —
 * matched against the code emitted so far, so it anchors at the end.
 *
 * `<`, `>`, `{` and `}` are deliberately absent from the punctuation. In `.tsx`
 * they are almost always JSX, and reading `</p>`, `/>` or `{/` as the start of a
 * regex would let the scan run past a real comment — the over-deletion this file
 * exists to prevent, arriving by a different route.
 */
const REGEX_MAY_FOLLOW =
  /(?:[(,=:[!&|?;+\-*%^~]|\b(?:await|case|delete|do|else|in|of|return|typeof|void|yield))$/u;

/** Enough trailing context to see the last token before a `/`, indentation included. */
const LOOKBEHIND = 32;

/** Does a `/` appearing after the text already emitted open a regex literal? */
function opensRegex(emitted: string): boolean {
  return REGEX_MAY_FOLLOW.test(emitted.slice(-LOOKBEHIND).trimEnd());
}

/** The index just past the line comment opening at `start` — its newline, or the end. */
function endOfLineComment(source: string, start: number): number {
  const newline: number = source.indexOf("\n", start);

  return newline === NOT_FOUND ? source.length : newline;
}

/** The index just past the block comment opening at `start`, unterminated ones included. */
function endOfBlockComment(source: string, start: number): number {
  const closer: number = source.indexOf("*/", start + DELIMITER_LENGTH);

  return closer === NOT_FOUND ? source.length : closer + DELIMITER_LENGTH;
}

/**
 * The index just past the string or template literal opening at `start`.
 *
 * `'` and `"` end at their line's newline whether or not they were closed: a
 * quoted string cannot span a line, and bounding them is what keeps an
 * apostrophe in JSX prose (`Don't`) from suppressing comment detection for the
 * rest of the file. A template literal may span lines, so it is bounded only by
 * its backtick.
 */
function endOfQuoted(source: string, start: number): number {
  const quote: string = source.charAt(start);
  const multiline: boolean = quote === "`";
  let index: number = start + 1;

  while (index < source.length) {
    const char: string = source.charAt(index);

    if (char === "\\") {
      index += ESCAPE_LENGTH;
    } else if (char === quote) {
      return index + 1;
    } else if (char === "\n" && !multiline) {
      return index;
    } else {
      index += 1;
    }
  }

  return source.length;
}

/**
 * The index just past the regex literal opening at `start`, which is its first
 * unescaped `/` outside a character class or, failing that, the end of its line
 * — a regex literal cannot span one.
 */
function endOfRegexLiteral(source: string, start: number): number {
  let index: number = start + 1;
  let inCharacterClass = false;

  while (index < source.length) {
    const char: string = source.charAt(index);

    if (char === "\n") {
      return index;
    }
    if (char === "\\") {
      index += ESCAPE_LENGTH;
    } else {
      if (char === "/" && !inCharacterClass) {
        return index + 1;
      }
      inCharacterClass = char === "[" || (inCharacterClass && char !== "]");
      index += 1;
    }
  }

  return source.length;
}

/**
 * The index just past the literal opening at `index`, or simply the next
 * character when nothing opens there. `emitted` is the code kept so far, which
 * is what decides whether a `/` divides or opens a regex.
 */
function endOfLiteral(source: string, index: number, emitted: string): number {
  const char: string = source.charAt(index);

  if (QUOTES.has(char)) {
    return endOfQuoted(source, index);
  }
  if (char === "/" && opensRegex(emitted)) {
    return endOfRegexLiteral(source, index);
  }

  return index + 1;
}

/**
 * `source` with its `//` and block comments removed, and every other character
 * kept byte for byte.
 *
 * Recognised as NOT a comment delimiter: a delimiter inside a single- or
 * double-quoted string, inside a template literal, or inside a regex literal;
 * and `//` immediately preceded by `:`, which is a URL scheme (`https://…`)
 * rather than a comment — the one construct that carries no quoting at all when
 * it appears as JSX text.
 */
export function stripComments(source: string): string {
  let output = "";
  let index = 0;

  while (index < source.length) {
    const char: string = source.charAt(index);
    const next: string = source.charAt(index + 1);

    if (char === "/" && next === "/" && source.charAt(index - 1) !== ":") {
      index = endOfLineComment(source, index);
    } else if (char === "/" && next === "*") {
      index = endOfBlockComment(source, index);
    } else {
      const end: number = endOfLiteral(source, index, output);

      output += source.slice(index, end);
      index = end;
    }
  }

  return output;
}
