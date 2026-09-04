# packages/forms — @bc-solutions-coder/forms Agent Guide

Consumer-facing docs (authoring, testid derivation, error model, escape hatches):
`docs/development/forms.md`.

## Layering — one direction

`styles → ui → forms → apps`. `ui` must never import this package. Source imports
`@bc-solutions-coder/api-errors` only from `core/server-error.ts` (the brand check, the message
resolver, `splitFieldErrors`) and never imports `@bc-solutions-coder/sdk` — the SDK is a
devDependency that specs use to drive the real client.

**Never name `@tanstack/react-query`.** react-query arrives through the
`@bc-solutions-coder/query` facade so `useAppForm`'s `useMutation` and the host's
`QueryClientProvider` share one module instance — two copies means "No QueryClient set".
Specs too: build the `QueryClient` from the facade.

## Wiring invariants

- `core/form-hook.tsx` is the ONE sanctioned `core/` → `fields/` import — the single
  `createFormHook` call and its `fieldComponents` registry must sit together.
- `createFormHookContexts()` and `createFormHook()` are each called exactly once; a second
  call anywhere binds fields to a context no `AppForm` publishes.
- **The schema is an `onDynamic` validator, never `onSubmit`.** `validationLogic:
revalidateLogic()` runs ONLY `onDynamic` — moving the schema to `validators.onSubmit`
  silently validates nothing. The `TOnDynamic` generic slot in `AppFormApi` must move with
  it.
- **`clearServerErrors()` runs before validation, not inside `onSubmit`** — `handleSubmit`
  aborts on `!isFieldsValid` and form-core never clears an `onServer` error itself; a stale
  one would wedge every later submit.
- **`TError` stays defaulted (`= unknown`) and inferred** — it sits contravariantly inside
  `UseMutationOptions`; pinning it rejects every generated factory naming a different type.
  Never destructure or cast a generated `{operation}Mutation({ client })` around a type
  error — that is the bug.
- `use-app-form.ts`'s `mutationFn` is the no-mutation escape hatch itself — the single
  scoped `wallow/no-hand-rolled-mutation` override.

## Tests

- `*.test.ts` → node project; `*.test.tsx` → browser; never `*.ssr.test.tsx` here.
- **Append any new runtime to `formRuntime` in `vitest.config.ts`** — left undiscovered,
  Vite pre-bundles a second copy of React and specs die on a null-hook read. Base UI needs
  nothing: `baseUi` is the single glob `@base-ui/react/*`.
- **Real CSS is required, not cosmetic** — without the stylesheet `Checkbox.Root` measures
  0×0 and a spec that clicks it hangs until Playwright's actionability timeout.
