import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The app's three zones and the one-way graph between them (Wallow-1eb5).
 *
 * `src/` is `app/` (routes, router, entries, server-only modules), `features/<x>/`
 * (one directory per screen or flow) and `shared/` (what more than one feature
 * genuinely needs). The rule is a DAG: `app` may reach features and shared,
 * a feature may reach shared, shared may reach nothing but itself. Nothing
 * reaches back up, and no feature reaches sideways into another.
 *
 * **Why this is a spec and not an oxlint rule.** `no-restricted-imports` globs the
 * specifier STRING, and the rule here is about where a path RESOLVES: whether
 * `../../lib/thing` lands in your own zone or someone else's depends entirely on
 * which file wrote it. A glob that catches `../../shared/*` from a feature is
 * defeated by `../../../wallow-auth/src/shared/*`, and one strict enough to catch
 * both bans legitimate intra-zone imports. So the guard resolves every specifier
 * against its importer's real directory and judges the resulting edge.
 *
 * Cross-zone edges must be spelled as ALIASES (`@app/*`, `@features/<x>`,
 * `@shared/*`) rather than relative hops. That is not decoration: an alias makes
 * a boundary crossing visible in the import block, and it is what lets a module
 * move within its zone without a rewrite. Relative specifiers stay legal — and
 * required — WITHIN a zone.
 *
 * Node project — it reads files as text and mounts nothing.
 */

const srcDir: string = dirname(fileURLToPath(import.meta.url));

/** This file is the guard; it necessarily names the zones it polices. */
const SELF: string = relative(srcDir, fileURLToPath(import.meta.url));

/**
 * The DAG constrains the PRODUCT graph, not the test graph. A spec may import
 * anything its subject is composed with: nineteen feature specs import
 * `@app/routes/<name>` and mount the real route, because the component's contract
 * IS the route's `validateSearch` schema — testing it against a hand-rolled stub
 * would test the stub. Product modules get no such licence.
 *
 * Long-term exit: once a feature's search schema is barrel-exported (barrel
 * category 3), a spec can build its own route from it and this exemption shrinks.
 */
const SPEC_MAY_REACH_APP = /\.test\.tsx?$/u;

/**
 * Promotion into shared/ is a decision, not a reflex. A new top-level directory
 * here is a design change and should fail until it is one.
 *
 * `stores` is on this list only until Slice 4: wallow-web's `ui-store` is entirely
 * navigation state and moves into `packages/navigation` as `useNavStore`, at which
 * point `shared/stores/` is deleted and MUST come off this list too — an allowlist
 * that keeps naming a directory nothing uses drifts in the permissive direction.
 *
 * There is no `test` entry: `src/test/` merged into `shared/testing/`.
 */
const SHARED_SUBDIRS: ReadonlySet<string> = new Set([
  "components",
  "hooks",
  "lib",
  "stores",
  "testing",
  "types",
]);

/**
 * A zone name: `"app"`, `"shared"`, `"features/<name>"`, or `"root"` for a policy
 * spec sitting directly under `src/`.
 */
type Zone = string;

/** Where a specifier landed, once resolved. */
type Target =
  /** A workspace package or a bare module — `no-restricted-imports`' job, not this spec's. */
  | { readonly kind: "package" }
  /** A relative hop that resolved out of `src/` entirely. */
  | { readonly kind: "outside" }
  /** Somewhere inside `src/`, reached either relatively or by alias. */
  | { readonly kind: "zone"; readonly zone: Zone; readonly alias: boolean; readonly deep: boolean };

interface Edge {
  /** The importing file, relative to `src/`. */
  readonly file: string;
  readonly zone: Zone;
  readonly specifier: string;
  readonly target: Target;
}

/**
 * Every hand-written TypeScript module under `src/`, specs included.
 *
 * `withFileTypes` + `isFile()` matters: Vitest browser mode writes failure
 * screenshots into `src/**\/__screenshots__/<spec>.test.tsx/` directories, and a
 * name-only filter would hand `readFileSync` a directory. `routeTree.gen.ts` is
 * codegen — it imports every route relatively by construction, and is not a file
 * anyone can fix.
 */
