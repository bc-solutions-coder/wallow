import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Spec (Wallow-pu6a.5.2, D7): the query-key RULE is rewritten in the same change
 * that adopts the generated keys.
 *
 * The old rule ("every key comes from `@bc-solutions-coder/sdk/query`'s
 * `queryKeys` registry", built parent-from-child so an `invalidateQueries` on a
 * parent sweeps the subtree) described a hand-written hierarchical factory
 * — `packages/sdk/src/query/keys.ts` — that hey-api does not produce. Its keys
 * are flat `[{ _id, baseUrl, path, tags }]` objects. Leaving the old wording in
 * place would send a reader looking for a prefix builder that no longer exists,
 * so these are documentation contracts with the same standing as the code:
 * both the apps guardrail and the long-form guide must describe the
 * generated surface, and must not describe the deleted one.
 *
 * Precedent for pinning prose from this package: `bff-pattern-docs.test.ts`.
 *
 * Second spec (Wallow-x4qn.13): the same docs now also carry the query-FACADE
 * rule and the shared auth package. react-query enters the workspace in exactly
 * one place — `@bc-solutions-coder/query` — and auth state in exactly one other
 * — `@bc-solutions-coder/auth`. Both are invisible to a reader following prose
 * that still shows `useQueryClient` coming from `@tanstack/react-query` or a
 * hand-rolled current-user `queryOptions` no app has, and a copy-pasteable
 * example importing the raw package is now a LINT failure as well as wrong. The
 * import side is enforced by oxlint and by each consuming package's own facade
 * spec; neither can see prose, which is what these pins are for.
 */

// packages/sdk/src -> repo root
const repoRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
const rootGuardrailPath: string = resolve(repoRoot, "CLAUDE.md");
const appsGuardrailPath: string = resolve(repoRoot, "apps/CLAUDE.md");
const frontendStateDocPath: string = resolve(repoRoot, "docs/development/frontend-state.md");
const formsDocPath: string = resolve(repoRoot, "docs/development/forms.md");
const formsGuardrailPath: string = resolve(repoRoot, "packages/forms/CLAUDE.md");

/** The one package that may declare and import react-query. */
const FACADE: string = "@bc-solutions-coder/query";

/** The package the facade re-exports, which no doc may show being imported. */
const RAW_QUERY: string = "@tanstack/react-query";

/** The one package an app reads auth state from. */
const AUTH_PACKAGE: string = "@bc-solutions-coder/auth";

function readDoc(path: string): string {
  return readFileSync(path, "utf8");
}

/** Line breaks erased, so a pinned sentence may wrap wherever prose wraps. */
function collapse(text: string): string {
  return text.replaceAll(/\s+/gu, " ");
}

/**
 * The `##`/`###`/`####`-level section of `path` whose heading starts with
 * `heading`, or `""` when the doc has no such section (which fails the caller).
 */
