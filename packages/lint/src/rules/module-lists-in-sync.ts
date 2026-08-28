import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";

import { defineRule, type ESTree } from "@oxlint/plugins";

/**
 * Keep a library package's four module lists agreeing with each other.
 *
 * Every `packages/*` library builds through `defineLibraryConfig` and declares
 * its modules four times over: the `entries` map in `vite.config.ts`, the
 * `exports` map and the `publishConfig.exports` map in `package.json`, and the
 * `include` list in `tsconfig.build.json`. A module added to some of them and
 * missed in another fails SILENTLY — an `exports` subpath with no lib entry
 * resolves in-repo (where `exports` points at `src/`) and publishes a path the
 * build never emitted; an entry missing from `include` emits no declarations;
 * an entry missing from `exports` is unreachable for consumers. `packages/env`
 * is where this bit: its four lists misaddress a Start app when they drift, and
 * the spec that used to diff them read source off disk and was deleted under
 * the no-source-tests doctrine (`.claude/rules/TESTING.md`).
 *
 * The rule anchors on `vite.config.ts` — the one list oxlint lints — and reads
 * the two JSON siblings off disk, the same way `zone-dag` reads an app's
 * `tsconfig.json`. Entry keys normalize to `exports` subpaths (`index` ↔ `.`,
 * `server/index` ↔ `./server`, `extra` ↔ `./extra`); the `include` list is
 * compared against the entry VALUES, since both name source files. A list the
 * rule cannot enumerate is skipped, not guessed at: a spread in `entries`
 * (packages/ui), a `*` in any `exports` key, a glob or directory in `include`.
 * Manifest keys whose target is not a JS/TS module (`./styles.css`,
 * `./source.css`) need no entry and are ignored.
 *
 * Self-gates on the `vite.config.ts` filename, so the root config switches it
 * on once repo-wide and it stays inert everywhere else — a helper or spec that
 * merely calls `defineLibraryConfig` is never judged against whatever
 * `package.json` sits beside it.
 */

/** The one file whose directory's manifests the rule may read. */
const CONFIG_FILE = /[\\/]vite\.config\.ts$/u;

/** An `exports` target that is a module and therefore needs a lib entry. */
const MODULE_TARGET = /\.(?:ts|tsx|js|mjs)$/u;

/** An `include` member the rule can compare — one source file, no glob. */
const INCLUDE_MEMBER = /^[^*]+\.tsx?$/u;

/** One lib entry: its manifest-subpath name, its source path, and its node. */
interface Entry {
  readonly name: string;
  readonly source: string;
  readonly node: ESTree.Node;
}

/** `index` → `.`, `server/index` → `./server`, `extra` → `./extra`. */
function subpathOf(key: string): string {
  if (key === "index") {
    return ".";
  }

  const trimmed = key.endsWith("/index") ? key.slice(0, -"/index".length) : key;

  return `./${trimmed}`;
}

/**
 * The literal string a property key spells, or null when it is computed —
 * a computed key makes the map non-enumerable as AST.
 */
function keyOf(property: ESTree.ObjectProperty): string | null {
  if (!property.computed && property.key.type === "Identifier") {
    return property.key.name;
  }

  if (property.key.type === "Literal" && typeof property.key.value === "string") {
    return property.key.value;
  }

  return null;
}

/**
 * The entries map as a list, or null when any member defeats static reading —
 * a spread (`...componentEntries()`), a computed key, a non-literal value.
 */
function entriesOf(object: ESTree.ObjectExpression): readonly Entry[] | null {
  const found: Entry[] = [];

  for (const property of object.properties) {
    if (property.type !== "Property") {
      return null;
    }

    const key = keyOf(property);

    if (
      key === null ||
      property.value.type !== "Literal" ||
      typeof property.value.value !== "string"
    ) {
      return null;
    }

    found.push({ name: subpathOf(key), source: property.value.value, node: property });
  }

  return found;
}

/**
 * Parse a JSON(C) sibling, tolerating tsconfig-style `//` line comments. A file
 * that is absent or does not parse yields null and its lists are skipped — a
 * broken manifest already fails `pnpm install` or the build, loudly.
 */
function readJson(path: string): Record<string, unknown> | null {
  if (!existsSync(path)) {
    return null;
  }

  try {
    const text = readFileSync(path, "utf8").replaceAll(/^\s*\/\/.*$/gmu, "");

    return JSON.parse(text) as Record<string, unknown>;
  } catch {
    return null;
  }
}

