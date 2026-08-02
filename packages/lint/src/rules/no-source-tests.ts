import { defineRule } from "@oxlint/plugins";

/**
 * Ban the filesystem in a spec.
 *
 * A test that reads application source, README prose or directory layout off
 * disk is not testing behaviour — it is constraining how code is WRITTEN, which
 * is a linter's job. The distinction is not stylistic. Seventy-seven specs in
 * this repo read source as text, every one of them hand-rolled the same
 * two-pass comment stripper, and that stripper is wrong: it removes block
 * comments BEFORE line comments, so a route glob written as a line comment can
 * open a block comment that runs to the file's next block-comment close, taking
 * everything between with it. Measured in wallow-auth,
 * spans of 2871, 2884 and 4041 characters of real source vanished from what the
 * guards read. On a NEGATIVE sweep — "this symbol appears nowhere" — an
 * over-deletion is indistinguishable from a clean module, so the spec goes green
 * over code it never saw.
 *
 * The fix is not a better scanner. A `wallow/*` AST rule reads the parsed tree,
 * fires in the editor, names the offending line, and cannot be out-run by
 * formatting. And most structural sweeps should not become rules either: a spec
 * pinning a folder anatomy or a README's wording makes the codebase rigid
 * without making it correct. **Prefer deleting the constraint to relocating it.**
 *
 * WHY `node:fs` AND NOT THE READ CALLS. Keying on `readFileSync`/`readdirSync`/
 * `existsSync` invites the workaround of aliasing the import, and it misses
 * `fs.readFileSync` member calls entirely. The import is the chokepoint: a spec
 * that cannot name the module cannot read a file, whatever it calls the binding.
 * That deliberately catches a legitimate-looking `existsSync` too — proving a
 * built artifact exists is `pnpm check:exports`' job (publint +
 * @arethetypeswrong/cli over real `dist/`), not a spec's.
 *
 * WHAT IS STILL FINE, and needs no exemption because it imports no `node:fs`:
 * importing the module under test and asserting export identity; importing a
 * `vite.config.ts` / `vitest.config.ts` and reading the resolved OBJECT rather
 * than the file's text; and `import.meta.glob`, which is a bundler feature that
 * imports modules rather than reading them as strings.
 *
 * The rule self-gates on the filename, so a config switches it on once at the
 * top level and it stays inert over source. `context.filename` is an absolute
 * path (`packages/lint/CLAUDE.md`), so the suffix test is safe.
 */

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