function appSources(): readonly string[] {
  return readdirSync(srcDir, { recursive: true, withFileTypes: true })
    .filter(
      (entry): boolean =>
        entry.isFile() && (entry.name.endsWith(".ts") || entry.name.endsWith(".tsx")),
    )
    .map((entry): string => relative(srcDir, join(entry.parentPath, entry.name)))
    .filter((path): boolean => path !== SELF && !path.endsWith("routeTree.gen.ts"))
    .toSorted();
}

/** Source with comments removed, so prose naming a zone is not read as an import. */
function codeOf(relativePath: string): string {
  return readFileSync(join(srcDir, relativePath), "utf8")
    .replaceAll(/\/\*[\s\S]*?\*\//gu, "")
    .replaceAll(/^\s*\/\/.*$/gmu, "");
}

/**
 * Every module specifier a file pulls from: `import … from`, `export … from`,
 * bare side-effect imports, and DYNAMIC `import("…")`.
 *
 * The third pattern is the one that is easy to leave out. `await import("…")`
 * matches neither of the first two: there is no `from`, and `import(` is neither
 * line-anchored nor followed by whitespace. A dynamic import is exactly how a
 * module reaches something it may not reach at module scope — server-only code in
 * `app/` is reached that way on purpose — so a boundary guard blind to it has a
 * hole shaped like the violation it exists to catch.
 *
 * Template-literal and variable specifiers (`import(\`./${name}\`)`) stay out of
 * scope: they cannot be judged statically, and neither app has one. One appearing
 * is an escalation, not a reason to widen this.
 */
function moduleSpecifiers(relativePath: string): readonly string[] {
  const code: string = codeOf(relativePath);

  return [
    ...[...code.matchAll(/\bfrom\s+"([^"]+)"/gu)].map((match): string => match[1] as string),
    ...[...code.matchAll(/^\s*import\s+"([^"]+)"/gmu)].map((match): string => match[1] as string),
    ...[...code.matchAll(/\bimport\(\s*"([^"]+)"\s*\)/gu)].map(
      (match): string => match[1] as string,
    ),
  ];
}

/** The zone a `src/`-relative path belongs to. */
function zoneOf(srcRelativePath: string): Zone {
  const segments: readonly string[] = srcRelativePath.split("/");

  if (segments.length < 2) {
    return "root";
  }
  if (segments[0] === "features") {
    return `features/${segments[1] as string}`;
  }

  return segments[0] as string;
}

/** Classify one specifier as written by `file`. */
function targetOf(file: string, specifier: string): Target {
  if (specifier.startsWith("@app/") || specifier.startsWith("@shared/")) {
    return {
      kind: "zone",
      zone: specifier.slice(1).split("/")[0] as string,
      alias: true,
      deep: false,
    };
  }

  if (specifier.startsWith("@features/")) {
    const segments: readonly string[] = specifier.split("/");

    return {
      kind: "zone",
      zone: `features/${segments[1] as string}`,
      alias: true,
      // Barrel-only: `@features/login` is the contract, `@features/login/anything`
      // reaches around it.
      deep: segments.length > 2,
    };
  }

  if (!specifier.startsWith(".")) {
    return { kind: "package" };
  }

  const importerDir: string = join(srcDir, dirname(file));
  const resolved: string = relative(srcDir, resolve(importerDir, specifier));

  return resolved.startsWith("..")
    ? { kind: "outside" }
    : { kind: "zone", zone: zoneOf(resolved), alias: false, deep: false };
}

function edges(): readonly Edge[] {
  return appSources().flatMap((file): readonly Edge[] =>
    moduleSpecifiers(file).map(
      (specifier): Edge => ({
        file,
        zone: zoneOf(file),
        specifier,
        target: targetOf(file, specifier),
      }),
    ),
  );
}

/** `${file} -> ${specifier}`, the shape every failure below reports. */
function describeEdge(edge: Edge): string {
  return `${edge.file} -> ${edge.specifier}`;
}

const ALL: readonly Edge[] = edges();

/** Edges a rule may judge: root-level policy specs are outside the product graph. */
const PRODUCT: readonly Edge[] = ALL.filter((edge): boolean => edge.zone !== "root");

describe("the zone walk itself", () => {
  // A guard on the guard. Every rule below is a filter over `ALL`, so a walk that
  // found nothing — a renamed directory, a broken relativize — would report a
  // clean DAG rather than a broken scan.
  it("finds importers in all three zones", () => {
    const zones: ReadonlySet<Zone> = new Set(
      ALL.map((edge): string => (edge.zone.startsWith("features/") ? "features" : edge.zone)),
    );

    expect([...zones].toSorted()).toEqual(["app", "features", "root", "shared"]);
  });

  it("reads a substantial number of edges, not a handful", () => {
    expect(ALL.length).toBeGreaterThan(200);
  });
});

describe("the import DAG", () => {
  it("spells every cross-zone import as an alias, never a relative hop", () => {
    // The rule that makes the rest of this file enforceable. A relative specifier
    // is legal WITHIN a zone at any depth; the moment it lands in another zone it
    // has to say so, because a boundary crossing hidden in `../../` is a boundary
    // crossing nobody reviews.
    const violations: readonly string[] = PRODUCT.filter(
      (edge): boolean =>
        edge.target.kind === "zone" && !edge.target.alias && edge.target.zone !== edge.zone,
    ).map((edge): string => describeEdge(edge));

    expect(violations).toEqual([]);
  });

  it("reaches a feature only through its barrel", () => {
    const violations: readonly string[] = PRODUCT.filter(
      (edge): boolean => edge.target.kind === "zone" && edge.target.deep,
    ).map((edge): string => describeEdge(edge));

    expect(violations).toEqual([]);
  });

  it("keeps each feature out of every other feature", () => {
    const violations: readonly string[] = PRODUCT.filter(
      (edge): boolean =>
        edge.zone.startsWith("features/") &&
        edge.target.kind === "zone" &&
        edge.target.zone.startsWith("features/") &&
        edge.target.zone !== edge.zone,
    ).map((edge): string => describeEdge(edge));

    expect(violations).toEqual([]);
  });

  it("never lets a feature or shared/ reach back into app/", () => {
    // Except a spec, per SPEC_MAY_REACH_APP: mounting the real route is how a
    // screen's contract is tested.
    const violations: readonly string[] = PRODUCT.filter(
      (edge): boolean =>
        edge.target.kind === "zone" &&
        edge.target.zone === "app" &&
        edge.zone !== "app" &&
        !SPEC_MAY_REACH_APP.test(edge.file),
    ).map((edge): string => describeEdge(edge));

    expect(violations).toEqual([]);
  });

  it("never lets shared/ reach a feature", () => {
    // No exemption here, spec or not. `shared/` is what features are built FROM;
    // a shared module that knows a feature's name is a feature module in hiding.
    const violations: readonly string[] = PRODUCT.filter(
      (edge): boolean =>
        edge.zone === "shared" &&
        edge.target.kind === "zone" &&
        edge.target.zone.startsWith("features/"),
    ).map((edge): string => describeEdge(edge));

    expect(violations).toEqual([]);
  });

  it("keeps relative imports that escape src/ to root-level policy specs", () => {
    /**
     * A relative specifier that resolves OUTSIDE `src/` is legal only from a
     * root-level policy spec — those exist precisely to assert things about
     * `vite.config.ts` and the app manifest.
     */
    const violations: readonly string[] = PRODUCT.filter(
      (edge): boolean => edge.target.kind === "outside",
    ).map((edge): string => describeEdge(edge));

    expect(violations).toEqual([]);
  });
});

describe("shared/", () => {
  it("keeps shared/ to its sanctioned subdirectories", () => {
    const present: readonly string[] = readdirSync(join(srcDir, "shared"), { withFileTypes: true })
      .filter((entry): boolean => entry.isDirectory())
      .map((entry): string => entry.name)
      .toSorted();

    expect(present.filter((name): boolean => !SHARED_SUBDIRS.has(name))).toEqual([]);
  });

  it("has subdirectories at all, so the allowlist is not policing an empty tree", () => {
    expect(
      readdirSync(join(srcDir, "shared"), { withFileTypes: true }).filter((entry): boolean =>
        entry.isDirectory(),
      ).length,
    ).toBeGreaterThan(0);
  });
});