/** An `exports` value's file target, whatever shape the map spells it in. */
function targetOf(value: unknown): string | null {
  if (typeof value === "string") {
    return value;
  }

  if (typeof value === "object" && value !== null) {
    const conditions = value as Record<string, unknown>;

    for (const condition of ["import", "types", "default"]) {
      if (typeof conditions[condition] === "string") {
        return conditions[condition];
      }
    }
  }

  return null;
}

/**
 * The module subpaths an `exports`-shaped map declares, or null when a `*`
 * makes the map non-enumerable. Keys whose target is not a JS/TS file — a
 * stylesheet passthrough, `./package.json` — need no lib entry and are dropped.
 */
function moduleSubpathsOf(map: unknown): readonly string[] | null {
  if (typeof map !== "object" || map === null) {
    return null;
  }

  const record = map as Record<string, unknown>;
  const keys = Object.keys(record);

  if (keys.some((key): boolean => key.includes("*"))) {
    return null;
  }

  return keys.filter((key): boolean => {
    const target = targetOf(record[key]);

    return target !== null && MODULE_TARGET.test(target);
  });
}

export const moduleListsInSync = defineRule({
  meta: {
    type: "problem",
    docs: {
      description:
        "Keep a library's lib entries, exports maps and tsconfig.build include naming one module set.",
    },
    schema: [],
    messages: {
      entryNotListed:
        "`{{name}}` has a lib entry here but is missing from {{list}}. The entries map, " +
        "`exports`, `publishConfig.exports` and `tsconfig.build.json`'s `include` declare one " +
        "module set — a subpath absent from `exports` is unreachable for consumers, and a " +
        "source file absent from `include` emits no declarations.",
      listedWithoutEntry:
        "{{list}} declares `{{name}}`, but no lib entry emits it — the build produces no file " +
        "for that subpath to resolve, and only `pnpm check:exports` would notice, after a " +
        "build. Add the entry here or drop it from {{list}}.",
    },
  },

  createOnce(context) {
    /** Report every disagreement between the entries and the three sibling lists. */
    function judge(entries: readonly Entry[], entriesNode: ESTree.Node): void {
      const directory = dirname(context.filename);
      const manifest = readJson(join(directory, "package.json"));
      const build = readJson(join(directory, "tsconfig.build.json"));

      const subpathLists: readonly (readonly [string, readonly string[] | null])[] = [
        ["`exports` in `package.json`", moduleSubpathsOf(manifest?.exports)],
        [
          "`publishConfig.exports` in `package.json`",
          moduleSubpathsOf(
            (manifest?.publishConfig as Record<string, unknown> | undefined)?.exports,
          ),
        ],
      ];

      for (const [list, subpaths] of subpathLists) {
        if (subpaths !== null) {
          for (const entry of entries) {
            if (!subpaths.includes(entry.name)) {
              context.report({
                node: entry.node,
                messageId: "entryNotListed",
                data: { name: entry.name, list },
              });
            }
          }

          for (const subpath of subpaths) {
            if (!entries.some((entry): boolean => entry.name === subpath)) {
              context.report({
                node: entriesNode,
                messageId: "listedWithoutEntry",
                data: { name: subpath, list },
              });
            }
          }
        }
      }

      const include = Array.isArray(build?.include) ? build.include : null;

      if (
        include === null ||
        !include.every(
          (member): boolean => typeof member === "string" && INCLUDE_MEMBER.test(member),
        )
      ) {
        return;
      }

      const list = "`include` in `tsconfig.build.json`";

      for (const entry of entries) {
        if (!include.includes(entry.source)) {
          context.report({
            node: entry.node,
            messageId: "entryNotListed",
            data: { name: entry.source, list },
          });
        }
      }

      for (const member of include as readonly string[]) {
        if (!entries.some((entry): boolean => entry.source === member)) {
          context.report({
            node: entriesNode,
            messageId: "listedWithoutEntry",
            data: { name: member, list },
          });
        }
      }
    }

    return {
      CallExpression(node) {
        if (
          !CONFIG_FILE.test(context.filename) ||
          node.callee.type !== "Identifier" ||
          node.callee.name !== "defineLibraryConfig"
        ) {
          return;
        }

        const [options] = node.arguments;

        if (options === undefined || options.type !== "ObjectExpression") {
          return;
        }

        const entriesProperty = options.properties.find(
          (property): property is ESTree.ObjectProperty =>
            property.type === "Property" && keyOf(property) === "entries",
        );

        if (entriesProperty === undefined || entriesProperty.value.type !== "ObjectExpression") {
          return;
        }

        const entries = entriesOf(entriesProperty.value);

        if (entries !== null) {
          judge(entries, entriesProperty);
        }
      },
    };
  },
});
