# Component composition, hook extraction, and forms adoption

**status: active**

A review of every component in `apps/wallow-auth` and `apps/wallow-web`, and the design for
reducing their size by composing what the workspace already provides rather than by inventing
new abstractions.

## What the review found

### `useEffect` is not the problem

Nine `useEffect` calls exist in the entire repo, every one a single call in its file:

| File | Verdict |
| --- | --- |
| `packages/ui/theme-provider` | Correct — subscribes to an external store |
| `packages/ui/focus-on-navigate` | Correct — imperative focus on route change |
| `packages/ui/ready-indicator` | Correct — paint signal for E2E |
| `packages/navigation/app-nav` | Correct |
| `apps/wallow-web/app/routes/bff-demo` | Correct — demo page |
| `wallow-auth/consent/ConsentScreen` | Correct |
| `wallow-auth/login/MagicLinkLoginForm` | Correct |
| `wallow-auth/mfa-enroll/MfaEnrollForm` | **Extract** — ref-guarded exchange-then-start |
| `wallow-auth/mfa-challenge/MfaChallengeForm` | **Extract** — refuse-and-navigate on verdict |

Only the last two are worth moving, and they move into hooks rather than disappearing. There is
no effect-soup to clean up.

### The size drivers are three separate things

1. **Prose.** `RegisterForm.tsx` is 763 lines; its header comment alone is 68, and oracle-port
   rationale runs through every function. Roughly 30–40% of the large auth files is comment.
2. **Hand-rolled form state beside an existing forms layer.** 11 screens hand-roll, 4 use
   `@bc-solutions-coder/forms`.
3. **An auth-screen skeleton nobody owns.** 11 local `CardHeading` functions, 11 hand-rolled
   footer links across 3 spellings, 9 local `Field`/`CodeField` wrappers, 4 copies of
   `ERROR_HREF`, 3 copies of `readMember`.

### The hand-rolled inventory

| Screen | Lines | State |
| --- | --- | --- |
| `wallow-auth/register/RegisterForm` | 763 | 7 `useState` |
| `wallow-auth/mfa-enroll/MfaEnrollForm` | 698 | 3 `useState` + 1 `useRef` |
| `wallow-auth/mfa-challenge/MfaChallengeForm` | 574 | 4 `useState` |
| `wallow-auth/login/LoginScreen` | 439 | 1 `useState` |
| `wallow-auth/accept-terms/AcceptTermsScreen` | 350 | 2 `useState` |
| `wallow-web/mfa/MfaSettingsSection` | 331 | 5 `useState` |
| `wallow-auth/login/OtpLoginForm` | 305 | 4 `useState` |
| `wallow-auth/login/MagicLinkLoginForm` | 263 | 3 `useState` |
| `wallow-web/mfa/MfaEnrollFlow` | 234 | 1 `useState` |
| `wallow-auth/login/PasswordLoginForm` | 197 | 3 `useState` |
| `wallow-auth/invitation/InvitationScreen` | 532 | — |

`ResetPasswordForm` (257 lines, comments included) is the migrated exemplar and the target shape.

## Design

Bottom-up across the four existing layers. Nothing new is introduced at the framework level.

### Layer 1 — `packages/ui`

Two additions. The catalog already has 58 components and is not the bottleneck; resist growth.

- **`CardHeader`** — title plus optional description, composing the existing `CardTitle` with
  `MutedText`. Replaces the inner markup of 11 local `CardHeading` functions.
- **`QuietLink`** — the muted footer link. 13 call sites currently spelled three ways
  (`hover:text-foreground`, `hover:text-primary`, one with `block text-center`).

Explicitly **not** in scope: wrapping `packages/ui/otp-field` for the code-entry screens. That
component exists and is unused, but adopting it changes the rendered DOM and the interaction
model — a UX change, not a refactor. It gets its own bead.

