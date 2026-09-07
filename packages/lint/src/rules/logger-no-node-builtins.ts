import { isBuiltin } from "node:module";

import { defineRule } from "@oxlint/plugins";

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

/**
 * Reject Node built-in imports in browser-owned logger source files.
 * The server entry and its listed helpers, plus test files, are exempt.
 * Uses a filename allowlist rather than walking the module graph.
 */
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
