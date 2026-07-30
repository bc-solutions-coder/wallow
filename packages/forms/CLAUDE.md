# packages/forms — @bc-solutions-coder/forms Agent Guide

The shared **form-authoring layer**: TanStack Form (`@tanstack/react-form` ^1.33.2) state bound
onto the `@bc-solutions-coder/ui` catalog, with zod validation, a `useMutation` submit pipeline
(the hook comes from the `@bc-solutions-coder/query` facade) and the RFC 7807 error split.
Private (never published), consumed by `apps/wallow-auth` and `apps/wallow-web` as `workspace:*`.

## Layering — one direction, no exceptions

```
@bc-solutions-coder/styles  →  @bc-solutions-coder/ui  →  @bc-solutions-coder/forms  →  apps
```

`ui` knows nothing about forms and **must never import this package**. `forms` depends on `ui`
(the `Field`, `Select`, `Checkbox`, `Textarea`, `Button` and `ErrorBanner` parts it wraps) and on
`@bc-solutions-coder/sdk` for exactly one symbol: `isWallowError`, in `core/server-error.ts`.
React and `react-dom` are **peer** dependencies — keep them out of `dependencies`.

**TanStack Query is not a peer here, and this package must never name
`@tanstack/react-query`.** It arrives as a `workspace:*` **dependency** on
`@bc-solutions-coder/query`, the private facade that is the one declarer of react-query in the
repo — so `useAppForm`'s `useMutation` and the host's `QueryClientProvider` are guaranteed to be
the same module instance (two copies means "No QueryClient set"), which a peer range could not
guarantee. That applies to the specs too: a spec building its own `QueryClient` takes it from the
facade. `src/core/query-facade.test.ts` (imports, pre-bundle list, this guide) and
`src/core/package-scaffold.test.ts` (the manifest) are the two halves of the guard.

## Internal layout — three layers plus the barrel

| Layer | Path           | Rule                                                                                                        |
| ----- | -------------- | ----------------------------------------------------------------------------------------------------------- |
| 0     | `src/core/`    | `contexts.ts`, `errors.ts`, `server-error.ts`, `test-id.ts` — import **nothing** from `fields/` or `form/`. |
| 1     | `src/form/`    | The shell: `app-form.tsx`, `app-form-context.ts`, `use-app-form.ts`, `submit-button.tsx`, `form-error.tsx`. |
| 1     | `src/fields/`  | One file per catalog field, plus `field-parts.tsx` (the label/message/state internals all five share).      |
| 2     | `src/index.ts` | The one public entry. There are no subpaths.                                                                |

`core/form-hook.tsx` is the deliberate exception: it is the ONE file in `core/` that imports from
`fields/`, because the package's single `createFormHook` call and its `fieldComponents` registry
have to sit together. Fields import `core/contexts` and `form/app-form-context`, never
`core/form-hook`, so no cycle forms.

- **`createFormHookContexts()` is called once** (`core/contexts.ts`) and **`createFormHook()` is
  called once** (`core/form-hook.tsx`). A second call anywhere produces a parallel binding whose
  fields read a context no `AppForm` publishes.
