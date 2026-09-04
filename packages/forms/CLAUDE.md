# packages/forms — @bc-solutions-coder/forms Agent Guide

Consumer-facing docs (authoring, testid derivation, error model, escape hatches):
`docs/development/forms.md`.

## Layering — one direction

`styles → ui → forms → apps`. `ui` must never import this package. Source imports
`@bc-solutions-coder/api-errors` runtime values only from `core/server-error.ts`
(`toApiFailure`, `splitFieldErrors`, and the deprecated helpers' brand check and resolver);
`form/use-app-form.ts` takes only types from it. The banner sentence comes from `ui`'s
`useFailureMessage`, never from a resolver call here, so the app registry reaches forms.
Source never imports `@bc-solutions-coder/sdk` — the SDK is a devDependency that specs use to
drive the real client.

## The failure path

Mechanics are in `docs/development/forms.md` § The error model; the invariants:

- **Never a joined string.** `splitSubmitFailure` (core) lands messages on the form's
  `defaultValues` keys and otherwise hands the whole `ApiFailure` to the banner; unmatched
  wording is not assembled into a sentence.
- **Resolve in the hook body, not the mutation callback.** `useAppForm` keeps `bannerFailure`
  in state and reads `useFailureMessage` where the `FailureMessagesProvider` context is
  reachable; a callback is not inside React.
- **Every mutation the hook creates carries `handledFailure(meta)`** over the caller's own
  `meta`, so the client's `onUnhandledFailure` never fires for a form. Specs build the client
  with `createQueryClient({ onUnhandledFailure })` and assert it.
- `errorText` and `splitServerError` are `@deprecated` and stay on the barrel only until their
  remaining call sites move. Do not add call sites.

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
