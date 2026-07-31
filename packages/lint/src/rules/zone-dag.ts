import { existsSync, readFileSync } from "node:fs";
import { dirname, relative, resolve } from "node:path";

import { defineRule, type ESTree } from "@oxlint/plugins";

/**
 * The app's three zones and the one-way graph between them.
 *
 * `src/` is `app/` (routes, router, entries, server-only modules), `features/<x>/` (one
 * directory per screen or flow) and `shared/` (what more than one feature genuinely
 * needs). The rule is a DAG: `app` may reach features and shared, a feature may reach
 * shared, shared may reach nothing but itself. Nothing reaches back up, and no feature
 * reaches sideways into another.
 *
 * Cross-zone edges must be spelled as ALIASES rather than relative hops. That is not
 * decoration: an alias makes a boundary crossing visible in the import block, and it is
 * what lets a module move within its zone without a rewrite. Relative specifiers stay
 * legal — and required — WITHIN a zone.
 *
 * `no-restricted-imports` cannot state any of this, because it globs the specifier
 * STRING and the rule here is about where a path RESOLVES: whether `../../lib/thing`
 * lands in your own zone or someone else's depends entirely on which file wrote it. A
 * glob that catches `../../shared/*` from a feature is defeated by
 * `../../../wallow-auth/src/shared/*`, and one strict enough to catch both bans
 * legitimate intra-zone imports. So this resolves every specifier against its importer's
 * real directory and judges the resulting edge.
 *
 * The zones are READ from the app's `tsconfig.json` `paths` rather than named here, so
 * the list this polices and the list Vite and vitest resolve are one list, and a fourth
 * zone is policed from the moment it exists. That derivation is also what lets one rule
 * serve both apps with no options at all — nothing here names `src`, a zone, or an app.
 */

/** Zones whose members are BARREL-ONLY, as `{ barrelZones: [...] }`. */
const DEFAULT_BARREL_ZONES: readonly string[] = ["features"];

/**
 * The DAG constrains the PRODUCT graph, not the test graph. A spec may import `@app/*`,
 * because mounting the real route is how a screen's contract is tested — the component's
 * contract IS the route's `validateSearch` schema, and testing it against a hand-rolled
 * stub would test the stub.
 *
 * This is a per-check exemption rather than a config override for a reason: an override
 * turning the rule off for `**\/*.test.tsx` would exempt specs from the other five checks
 * too, a widening nothing ever granted. Every other edge is judged in a spec exactly as
 * it is in a product module.
 */
const SPEC = /\.test\.[cm]?[jt]sx?$/u;

/** A file directly under `src/` — a policy spec — sits outside the product graph. */
const ROOT_ZONE = "root";

/** `@features/login` is two segments; anything longer reached around the barrel. */
const BARREL_SEGMENTS = 2;

/** An app's zone declaration, derived from one `tsconfig.json`. */
interface App {
  /** The directory the zone directories share — `<root>/src`. */
  readonly srcDir: string;
  /** `["@app", "@features", "@shared"]`, longest first so no prefix shadows another. */
  readonly aliases: readonly string[];
}

/** Where a specifier landed, once resolved. */
type Target =
  /** A workspace package or bare module — `no-restricted-imports`' job, not this rule's. */
  | { readonly kind: "package" }
  /** A relative hop that resolved out of `src/` entirely. */
  | { readonly kind: "outside" }
  /** Somewhere inside `src/`, reached either relatively or by alias. */
  | {
      readonly kind: "zone";
      readonly zone: string;
      readonly alias: boolean;
      readonly deep: boolean;
    };

/** The longest directory prefix every path in `paths` shares. */
function commonDirectory(paths: readonly string[]): string {
  const split = paths.map((path): string[] => path.split("/"));
  const [first = []] = split;
  const shared: string[] = [];

  for (const [index, segment] of first.entries()) {
    if (!split.every((candidate): boolean => candidate[index] === segment)) {
      break;
    }

    shared.push(segment);
  }

  return shared.join("/");
}

