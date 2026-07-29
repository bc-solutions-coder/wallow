import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Structural guard for Wallow-evd5.3.2: the MfaEnroll screen starts enrollment
 * through a `useMutation` instead of the hand-rolled
 * `useEffect` + `useState` + `try/catch` trio.
 *
 * WHY THIS SPEC IS SOURCE-READING RATHER THAN BEHAVIOURAL. The sibling
 * `MfaEnrollForm.test.tsx` already pins every observable behaviour of this
 * screen across ~57 browser-mode tests, and this bead is a faithful refactor —
 * so by construction there is no behaviour that changes, and no behavioural
 * assertion can go red. The acceptance criterion itself is structural ("uses
 * useMutation instead of useEffect+useState+try/catch for enrollment start"),
 * so it is asserted structurally. Source-reading node specs are an established
 * idiom in this app (`vitest-browser-project-split.test.ts`).
 *
 * The behavioural net for the refactor is the existing component spec plus
 * `MfaEnrollForm.start-pending.test.tsx`; this file only pins the SHAPE.
 *
 * ── SCOPE: ONE OF THE TWO CALLS IN THAT EFFECT ────────────────────────────────
 *
 * The mount effect fires TWO calls, and only one of them is in scope.
 * `mfaExchangeEnrollmentToken` must still run — and still run BEFORE the
 * enrollment start, because the exchange is what mints the
 * `Identity.MfaPartial` cookie that `mfaEnrollTotp` needs to authenticate — so its
 * `try/catch` and the effect that sequences it both survive this refactor. The
 * assertions below are therefore scoped to the `mfaEnrollTotp` call specifically,
 * never to "no try/catch anywhere" or "no useEffect".
 *
 * There is also a PRE-EXISTING `useMutation` in this file wrapping
 * `mfaConfirmEnrollment` — a different, already-idiomatic call. It is pinned here
 * so a refactor cannot satisfy the criterion by repurposing the wrong mutation.
 */

const componentPath: string = fileURLToPath(new URL("./MfaEnrollForm.tsx", import.meta.url));

/**
 * The file with its comments removed. This screen carries an unusually long
 * prose header that NAMES the very identifiers under test — `mfaEnrollTotp()`,
 * `try`, `retry` — so matching against the raw text reports the documentation
 * instead of the code. Line-comment stripping is safe here only because no
 * string literal in this file contains `//`; revisit if one ever does.
 */
const source: string = readFileSync(componentPath, "utf8")
  .replaceAll(/\/\*[\s\S]*?\*\//g, "")
  .replaceAll(/\/\/[^\n]*/g, "");

/**
 * Index of the `}` closing the block opened at `openIndex`. Naive brace counting
 * (it does not skip string literals), which is sound on the comment-stripped
 * source because every remaining brace is balanced.
 */
function matchingBrace(text: string, openIndex: number): number {
  let depth = 0;

  for (let i = openIndex; i < text.length; i += 1) {
    if (text[i] === "{") {
      depth += 1;
    } else if (text[i] === "}") {
      depth -= 1;

      if (depth === 0) {
        return i;
      }
    }
  }

  return -1;
}

/**
 * Every block introduced by `opener`. The pattern must be anchored on a word
 * boundary: a bare `"try"` search also matches the `try` inside `retry`, which
 * would attribute an unrelated block to a try/catch that is not there.
 */
function blocksOpenedBy(opener: RegExp): string[] {
  const blocks: string[] = [];

  for (const match of source.matchAll(opener)) {
    const from: number = match.index + match[0].length - 1;
    const open: number = source.indexOf("{", from);
    const close: number = open === -1 ? -1 : matchingBrace(source, open);

    if (open !== -1 && close !== -1) {
      blocks.push(source.slice(open, close + 1));
    }
  }

  return blocks;
}

/** The options object passed to each `useMutation(...)` call. */
function mutationBlocks(): string[] {
  return blocksOpenedBy(/\buseMutation\s*\(\s*\{/g);
}

/** The body of each `try { ... }` still standing in the file. */
function tryBlocks(): string[] {
  return blocksOpenedBy(/\btry\s*\{/g);
}

describe("MfaEnrollForm — enrollment start is a mutation", () => {
  it("calls mfaEnrollTotp from a useMutation", () => {
    // The criterion itself: the enrollment-start request is issued by TanStack
    // Query, so pending and error state have one owner instead of two
    // hand-rolled useState pairs that can drift out of step.
    const owning: string[] = mutationBlocks().filter((block) => block.includes("mfaEnrollTotp"));

    expect(owning).toHaveLength(1);
  });

  it("keeps the confirm call on its own separate mutation", () => {
    // Guard against satisfying the criterion by folding the start call into the
    // PRE-EXISTING confirm mutation. They are different endpoints with different
    // lifetimes: the start fires once on mount, the confirm fires per submit and
    // is retried in place after a rejected code.
    const blocks: string[] = mutationBlocks();
    const confirming: string[] = blocks.filter((block) => block.includes("mfaConfirmEnrollment"));

    expect(confirming).toHaveLength(1);
    expect(confirming[0]).not.toContain("mfaEnrollTotp");
  });

  it("does not wrap the enrollment start in a try/catch", () => {
    // The mutation owns rejection now — `startFailureMessage` is applied to the
    // mutation's error, not inside a catch. The exchange-token try/catch is out
    // of scope and deliberately not asserted against.
    const wrapping: string[] = tryBlocks().filter((block) => block.includes("mfaEnrollTotp"));

    expect(wrapping).toEqual([]);
  });

  it("leaves the token exchange outside the mutation", () => {
    // The scope line. Only the enrollment START folds into a mutation; the
    // exchange keeps its place in the mount effect because its ordering AHEAD of
    // the start is load-bearing — the exchange is what mints the
    // `Identity.MfaPartial` cookie, and an `mfaEnrollTotp` fired first has no
    // session to resolve and 401s, blaming the wrong thing.
    //
    // That ordering is a RUNTIME property and is pinned as one, by the sibling
    // spec's "exchanges the token for a session BEFORE asking for a secret"
    // (which records real call order across the two spies). It deliberately is
    // not re-asserted here from text position, which says nothing about
    // execution order once a mutationFn is declared above the effect that fires
    // it.
    expect(source).toContain("mfaExchangeEnrollmentToken(");
    expect(
      mutationBlocks().filter((block) => block.includes("mfaExchangeEnrollmentToken")),
    ).toEqual([]);
  });
});

describe("MfaEnrollForm — the hand-rolled start state is gone", () => {
  it("holds no useState for the enrollment pending flag", () => {
    // `loading` is `mutation.isPending` now. Two sources of truth for "a request
    // is in flight" is exactly what this refactor exists to delete.
    expect(source).not.toContain("setLoading");
  });

  it("holds no useState for the secret or the qr uri", () => {
    // Per the bead design, the one-time secret lives in MUTATION state — never a
    // query (it is not cacheable, not refetchable, and a second fetch would mint
    // a second secret and invalidate the QR the user already scanned).
    expect(source).not.toContain("setSecret");
    expect(source).not.toContain("setQrUri");
  });

  it("no longer routes the start through a useCallback", () => {
    // `startEnroll` was a `useCallback` shared by the mount effect and the retry
    // button. Both call sites become `mutate()`, so the callback — and the
    // `useCallback` import that existed only for it — go away.
    expect(source).not.toContain("useCallback");
  });
});