- `src/index.ts` is the contract. `src/index.test.ts` pins it in **both** directions:
  `PUBLIC_RUNTIME_EXPORTS` as an exact set, and `INTERNAL_EXPORTS` (the TanStack contexts, the
  shell's own React context, the field-part helpers) asserted absent.

## Adding a catalog field

Four files move together, plus a fifth when new Base UI surface is involved:

1. `src/fields/<name>-field.tsx` — copy `text-field.tsx`. Build the row from `useCatalogField`,
   `CatalogFieldLabel` and `CatalogFieldError` so testid derivation and the `testId` override
   (which covers the control **and** its `-error` id) behave identically to every other field.
2. `src/core/form-hook.tsx` — add it to `fieldComponents`. That is what makes it a member of
   `AppField`'s render-prop argument.
3. `src/index.ts` — export the component and its props type.
4. `src/index.test.ts` — add the runtime name to `PUBLIC_RUNTIME_EXPORTS` **and** the props type to
   the `PublicTypeExports` tuple at the bottom of the same file. (One file carries both here,
   unlike `packages/ui`'s three.) Growing one alone turns the other red.
5. `vitest.config.ts` — append any new `@base-ui/react/<part>` subpath to `baseUiSubpaths`, and any
   new runtime to `formRuntime`. Not an optimisation: undiscovered, Vite pre-bundles a second copy
   of React and specs die on `Cannot read properties of null (reading 'useRef')`.

## Test model

`vitest.config.ts` takes the shared node + headless-Chromium split from
`@bc-solutions-coder/testing`'s `createVitestProjects`, then re-adds styling to the browser project
(`wallowStyles()` compiling `vitest-styles.css` via `vitest.setup.ts`).

- `*.test.ts` → **node** project (barrel pin, testid/error-split helpers, the on-disk scaffold
  guard). `*.test.tsx` → **browser** project. `nodeTsxSpecs` is empty: there are no render-nothing
  `.tsx` specs.
- **Real CSS is required, not cosmetic.** A ui control gets its box from a recipe utility; without
  the stylesheet `Checkbox.Root` measures 0×0 and a spec that clicks it hangs until Playwright's
  actionability timeout.
- **Never mock `@bc-solutions-coder/ui`** — repo-wide rule (`.claude/rules/TESTING.md`). Never
  jsdom or happy-dom.
- `src/index.test.ts`'s `dist/` assertions are `skipIf(distIsMissing)`, and `pnpm check` runs test
  **before** build — rebuild after changing the barrel to arm them.

## Gotchas

- **`z.string().trim()` does not trim submitted values.** TanStack's standard-schema adapter reads
  only the issue list off a validation result and discards the parsed output, so `form.state.values`
  stays raw. `.trim()` only makes `"   "` fail a `.min(1)`.
- **The schema is an `onDynamic` validator, never an `onSubmit` one.** `useAppForm` sets
  `validationLogic: revalidateLogic()` (validate on submit, revalidate on change thereafter), and
  that strategy runs **only** `onDynamic` — it ignores `onChange`/`onBlur`/`onSubmit` entirely, so
  moving the schema back to `validators.onSubmit` silently validates nothing. The timing contract
  lives in `src/form/use-app-form.revalidate.test.tsx`; the `TOnDynamic` generic slot in
  `AppFormApi` has to move with it or the instance type stops matching.
- **App-level specs resolve this package from `dist/`, not `src/`.** Change the hook and the
  `wallow-auth`/`wallow-web` suites keep testing the previous bundle until
  `pnpm --filter @bc-solutions-coder/forms build` runs. A consumer spec failing on behaviour the
  package's own specs prove is that, not a real divergence.
- **`clearServerErrors()` runs before validation, not inside `onSubmit`.** `handleSubmit` aborts on
  `!isFieldsValid`, and nothing in `@tanstack/form-core` clears an `onServer` error by itself — so a
  stale server field error would wedge every later submit. `AppForm`'s submit handler owns the call.
- **`UseAppFormOptions`' `TError` is defaulted (`= unknown`) and inferred from the whole mutation
  object.** It sits contravariantly inside `UseMutationOptions`, so pinning it to any one concrete
  type rejects every generated factory that names a different one. Never destructure or cast a
  generated `{operation}Mutation({ client })` to work around a type error — that is the bug.
- **`react/jsx-max-depth` is 2** and `pnpm lint` runs `--deny-warnings`, which is why `SelectField`'s
  portal tree is split into one component per nesting level. `unicorn/catch-error-name` requires the
  catch parameter be named `error` outside tests.
- **`.oxlintrc.json` here `extends` the root config, and its override globs must stay
  directory-relative.** oxlint reads the NEAREST config for a file and does not merge upward on
  its own, so dropping `extends` silently replaces the root's plugins, categories and every
  `no-restricted-imports` ban for this whole subtree. The non-obvious half: an override glob in
  this file is matched against the path relative to `packages/forms/`, so a repo-rooted prefix
  copied from the root config (`packages/forms/**/*.tsx`) matches nothing and fails silently.
  Never restate `categories` or `plugins` here either. `packages/sdk/src/oxlint-guardrails.test.ts`
  asserts all three. The rules the test/story override turns off, and why each is wrong for a
  component spec, are listed in `packages/ui/CLAUDE.md`; the two configs are kept identical.
- `.oxlintrc.json` here turns off `react/jsx-props-no-spreading`.
- The package is `private: true`, so it is **not** in `scripts/check-exports.sh`'s package list
  (same as `packages/ui`) — `pnpm check:exports` neither covers it nor needs to.

## Scripts

```bash
pnpm --filter @bc-solutions-coder/forms build       # vite build (lib mode) && tsc -p tsconfig.build.json
pnpm --filter @bc-solutions-coder/forms test        # vitest run — node | browser
pnpm --filter @bc-solutions-coder/forms typecheck   # tsc --noEmit
```

Consumer-facing docs (how a form is authored, the testid derivation, the error model, the escape
hatches) live in `docs/development/forms.md`.
