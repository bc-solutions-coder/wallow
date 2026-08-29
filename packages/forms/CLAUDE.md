# packages/forms — @bc-solutions-coder/forms Agent Guide

The shared **form-authoring layer**: TanStack Form (`@tanstack/react-form`) state bound onto
the `@bc-solutions-coder/ui` catalog, with zod validation, a `useMutation` submit pipeline
(the hook comes from the `@bc-solutions-coder/query` facade) and the RFC 7807 error split.
Private (never published), consumed by `apps/wallow-auth` and `apps/wallow-web` as
`workspace:*`. Consumer-facing docs (authoring, testid derivation, error model, escape
hatches): `docs/development/forms.md`.

## Layering — one direction, no exceptions

```
@bc-solutions-coder/styles  →  @bc-solutions-coder/ui  →  @bc-solutions-coder/forms  →  apps
```

- `ui` knows nothing about forms and **must never import this package**. `forms` depends on
  `ui` (the `Field`, `Select`, `Checkbox`, `Textarea`, `Button`, `ErrorBanner` parts it
  wraps) and on `@bc-solutions-coder/sdk` for exactly one symbol: `isWallowError`, in
  `core/server-error.ts`.
- React and `react-dom` are **peer** dependencies — keep them out of `dependencies`.
- **This package must never name `@tanstack/react-query`.** react-query arrives as a
  `workspace:*` dependency on `@bc-solutions-coder/query`, the repo's one declarer of it, so
  `useAppForm`'s `useMutation` and the host's `QueryClientProvider` are guaranteed the same
  module instance (two copies means "No QueryClient set"). Specs too: build a `QueryClient`
  from the facade. The root `.oxlintrc.json` `no-restricted-imports` ban enforces it in both
  lint passes.

## Internal layout — three layers plus the barrel

| Layer | Path           | Rule                                                                                                        |
| ----- | -------------- | ----------------------------------------------------------------------------------------------------------- |
| 0     | `src/core/`    | `contexts.ts`, `errors.ts`, `server-error.ts`, `test-id.ts` — import **nothing** from `fields/` or `form/`. |
| 1     | `src/form/`    | The shell: `app-form.tsx`, `app-form-context.ts`, `use-app-form.ts`, `submit-button.tsx`, `form-error.tsx`. |
| 1     | `src/fields/`  | One file per catalog field, plus `field-parts.tsx` (the label/message/state internals fields share).        |
| 2     | `src/index.ts` | The one public entry. There are no subpaths.                                                                |

`core/form-hook.tsx` is the deliberate exception: the ONE file in `core/` that imports from
`fields/`, because the single `createFormHook` call and its `fieldComponents` registry must
sit together. Fields import `core/contexts` and `form/app-form-context`, never
`core/form-hook`, so no cycle forms.

- **`createFormHookContexts()` and `createFormHook()` are each called once**
  (`core/contexts.ts`, `core/form-hook.tsx`). A second call anywhere produces a parallel
  binding whose fields read a context no `AppForm` publishes.
- **Both RFC 7807 readers are public.** `splitServerError` is what a form needs;
  `errorText(error, fallback)` is the one-line reader for a failure with no fields to
  distribute across (a failed read, or a write rendered outside a form).
- **`TextField.inputMode` is separate from `type` on purpose**: `type="number"` eats the
  leading zero of a zero-padded one-time code, so the OTP field stays `type="text"` and asks
  for the numeric keypad via `inputMode`.
- **`PasswordField.labelAction` puts an affordance on the label's LINE, never inside the
  label** (an anchor folded into a label joins the field's accessible name and puts a
  navigation target inside the click area). Mind the depth budget when passing one —
  `react/jsx-max-depth` is 2 here and in both apps, and a prop's JSX counts at the depth of
  the element carrying it, so an inline `labelAction={<X/>}` usually must be hoisted to a
  module-scope element constant.
- `src/index.ts` is the contract. `src/index.test.ts` pins it in **both** directions:
  `PUBLIC_RUNTIME_EXPORTS` as an exact set, and `INTERNAL_EXPORTS` asserted absent.

## Adding a catalog field

Four files move together, plus a fifth when new runtime surface is involved:

