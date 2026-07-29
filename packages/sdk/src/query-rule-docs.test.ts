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
 * both the repo-root guardrail and the long-form guide must describe the
 * generated surface, and must not describe the deleted one.
 *
 * Precedent for pinning prose from this package: `bff-pattern-docs.test.ts`.
 */

// packages/sdk/src -> repo root
const repoRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
const rootGuardrailPath: string = resolve(repoRoot, "CLAUDE.md");
const frontendStateDocPath: string = resolve(repoRoot, "docs/development/frontend-state.md");

function readDoc(path: string): string {
  return readFileSync(path, "utf8");
}

/** The `### `-level section of the repo-root guardrail that states the rule. */
function frontendStateBoundarySection(): string {
  const sections: string[] = readDoc(rootGuardrailPath).split(/^#{2,3} /mu);
  const section: string | undefined = sections.find((candidate: string): boolean =>
    candidate.startsWith("Frontend state boundary"),
  );

  expect(section).toBeDefined();
  return section ?? "";
}

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

describe("the repo-root guardrail states the generated-key rule", () => {
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
