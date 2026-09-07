**status: active**

# Workspace package documentation

Review and clean up comments throughout all 14 libraries under `packages/`, including private workspace libraries and the published SDK. Baseline: `6e1d5f164494ad9e87b083320f109a30459c9621`. Application packages under `apps/` are consumers, outside this library documentation pass.

Document each package's purpose, supported imports, setup and usage. Public functions, components, hooks, types, and important options need accurate editor documentation grounded in implementation. Explain internal constraints where useful; remove stale migration history, misleading claims, and comments that merely repeat code. Preserve compiler/linter directives, legal notices, generated ownership, and executable behavior. Source comments remain the owner of declaration documentation; never hand-edit generated output.

Inventory every package and source file. Review independent package groups in parallel, with exclusive file ownership. Add or improve package READMEs without duplicating long integration guides. Keep an export and comment audit outside the repository during implementation, then summarize its results here.

Run an AST comparison of changed TypeScript/JavaScript to prove executable code is unchanged, validate README examples and links, check generated declarations for consumer comments, run `pnpm check`, and inspect packed published packages. No source-text assertion tests. Complete independent Standards and Spec reviews, commit and push the current branch, and verify CI.
