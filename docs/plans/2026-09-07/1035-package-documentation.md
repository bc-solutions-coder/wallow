**status: completed**

# Workspace package documentation

Review and clean up comments throughout all 14 libraries under `packages/`, including private workspace libraries and the published SDK. Baseline: `6e1d5f164494ad9e87b083320f109a30459c9621`. Application packages under `apps/` are consumers, outside this library documentation pass.

Document each package's purpose, supported imports, setup and usage. Public functions, components, hooks, types, and important options need accurate editor documentation grounded in implementation. Explain internal constraints where useful; remove stale migration history, misleading claims, and comments that merely repeat code. Preserve compiler/linter directives, legal notices, generated ownership, and executable behavior. Source comments remain the owner of declaration documentation; never hand-edit generated output.

Inventory every package and source file. Review independent package groups in parallel, with exclusive file ownership. Add or improve package READMEs without duplicating long integration guides. Keep an export and comment audit outside the repository during implementation, then summarize its results here.

Run an AST comparison of changed TypeScript/JavaScript to prove executable code is unchanged, validate README examples and links, check generated declarations for consumer comments, run `pnpm check`, and inspect packed published packages. No source-text assertion tests. Complete independent Standards and Spec reviews, commit and push the current branch, and verify CI.

## Implementation and review evidence

All 14 library packages have concise READMEs, with a shared index at `packages/README.md`. Source comments cover public functions, hooks, components, types and options. Internal, test and configuration prose was reviewed for inaccurate claims and obsolete history. Legal notices, compiler and lint directives, fixtures, and release history were retained. Four package `CLAUDE.md` files received factual corrections to match their implementations.

The SDK generator now supplies comments for all 874 generated public type aliases through supported generator hooks. Existing schema descriptions and deprecated tags are preserved; missing descriptions identify actual endpoint usage. All 395 generated endpoint and query functions retain their OpenAPI summaries and descriptions.

Validation artifacts are local under `/tmp/wallow-package-docs/`:

- Source and built-export audits find zero undocumented package-owned public symbols across all 14 packages. Counts include re-exports within each package; external library symbols retain upstream documentation.
- All 24 TypeScript/TSX README examples pass strict consumer checks, and all 26 relative links resolve. Built declarations are used where available; source-only packages use their source exports.
- With dependency declaration checking enabled, 11 of 13 snippet-bearing packages pass. Auth has two existing missing symbol imports in emitted declarations (`dataTagSymbol` and `dataTagErrorSymbol`); config has 59 diagnostics in Nitro dependency declarations. Their examples pass with `skipLibCheck: true`. These are recorded consumer limitations, not changed by this documentation pass.
- All 355 modified existing TypeScript/JavaScript files outside generator implementation have identical executable tokens, allowing optional trailing commas. Eleven modified JSON/JSONC/CSS/SVG files retain the same data and rules.
- Packed SDK and api-errors archives include the READMEs and all 56 and 8 declaration files respectively, byte-for-byte matching the checked builds. Generated function documentation is also checked directly in packed declarations.
- Independent Standards review found no issues. Spec review found an incorrect virtual theme import in the testing README; it was corrected to `virtual:wallow-theme.css`.

The contrast helper documentation now records its existing limitations: invalid CSS falls back to black, and translucent RGB channels can exceed 255. A real Chromium canvas check confirmed red `[255, 0, 0, 128]` becomes approximately 508 in the helper's current alpha calculation. Runtime behavior was preserved.

`pnpm check` passed after the CSS header wording was corrected to avoid triggering an existing raw-text test. The gate includes lint, formatting, builds, type checks, workspace tests, generated drift checks, and the packed external SDK consumer. No source tests were added.