function section(path: string, heading: string): string {
  const sections: string[] = readDoc(path).split(/^#{2,4} /mu);
  const found: string | undefined = sections.find((candidate: string): boolean =>
    candidate.startsWith(heading),
  );

  expect(found, `${path} has no "${heading}" section`).toBeDefined();
  return found ?? "";
}

/**
 * The section of the apps guardrail that states the rule.
 *
 * It sits in `apps/CLAUDE.md` rather than the repo root because only an agent
 * working under `apps/` needs it, and the root file is paid for by every session
 * and every subagent. The pin follows the audience, not the path.
 */
function frontendStateBoundarySection(): string {
  return section(appsGuardrailPath, "Frontend state boundary");
}

/**
 * The lines inside ``` fences — the copy-pasteable half of a doc.
 *
 * The import sweeps below read ONLY these. Prose legitimately names the raw
 * package to forbid it ("never `@tanstack/react-query` directly"), and a
 * text-level search for the specifier would fail on the very sentence it wants.
 */
function codeBlockLines(path: string): string[] {
  const collected: string[] = [];
  let inFence: boolean = false;

  for (const line of readDoc(path).split("\n")) {
    if (line.trimStart().startsWith("```")) {
      inFence = !inFence;
    } else if (inFence) {
      collected.push(line);
    }
  }

  return collected;
}

/**
 * `from "x"` / `import "x"` / `require("x")` naming {@link RAW_QUERY} — a
 * subpath (`@tanstack/react-query/build/…`) is the same package and counts.
 */
const RAW_QUERY_IMPORT: RegExp =
  /(?:\bfrom|^\s*import|\brequire\s*\()\s*["'`]@tanstack\/react-query/u;

/** The prose sentence a facade rule has to state, wherever it wraps. */
const FACADE_ONLY_RULE: RegExp = /never\s+(?:from\s+)?`?@tanstack\/react-query/iu;

/**
 * Wording that only makes sense for the deleted hand-written factory: a named
 * registry/factory, its source file, and member access into its nested shape
 * (`queryKeys.organizations.all`).
 */
const HIERARCHICAL_FACTORY_LANGUAGE: readonly RegExp[] = [
  /queryKeys[`"']?\s+(factory|registry)/iu,
  /query\/keys\.ts/u,
  /queryKeys\s*[.=]/u,
];

describe("the apps guardrail states the generated-key rule", () => {
  it("no longer points at the hand-written queryKeys factory", () => {
    const section: string = frontendStateBoundarySection();

    for (const pattern of HIERARCHICAL_FACTORY_LANGUAGE) {
      expect(section).not.toMatch(pattern);
    }
  });

  it("says keys come from the generated per-operation artifacts", () => {
    const section: string = frontendStateBoundarySection();

    expect(section).toMatch(/generated/iu);
    expect(section).toMatch(/Options\(/u);
  });

  it("routes invalidation through the curated invalidations module", () => {
    expect(frontendStateBoundarySection()).toMatch(/invalidations/iu);
  });

  it("keeps the no-inline-key-literals rule", () => {
    expect(frontendStateBoundarySection()).toMatch(/inline\s+(query\s+)?key/iu);
  });
});

describe("docs/development/frontend-state.md describes the generated query layer", () => {
  it("no longer documents the hand-written queryKeys factory", () => {
    const doc: string = readDoc(frontendStateDocPath);

    for (const pattern of HIERARCHICAL_FACTORY_LANGUAGE) {
      expect(doc).not.toMatch(pattern);
    }
  });

  it("no longer documents the retired bootstrap/unwrap query authoring steps", () => {
    const doc: string = readDoc(frontendStateDocPath);

    // Both belong to the hand-written slices task 5.5 deletes: a generated
    // `queryFn` calls the operation with an explicit `{ client }` instead.
    expect(doc).not.toContain("ensureQueryBootstrapped");
    expect(doc).not.toContain("unwrap(");
  });

  it("names the generated options factory and key builder a reader will import", () => {
    const doc: string = readDoc(frontendStateDocPath);

    expect(doc).toMatch(/Options\(/u);
    expect(doc).toMatch(/QueryKey/u);
    expect(doc).toContain("@bc-solutions-coder/sdk/query");
  });

  it("explains invalidation as a sweep over the flat generated keys", () => {
    const doc: string = readDoc(frontendStateDocPath);

    expect(doc).toMatch(/invalidations/iu);
    // The flat key is the reason the rule changed at all; a reader who does not
    // learn the shape cannot write a correct sweep.
    expect(doc).toMatch(/_id/u);
    expect(doc).toMatch(/tags/u);
  });

  it("keeps the no-inline-key-literals rule", () => {
    expect(readDoc(frontendStateDocPath)).toMatch(/inline\s+(`?queryKey`?|query\s+key)/iu);
  });
});

describe("the apps guardrail states the query-facade rule", () => {
  it("names the facade as where react-query symbols come from", () => {
    const boundary: string = frontendStateBoundarySection();

    expect(boundary, `the section never names ${FACADE}`).toContain(FACADE);
    // Naming the facade is not enough: a reader who already imports the raw
    // package has to be told to stop. Wording is free, the claim is not — the
    // sentence must read "…, never `@tanstack/react-query` (directly)".
    expect(collapse(boundary)).toMatch(FACADE_ONLY_RULE);
  });

  it("names the react-query symbols the rule covers, so the reader recognises theirs", () => {
    const boundary: string = collapse(frontendStateBoundarySection());

    expect(boundary).toMatch(/useQuery/u);
    expect(boundary).toMatch(/useMutation/u);
    expect(boundary).toMatch(/QueryClient/u);
  });

  it("says the facade rule is machine-enforced rather than a convention", () => {
    // A reader who thinks this is style will discover it as a CI failure. The
    // oxlint `no-restricted-imports` rule is bead Wallow-x4qn.12's half.
    expect(collapse(frontendStateBoundarySection())).toMatch(/lint/iu);
  });

  it("names the auth package as the source of auth state", () => {
    const boundary: string = frontendStateBoundarySection();

    expect(boundary, `the section never names ${AUTH_PACKAGE}`).toContain(AUTH_PACKAGE);
    // The three things that package owns, and the three a reader would otherwise
    // rebuild per app: who is signed in, what they may do, and route gating.
    expect(collapse(boundary)).toMatch(/current[- ]user/iu);
    expect(collapse(boundary)).toMatch(/role|permission/iu);
  });
});

describe("docs/development/frontend-state.md documents the query facade", () => {
  it("states the facade-only rule in full", () => {
    const doc: string = readDoc(frontendStateDocPath);

    expect(doc, `the guide never names ${FACADE}`).toContain(FACADE);
    expect(collapse(doc)).toMatch(FACADE_ONLY_RULE);
  });

  it("routes the add-a-query checklist through the facade and the feature api.ts seam", () => {
    // The checklist is what a reader actually follows, and its samples currently
    // import the hooks from nowhere at all — leaving the raw package as the
    // reader's obvious guess. It also has to name the `features/<name>/api.ts`
    // seam the apps re-export the generated artifacts through, not only mention
    // it in a trailing "See also" link.
    const checklist: string = section(frontendStateDocPath, "How to add a query");

    expect(checklist).toContain(FACADE);
    expect(checklist).toMatch(/api\.ts/u);
  });
});

describe("docs/development/frontend-state.md documents the shared auth package", () => {
  /** The `### `-level section holding the canonical current-user snippet. */
  function currentUserSection(): string {
    return section(frontendStateDocPath, "The current user query");
  }

  it("points the current-user snippet at currentUserQuery from the auth package", () => {
    const currentUser: string = currentUserSection();

    expect(currentUser).toContain(AUTH_PACKAGE);
    expect(currentUser).toContain("currentUserQuery");
  });

  it("no longer shows a hand-rolled queryOptions wrapper for the current user", () => {
    // The snippet spread `usersGetCurrentUserOptions` into a local
    // `queryOptions` call — a shape no app ever ran, and one that now duplicates
    // `currentUserQuery`. Two definitions of "who is signed in" is the exact
    // drift packages/auth exists to end, so the stale one must not survive as a
    // copy-pasteable example.
    const currentUser: string = currentUserSection();

    expect(currentUser).not.toContain("usersGetCurrentUserOptions");
    expect(currentUser).not.toMatch(/queryOptions\s*\(/u);
  });

  it("keeps the 30-second staleTime rationale and documents the 401-to-null softening", () => {
    // Both are behavioural contracts of `currentUserQuery` that its callers
    // depend on: the staleTime is why a `beforeLoad` on every navigation does not
    // refetch, and the softening is why an anonymous visitor reaches a login gate
    // instead of an error boundary.
    const currentUser: string = collapse(currentUserSection());

    expect(currentUser).toMatch(/staleTime/u);
    expect(currentUser).toMatch(/30/u);
    expect(currentUser).toMatch(/401/u);
    expect(currentUser).toMatch(/null|anonymous/iu);
  });
});

describe("docs/development/forms.md keeps its samples runnable under the facade rule", () => {
  it("takes useQueryClient from the facade", () => {
    // The post-submit invalidation example is the one every form author copies.
    const facadeImports: string[] = codeBlockLines(formsDocPath).filter((line: string): boolean =>
      line.includes(FACADE),
    );

    expect(
      facadeImports.filter((line: string): boolean => line.includes("useQueryClient")),
      `no sample imports useQueryClient from ${FACADE}`,
    ).not.toEqual([]);
  });
});

describe("no doc shows react-query being imported directly", () => {
  const docs: readonly string[] = [
    rootGuardrailPath,
    appsGuardrailPath,
    frontendStateDocPath,
    formsDocPath,
    formsGuardrailPath,
  ];

  it.each(docs)("%s imports react-query in no code sample", (path: string): void => {
    // Sweeps code fences only — prose has to be free to NAME the package in
    // order to forbid it.
    const offenders: string[] = codeBlockLines(path).filter((line: string): boolean =>
      RAW_QUERY_IMPORT.test(line),
    );

    expect(offenders, `a sample still imports ${RAW_QUERY} directly`).toEqual([]);
  });
});

/*
 * The optionality claim below is a query/auth-consolidation fact, which is why it
 * is pinned here rather than left to whoever next edits apps/CLAUDE.md: the
 * workspace grew from five shared packages to seven in this epic, and
 * examples/minimal-app adopted only five of them. It renders no form and has no
 * signed-in user, so it declares neither `forms` NOR `auth`.
 */
describe("apps/CLAUDE.md states which shared packages an app may omit", () => {
  it("lists the full seven-package set including query and auth", () => {
    expect(collapse(readDoc(appsGuardrailPath))).toMatch(
      /`sdk`.*`styles`.*`ui`.*`forms`.*`query`.*`auth`.*`testing`/u,
    );
  });

  it("names both optional packages, not one", () => {
    const sentences: string[] = collapse(readDoc(appsGuardrailPath)).split(/(?<=[.!?])\s/u);
    const optionality: string[] = sentences.filter((sentence: string): boolean =>
      /optional/iu.test(sentence),
    );

    expect(optionality, "apps/CLAUDE.md says nothing about optional packages").not.toEqual([]);
    // A sentence claiming a single optional package ("forms is the only optional
    // one") would be false guidance: minimal-app's manifest declares 5 of 7.
    expect(optionality.filter((sentence: string): boolean => /only/iu.test(sentence))).toEqual([]);
    expect(
      optionality.some(
        (sentence: string): boolean => /forms/u.test(sentence) && /auth/u.test(sentence),
      ),
      "no sentence names forms AND auth as the optional ones",
    ).toBe(true);
  });
});