1. `src/fields/<name>-field.tsx` — copy `text-field.tsx`. Build the row from
   `useCatalogField`, `CatalogFieldLabel` and `CatalogFieldError` so testid derivation and
   the `testId` override (control **and** its `-error` id) behave like every other field.
2. `src/core/form-hook.tsx` — add it to `fieldComponents`; that makes it a member of
   `AppField`'s render-prop argument.
3. `src/index.ts` — export the component and its props type.
4. `src/index.test.ts` — add the runtime name to `PUBLIC_RUNTIME_EXPORTS` **and** the props
   type to the `PublicTypeExports` tuple in the same file. Growing one alone turns the other
   red.
5. `vitest.config.ts` — append any new **runtime** to `formRuntime`. Left undiscovered, Vite
   pre-bundles a second copy of React and specs die on a null-hook read. Base UI needs
   nothing: `baseUi` is the single glob `@base-ui/react/*`, expanded against that package's
   own `exports` keys.

## Test model

`vitest.config.ts` takes the shared node + headless-Chromium split from
`@bc-solutions-coder/testing`'s `createVitestProjects`, handing it styling through the
preset's `browserPlugins` / `browserSetupFiles` pass-throughs (`wallowStyles()` compiling
`vitest-styles.css` via `vitest.setup.ts`).

- `*.test.ts` → **node** project (barrel pin, testid/error-split helpers). `*.test.tsx` →
  **browser** project. Nothing here should use the `*.ssr.test.tsx` naming.
- **Real CSS is required, not cosmetic.** A ui control gets its box from a recipe utility;
  without the stylesheet `Checkbox.Root` measures 0×0 and a spec that clicks it hangs until
  Playwright's actionability timeout.
- **Never mock `@bc-solutions-coder/ui`**; never jsdom or happy-dom
  (`.claude/rules/TESTING.md`).

## Gotchas

- **`z.string().trim()` does not trim submitted values.** TanStack's standard-schema adapter
  reads only the issue list off a validation result and discards the parsed output, so
  `form.state.values` stays raw. `.trim()` only makes `"   "` fail a `.min(1)`.
- **The schema is an `onDynamic` validator, never an `onSubmit` one.** `useAppForm` sets
  `validationLogic: revalidateLogic()` (validate on submit, revalidate on change), and that
  strategy runs **only** `onDynamic` — moving the schema back to `validators.onSubmit`
  silently validates nothing. Timing contract: `src/form/use-app-form.revalidate.test.tsx`;
  the `TOnDynamic` generic slot in `AppFormApi` must move with it.
- **`clearServerErrors()` runs before validation, not inside `onSubmit`.** `handleSubmit`
  aborts on `!isFieldsValid`, and nothing in `@tanstack/form-core` clears an `onServer` error
  by itself — a stale server field error would wedge every later submit. `AppForm`'s submit
  handler owns the call.
- **`UseAppFormOptions`' `TError` stays defaulted (`= unknown`) and inferred.** It sits
  contravariantly inside `UseMutationOptions`, so pinning it to one concrete type rejects
  every generated factory naming a different one. Never destructure or cast a generated
  `{operation}Mutation({ client })` to work around a type error — that is the bug.
- **`react/jsx-max-depth` is 2** and `pnpm lint` runs `--deny-warnings` — which is why
  `SelectField`'s portal tree is split into one component per nesting level.
  `unicorn/catch-error-name` requires catch parameters be named `error` outside tests.

## Lint

`.oxlintrc.json` here `extends` the root config and enables all six `wallow/*` rules —
config mechanics (extends semantics, directory-relative override globs, the shared
test/story override) are owned by `packages/lint/CLAUDE.md`; read it before editing.
Package-specific facts: `no-hand-rolled-mutation` matters most here — this layer owns the
`useMutation` every form submits through, and the one legitimate `mutationFn`
(`use-app-form.ts`'s escape-hatch stand-in) is a scoped override naming that single file.
`react/jsx-props-no-spreading` is off. The package is `private: true`, so it is not in
`scripts/check-exports.sh`'s package list (same as `packages/ui`).

## Scripts

```bash
pnpm --filter @bc-solutions-coder/forms build       # vite build (lib mode) && tsc -p tsconfig.build.json
pnpm --filter @bc-solutions-coder/forms test        # vitest run — node | browser
pnpm --filter @bc-solutions-coder/forms typecheck   # tsc --noEmit
```
