# @bc-solutions-coder/lint

Private workspace plugin for Wallow's oxlint rules. The default export is an
ESLint-compatible plugin object. The repository loads its TypeScript source
through Node; it is not a package to install from the registry.

## Registration

The root `.oxlintrc.json` registers the plugin once:

```json
{
  "jsPlugins": [{ "name": "wallow", "specifier": "@bc-solutions-coder/lint" }]
}
```

Nested configs inherit registration through `extends` and enable the rules they
need. A plugin specifier resolves from the registering config's directory.
An explicit oxlint `-c` disables nested-config lookup, so the repository lint
commands rely on normal config discovery.

## Rules

| Rule                             | Checks                                                                                   | Options                                                                  |
| -------------------------------- | ---------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| `wallow/logger-no-node-builtins` | Node built-in imports in browser-owned logger files; server helpers and tests are exempt | None; filename-gated                                                     |
| `wallow/module-lists-in-sync`    | Explicit Vite entries, package export maps, and declaration inputs agree                 | None; runs on `vite.config.ts`; skips non-enumerable lists               |
| `wallow/no-source-tests`         | Filesystem imports in test files                                                         | None; filename-gated                                                     |
| `wallow/no-hand-rolled-mutation` | Object properties named `mutationFn`                                                     | None; enable only where generated SDK mutations are required             |
| `wallow/no-tinted-text`          | Alpha modifiers on theme text colors in strings                                          | None                                                                     |
| `wallow/no-sidebar-inversion`    | Opaque `bg-foreground` and `text-background` classes                                     | None; alpha modifiers remain allowed                                     |
| `wallow/text-heading-variant`    | Explicit variants on literal `Text` headings                                             | `levels` maps a heading to a required variant, or `false` to prohibit it |
| `wallow/zone-dag`                | Cross-zone aliases, feature barrels, and app/feature/shared dependency direction         | `barrelZones`, defaulting to `features`                                  |

Rule option objects are replaced by overrides, not merged. Restate all intended
heading levels in a scoped override. The zone rule reads aliases from the nearest
app configuration with `compilerOptions.paths`; root-level source files are exempt.

## Development

Run from the repository root:

```bash
pnpm --filter @bc-solutions-coder/lint test
pnpm --filter @bc-solutions-coder/lint typecheck
```

Rules use `createOnce`; read per-file options inside visitors and reset file state
in `Program` when every file must be inspected. Relative source imports require
`.ts` extensions for Node ESM loading.

The fixture suite discovers `fixtures/<rule>/` directories and runs oxlint against
each one. Place `// expect-error: wallow/<rule>` immediately before each expected
diagnostic. Valid fixtures must produce no diagnostics. Preserve these comments
and their line placement: they are test inputs. A rule with no fixture directory
is not covered by this suite.