/**
 * The `App` governing `directory`, or null when nothing above it declares zones.
 *
 * `tsconfig.json` carries `//` comments and `JSON.parse` rejects them, so they are
 * stripped first. A `tsconfig.json` without `compilerOptions.paths` does not stop the
 * walk — a package can have one for other reasons.
 */
function readApp(directory: string): App | null {
  const configPath = resolve(directory, "tsconfig.json");

  if (!existsSync(configPath)) {
    return null;
  }

  const text = readFileSync(configPath, "utf8").replaceAll(/^\s*\/\/.*$/gmu, "");
  const config = JSON.parse(text) as { compilerOptions?: { paths?: Record<string, string[]> } };
  const paths = config.compilerOptions?.paths ?? {};
  const entries = Object.entries(paths);

  if (entries.length === 0) {
    return null;
  }

  const zoneDirectories = entries.map(([, [target = ""]]): string =>
    resolve(directory, target.replace(/\/\*$/u, "")),
  );
  const aliases = entries
    .map(([key]): string => key.replace(/\/\*$/u, ""))
    .toSorted((a, b): number => b.length - a.length);

  return { srcDir: commonDirectory(zoneDirectories), aliases };
}

export const zoneDag = defineRule({
  meta: {
    type: "problem",
    docs: {
      description: "Enforce the one-way import graph between an app's src/ zones.",
    },
    schema: [
      {
        type: "object",
        properties: { barrelZones: { type: "array", items: { type: "string" } } },
        additionalProperties: false,
      },
    ],
    messages: {
      relativeCrossZone:
        "`{{specifier}}` reaches from `{{from}}` into `{{to}}` as a relative hop. A relative " +
        "specifier is legal WITHIN a zone at any depth; the moment it lands in another zone it " +
        "has to say so, because a boundary crossing hidden in `../../` is a boundary crossing " +
        "nobody reviews. Spell it as the zone's alias.",
      deepIntoFeature:
        "`{{specifier}}` reaches around `{{to}}`'s barrel. The barrel IS the contract — " +
        "`@features/login` is what a feature offers, `@features/login/anything` is what it " +
        "happens to contain. Export what you need from the feature's `index.ts` and import that.",
      siblingFeature:
        "`{{from}}` reaches sideways into `{{to}}`. Features are siblings, not a hierarchy: what " +
        "two of them both need belongs in `shared/`, and what one needs FROM the other means " +
        "they are one feature.",
      reachesBackIntoApp:
        "`{{from}}` reaches back into `app/`. The graph is one-way — `app/` composes features " +
        "and shared, never the reverse — so a module that reads the router or a route is a piece " +
        "of app wiring living in the wrong zone. (A spec may do this; a product module may not.)",
      sharedReachesFeature:
        "`shared/` reaches into `{{to}}`. `shared/` is what features are built FROM; a shared " +
        "module that knows a feature's name is a feature module in hiding. This holds for specs " +
        "too — there is no exemption here.",
      escapesSrc:
        "`{{specifier}}` resolves outside `src/`. That is legal only from a root-level policy " +
        "spec, which exists precisely to assert things about `vite.config.ts` and the app " +
        "manifest. A product module reaching out of `src/` has left the graph entirely.",
    },
  },

  createOnce(context) {
    /** Directory -> its governing app, memoised for the life of the process. */
    const apps = new Map<string, App | null>();

    /** Per-file, set by `Program` — `before()` is not guaranteed to run on every file. */
    let app: App | null = null;
    let file = "";
    let zone = ROOT_ZONE;

    function appFor(start: string): App | null {
      const visited: string[] = [];
      let directory = start;
      let found: App | null = null;

      for (;;) {
        const cached = apps.get(directory);

        if (cached !== undefined) {
          found = cached;
          break;
        }

        visited.push(directory);
        found = readApp(directory);

        if (found !== null) {
          break;
        }

        const parent = dirname(directory);

        if (parent === directory) {
          break;
        }

        directory = parent;
      }

      for (const entry of visited) {
        apps.set(entry, found);
      }

      return found;
    }

    function barrelZones(): ReadonlySet<string> {
      const options = context.options[0] as { barrelZones?: readonly string[] } | undefined;

      return new Set(options?.barrelZones ?? DEFAULT_BARREL_ZONES);
    }

    /** The zone a `src/`-relative path belongs to. */
    function zoneOf(srcRelativePath: string, barrels: ReadonlySet<string>): string {
      const segments = srcRelativePath.split("/");

      if (segments.length <= 1) {
        return ROOT_ZONE;
      }

      const [top = "", next = ""] = segments;

      return barrels.has(top) ? `${top}/${next}` : top;
    }

    /** Classify one specifier as written by the current file. */
    function targetOf(specifier: string, barrels: ReadonlySet<string>): Target {
      const alias = app?.aliases.find((candidate): boolean =>
        specifier.startsWith(`${candidate}/`),
      );

      if (alias !== undefined) {
        const zoneName = alias.slice(1);
        const segments = specifier.split("/");

        if (!barrels.has(zoneName)) {
          return { kind: "zone", zone: zoneName, alias: true, deep: false };
        }

        const [, member = ""] = segments;

        return {
          kind: "zone",
          zone: `${zoneName}/${member}`,
          alias: true,
          deep: segments.length > BARREL_SEGMENTS,
        };
      }

      if (!specifier.startsWith(".")) {
        return { kind: "package" };
      }

      const resolved = relative(app!.srcDir, resolve(dirname(context.filename), specifier));

      return resolved.startsWith("..")
        ? { kind: "outside" }
        : { kind: "zone", zone: zoneOf(resolved, barrels), alias: false, deep: false };
    }

    function judge(specifier: string, node: ESTree.Node): void {
      if (app === null || zone === ROOT_ZONE) {
        return;
      }

      const barrels = barrelZones();
      const target = targetOf(specifier, barrels);
      const data = { specifier, from: zone, to: "" };

      if (target.kind === "outside") {
        context.report({ node, messageId: "escapesSrc", data });

        return;
      }

      if (target.kind === "package") {
        return;
      }

      const reported = { ...data, to: target.zone };

      if (!target.alias && target.zone !== zone) {
        context.report({ node, messageId: "relativeCrossZone", data: reported });
      }

      if (target.deep) {
        context.report({ node, messageId: "deepIntoFeature", data: reported });
      }

      if (
        zone.includes("/") &&
        target.zone.includes("/") &&
        zone.split("/")[0] === target.zone.split("/")[0] &&
        target.zone !== zone
      ) {
        context.report({ node, messageId: "siblingFeature", data: reported });
      }

      if (target.zone === "app" && zone !== "app" && !SPEC.test(file)) {
        context.report({ node, messageId: "reachesBackIntoApp", data: reported });
      }

      if (zone === "shared" && barrels.has(target.zone.split("/")[0] ?? "")) {
        context.report({ node, messageId: "sharedReachesFeature", data: reported });
      }
    }

    return {
      Program() {
        app = appFor(dirname(context.filename));
        file = "";
        zone = ROOT_ZONE;

        if (app === null) {
          return;
        }

        const srcRelative = relative(app.srcDir, context.filename);

        if (srcRelative.startsWith("..")) {
          app = null;

          return;
        }

        file = srcRelative;
        zone = zoneOf(srcRelative, barrelZones());
      },

      ImportDeclaration(node) {
        judge(node.source.value, node.source);
      },

      ExportNamedDeclaration(node) {
        if (node.source !== null) {
          judge(node.source.value, node.source);
        }
      },

      ExportAllDeclaration(node) {
        judge(node.source.value, node.source);
      },

      /**
       * A dynamic import is exactly how a module reaches something it may not reach at
       * module scope, so a boundary guard blind to it has a hole shaped like the
       * violation it exists to catch. Template-literal and variable specifiers cannot be
       * judged statically and are left alone.
       */
      ImportExpression(node) {
        if (node.source.type === "Literal" && typeof node.source.value === "string") {
          judge(node.source.value, node.source);
        }
      },
    };
  },
});
