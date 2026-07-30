import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Guard for Wallow-jtdg: `docs/toc.yml` must be the ONE toc emitted at the site root, and every
 * entry in it must survive into the built site.
 *
 * The bug this pins was invisible: two `docfx.json` build.content entries both emitted `toc.json`
 * to the site root, docfx resolved the collision by an undefined tie-break, and the build still
 * exited 0 whichever side won. Measured on this tree, `docs/toc.yml` wins — so the sidebar renders
 * by luck, not by construction. These assertions turn that luck into a contract.
 *
 * Split in two on purpose:
 *  - The CONFIG assertion always runs. It is the one that fails fast in `pnpm test` if a second
 *    root-toc emitter is ever added back.
 *  - The BUILT-SITE assertions are `skipIf`-gated on the artifact existing, matching how
 *    `packages/forms/src/index.test.ts` gates its `dist/` assertions. They arm after
 *    `dotnet docfx build docfx.json` and in CI, which builds the site.
 */

// packages/sdk/src -> repo root
const repoRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
const docfxConfigPath: string = resolve(repoRoot, "docfx.json");
const tocSourcePath: string = resolve(repoRoot, "docs/toc.yml");
const builtTocPath: string = resolve(repoRoot, ".docfx/_site/toc.json");

interface DocfxContentEntry {
  files: string[];
  src?: string;
  dest?: string;
  exclude?: string[];
}

interface DocfxConfig {
  build: { content: DocfxContentEntry[]; dest: string };
}

function readDocfxConfig(): DocfxConfig {
  return JSON.parse(readFileSync(docfxConfigPath, "utf8")) as DocfxConfig;
}

/** Every `href: some/path.md` in docs/toc.yml, in source order. */
function readSourceHrefs(): string[] {
  const source: string = readFileSync(tocSourcePath, "utf8");
  return [...source.matchAll(/^\s*href:\s*(\S+\.md)\s*$/gmu)].map((match) => match[1]);
}

/** Every `href` anywhere in the built toc.json tree, flattened. */
function readBuiltHrefs(): string[] {
  interface TocNode {
    href?: string;
    items?: TocNode[];
  }
  const built: TocNode = JSON.parse(readFileSync(builtTocPath, "utf8")) as TocNode;
  const collected: string[] = [];
  const walk = (node: TocNode): void => {
    if (node.href !== undefined) {
      collected.push(node.href);
    }
    for (const child of node.items ?? []) {
      walk(child);
    }
  };
  walk(built);
  return collected;
}

describe("docfx toc configuration", () => {
  it("emits exactly one toc at the site root", () => {
    // An entry emits a root toc when it ships a toc.yml AND its dest is the site root
    // (`.` or absent). Two such entries collide on toc.json and docfx silently drops one.
    const rootTocEmitters: DocfxContentEntry[] = readDocfxConfig().build.content.filter(
      (entry) =>
        entry.files.some((pattern) => pattern.endsWith("toc.yml")) &&
        (entry.dest === undefined || entry.dest === "."),
    );

    expect(
      rootTocEmitters.map((entry) => entry.src ?? "<repo root>"),
      "exactly one build.content entry may emit toc.json at the site root; a second one collides " +
        "and docfx picks a winner by an undefined rule (Wallow-jtdg)",
    ).toEqual(["docs"]);
  });

  it("lists every source href with a .md extension", () => {
    expect(readSourceHrefs().length).toBeGreaterThan(30);
  });
});

const builtSiteIsMissing: boolean = !existsSync(builtTocPath);

describe.skipIf(builtSiteIsMissing)("built docs toc", () => {
  it("carries every entry from docs/toc.yml", () => {
    const built: Set<string> = new Set(readBuiltHrefs());
    const missing: string[] = readSourceHrefs()
      .map((href) => href.replace(/\.md$/u, ".html"))
      .filter((href) => !built.has(href));

    expect(
      missing,
      "every docs/toc.yml entry must survive the build; a missing one means the site-root toc " +
        "collision came back (Wallow-jtdg)",
    ).toEqual([]);
  });

  it("reaches the generated API reference", () => {
    // Match a GENERATED page specifically (`api/Wallow.<Namespace>.html`), not merely anything
    // under `api/` — the hand-written `api/service-accounts.html` lives there too and would
    // satisfy a bare `startsWith("api/")` while the entire generated reference was missing,
    // which is exactly the state this assertion exists to catch (Wallow-jtdg defect 2).
    const generated: string[] = readBuiltHrefs().filter((href) =>
      /^api\/Wallow\.[\w.]+\.html$/u.test(href),
    );

    expect(
      generated.length,
      "the generated .NET API reference must be reachable from the sidebar; docfx emits it from " +
        "the metadata block into api/ and docs/toc.yml includes ../.docfx/api/toc.yml",
    ).toBeGreaterThan(100);
  });
});
