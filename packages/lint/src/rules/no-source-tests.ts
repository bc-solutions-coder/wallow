import { defineRule } from "@oxlint/plugins";

/** The specifiers that hand a spec a file's bytes. */
const BANNED: ReadonlySet<string> = new Set(["fs", "fs/promises", "node:fs", "node:fs/promises"]);

const MESSAGE: string =
  "A spec does not read the filesystem. Reading source as text constrains how code is written, " +
  "which is a lint rule's job (`packages/lint/CLAUDE.md`) — and most of these constraints are " +
  "better deleted than relocated, per `.claude/rules/TESTING.md`. Import the module and assert " +
  "what it DOES; read a `vite.config.ts` as an imported object rather than as text; leave " +
  "manifests and `dist/` layout to `pnpm check:exports`.";

/** A spec file, which is the only place this rule has anything to say. */
function isSpec(filename: string): boolean {
  return /\.test\.[cm]?[jt]sx?$/u.test(filename);
}

/**
 * Reject filesystem imports in files named .test.ts, .test.tsx, and related JS forms.
 * Matches fs and fs/promises with or without node: prefixes. Tests can import
 * modules and resolved configuration objects directly.
 */
export const noSourceTests = defineRule({
  meta: {
    type: "problem",
    docs: { description: "Ban filesystem reads in a spec — tests assert behaviour, not source." },
    schema: [],
  },

  createOnce(context) {
    return {
      ImportDeclaration(node) {
        if (!isSpec(context.filename) || !BANNED.has(String(node.source.value))) {
          return;
        }

        context.report({ node: node.source, message: MESSAGE });
      },

      ImportExpression(node) {
        const source = node.source;

        if (
          !isSpec(context.filename) ||
          source.type !== "Literal" ||
          !BANNED.has(String(source.value))
        ) {
          return;
        }

        context.report({ node: source, message: MESSAGE });
      },

      CallExpression(node) {
        const callee = node.callee;
        const argument = node.arguments[0];

        if (
          !isSpec(context.filename) ||
          callee.type !== "Identifier" ||
          callee.name !== "require" ||
          argument === undefined ||
          argument.type !== "Literal" ||
          !BANNED.has(String(argument.value))
        ) {
          return;
        }

        context.report({ node: argument, message: MESSAGE });
      },
    };
  },
});
