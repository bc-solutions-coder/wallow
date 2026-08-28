import { isBuiltin } from "node:module";

import { defineRule } from "@oxlint/plugins";

/**
 * Keep Node built-ins out of `packages/logger`'s browser entry.
 *
 * The package ships two entries into two runtimes: `.` (`src/index.ts`) is the
 * browser core that runs in the user's tab, `./server` (`src/server.ts`) is the
 * app-server ingest handler. A `node:fs` (or bare `path`, `process`, ...) import
 * anywhere in the browser graph breaks the client bundle; the server graph is
 * free to use them. The invariant used to be held by a spec that read every
 * module's source off disk — deleted under `wallow/no-source-tests`' doctrine
 * (`.claude/rules/TESTING.md`), because constraining how code is WRITTEN is a
 * lint rule's job.
 *
 * WHY A RULE AND NOT THE `types: []` COMPILE GUARD. Today a `node:*` import in
 * this package fails `tsc` because no `@types/node` is reachable — but that
 * guard covers BOTH entries indiscriminately, and evaporates for the browser
 * entry the day `@types/node` is added so the server entry can use a built-in.
 * This rule is entry-aware and fires in the editor, naming the line.
 *
 * WHY AN ALLOWLIST AND NOT A GRAPH WALK. A rule sees one file at a time, so it
 * cannot trace what `index.ts` transitively imports. Instead every non-spec file
 * under `packages/logger/src/` is treated as browser-owned unless it is one of
 * the three server-owned modules (`server.ts` plus the `otlp.ts` / `rate-limit.ts`
 * helpers it composes — `packages/logger/CLAUDE.md` owns that census). Fail-safe
 * by construction: a new server-side helper that needs a built-in draws a
 * diagnostic naming this allowlist, rather than a new browser-side file silently
 * shipping `node:path` to the page. Specs are exempt — they ship in neither
 * bundle, and what a spec may import is `wallow/no-source-tests`' business.
 *
 * The built-in test is `node:module`'s `isBuiltin`, which covers the bare and
 * `node:`-prefixed spellings alike. The repo-wide `import/no-nodejs-modules`
 * cannot be the mechanism: it is deliberately OFF by name in the root config,
 * because the SDK's server entry legitimately imports `node:crypto`.
 *
 * The rule self-gates on the filename, so the root config switches it on once
 * and it is inert outside `packages/logger/src/`.
 */

/** The gated directory, separator-tolerant for the Windows case. */
const LOGGER_SRC: RegExp = /packages[\\/]logger[\\/]src[\\/]/u;

/** The `./server` graph: the one entry allowed to touch Node built-ins. */
const SERVER_OWNED: RegExp = /(?:^|[\\/])(?:server|otlp|rate-limit)\.ts$/u;

/** A spec file, which ships in neither bundle. */
const SPEC: RegExp = /\.test\.[cm]?[jt]sx?$/u;

const MESSAGE: string =
  "packages/logger's browser entry ships to the page: no file it owns may import a Node " +
  "built-in. Only the ./server graph (server.ts, otlp.ts, rate-limit.ts) may — if this " +
  "module genuinely belongs to it, extend that allowlist in wallow/logger-no-node-builtins " +
  "and the census in packages/logger/CLAUDE.md together.";

/** A browser-owned logger module — the only place this rule has anything to say. */
function isBrowserOwned(filename: string): boolean {
  return LOGGER_SRC.test(filename) && !SERVER_OWNED.test(filename) && !SPEC.test(filename);
}

export const loggerNoNodeBuiltins = defineRule({
  meta: {
    type: "problem",
    docs: {
      description: "Ban Node built-in imports from packages/logger's browser-owned modules.",
    },
    schema: [],
  },

  createOnce(context) {
    return {
      ImportDeclaration(node) {
        if (!isBrowserOwned(context.filename) || !isBuiltin(String(node.source.value))) {
          return;
        }

        context.report({ node: node.source, message: MESSAGE });
      },

      ImportExpression(node) {
        const source = node.source;

        if (
          !isBrowserOwned(context.filename) ||
          source.type !== "Literal" ||
          !isBuiltin(String(source.value))
        ) {
          return;
        }

        context.report({ node: source, message: MESSAGE });
      },

      CallExpression(node) {
        const callee = node.callee;
        const argument = node.arguments[0];

        if (
          !isBrowserOwned(context.filename) ||
          callee.type !== "Identifier" ||
          callee.name !== "require" ||
          argument === undefined ||
          argument.type !== "Literal" ||
          !isBuiltin(String(argument.value))
        ) {
          return;
        }

        context.report({ node: argument, message: MESSAGE });
      },
    };
  },
});