**Enforcement note.** Moving the `h2` inside `CardHeader` means screens stop writing
`<Text as="h2" variant="subheading">`, so `wallow/text-heading-variant` no longer sees those
call sites. The standard becomes guaranteed by construction rather than by lint, which is
stronger — but `packages/ui/CLAUDE.md` documents lint as the mechanism and must be updated to
say so.

### Layer 2 — `packages/forms`

No new API. The layer already covers what these screens hand-roll. Each migration deletes:

- the `<form onSubmit>` `preventDefault`/`stopPropagation` block — `AppForm` owns it
- the `pending ? "Verifying..." : "Verify"` button — `SubmitButton pendingLabel`
- the `ErrorBanner` wiring — `FormError`
- one `useState` per field

**The one gap, which stays a gap.** These auth endpoints return bare `{ succeeded, error }`
rather than RFC 7807, so `splitServerError` cannot read them. Each screen keeps its own
`failureMessage(cause)` mapper — genuine domain logic, not boilerplate. The narrower fix:
promote the `readMember` already exported from `login/auth-result.ts` into
`shared/lib/error-code.ts`, delete the three copies, and move each screen's mapper into its
feature's `api.ts` as a pure exported function.

### Layer 3 — `wallow-auth/src/shared/components/AuthScreen.tsx`

`Card` → `CardHeader` → error slot → children → footer. The skeleton all 16 screens open with.
App-specific composition, so it stays out of the catalog.

### Layer 4 — hooks

New hooks live in `features/<name>/hooks/`. **A hook used by more than one feature cannot**,
because `wallow/zone-dag` forbids feature-to-feature imports — those go in `shared/hooks/`.

| Hook | Home | Replaces |
| --- | --- | --- |
| `useReturnUrlGuard` | `wallow-auth/src/shared/hooks/` | 4 copies of `ERROR_HREF` and the refuse-and-navigate effect in 2 screens; used by 7 features |
| `useEnrollmentStart` | `features/mfa-enroll/hooks/` | the ref-guarded exchange-then-start effect |
| `useRedirectVerdict` | `features/mfa-challenge/hooks/` | the allow-list probe and its verdict effect |
| `useMfaSettings` | `wallow-web features/mfa/hooks/` | 5 `useState` and 3 mutations in one component |
| `useUserPicker` | `wallow-web features/organizations/hooks/` | the combobox open/blur state inside `MemberList` |

This removes both remaining app `useEffect`s.

### Comments

Condense to load-bearing why. Keep divergences from the oracle, security reasoning, and
non-obvious constraints. Drop line-by-line oracle narration and anything restating what the code
says.

## Sequencing

Bottom-up — each step makes the next smaller.

1. `packages/ui`: `CardHeader`, `QuietLink`
2. `wallow-auth`: `AuthScreen`, `shared/lib/error-code.ts`
3. `wallow-auth`: `shared/hooks/useReturnUrlGuard`
4. The 11 form migrations, one bead and one commit per screen, largest first
5. The remaining feature hooks in `wallow-web`

## Risks

- **E2E testids.** Every migrated screen must keep its exact `data-testid` values.
  `fieldTestId` kebab-derives from the field name (`newPassword` →
  `reset-password-new-password`); `ResetPasswordForm` already needed an explicit
  `testId="reset-password-confirm"` because the suite fills that name. Each migration diffs its
  testids against the specs rather than assuming derivation matches.
- **`dist/` resolution.** App specs resolve `@bc-solutions-coder/forms` and
  `@bc-solutions-coder/ui` from `dist/`. Any package change needs
  `pnpm --filter <pkg> build` before the app suites mean anything.
- **`react/jsx-max-depth` is 2** in `packages/forms` and both apps, with `--deny-warnings`. New
  composition must respect it.

## Gates

`pnpm check`, `./scripts/run-tests.sh`, and `./scripts/e2e.sh` — the last one matters more than
usual here, because testid preservation is the main correctness risk.
