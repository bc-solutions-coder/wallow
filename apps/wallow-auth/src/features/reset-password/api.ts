/**
 * Reset-password feature `api.ts` — a THIN RE-EXPORT SEAM over the RAW SDK barrel
 * (`@bc-solutions-coder/sdk`), added by Wallow-x4qn.9.4.
 *
 * READ THIS BEFORE "FIXING" IT. Every other seam in this app re-exports GENERATED
 * `{op}Mutation()` / `{op}Options()` factories from `@bc-solutions-coder/sdk/query`.
 * This one re-exports a raw operation, and that is deliberate: `ResetPasswordForm`
 * uses `@bc-solutions-coder/forms`' NO-MUTATION escape hatch and calls
 * `accountResetPassword` directly inside `useAppForm`'s `onSubmit`, because it owns
 * its own status-code branching — an expired or already-redeemed token is not a
 * form error to render beside a field, it is a different screen. Wallow-x4qn.9.3
 * excluded this screen from the mutation conversion for exactly that reason. The
 * generator DOES emit `accountResetPasswordMutation` — it emits one for every non-GET
 * operation, whether or not anything calls it — so the factory is always one import
 * away, and NOT ADOPTING IT IS THE POINT. `api.test.ts` beside this file pins that:
 * the factory exists on the query entry, and neither this seam nor the screen behind
 * it names it.
 *
 * So the seam still holds — `api.ts` is the feature's only data import — while what
 * sits behind it is the raw POST that spends the single-use token. The operation
 * takes `{ client }` like the generated factories do: read the request-scoped
 * instance off the router context (`useRouteContext({ from: "__root__" }).sdk`).
 */
export { accountResetPassword } from "@bc-solutions-coder/sdk";
