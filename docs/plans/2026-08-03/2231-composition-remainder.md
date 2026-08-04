# Component Composition — Remainder Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**status: active**

**Goal:** Finish the ten open beads under epic `Wallow-9n7a` — four screen migrations onto
`@bc-solutions-coder/forms`, one catalog addition, one hook extraction, one lint reconciliation,
and the doc sweep.

**Architecture:** This is a *continuation* of
`docs/plans/2026-08-03/1722-component-composition-implementation.md`, which stays the normative
source for the per-screen recipe and the facts-that-will-bite-you list. Nothing here restates
that recipe; every migration task below says "apply the Phase 3 recipe" and then records only
what is screen-specific. Phases 1, 2, 4 (Task 10) and migrations 9a–9g of the original plan are
**already landed** — this plan covers only what is left.

**Tech Stack:** React 19, TanStack Form + Query, zod, Base UI, CVA, Tailwind v4, Vitest browser
mode (real headless Chromium), Playwright, oxlint.

**Design:** `docs/plans/2026-08-03/1722-component-composition-design.md`

---

## Verified starting state (2026-08-03)

Landed, do not redo:

| Original task | Artifact | State |
| --- | --- | --- |
| 1 | `packages/ui/src/components/card/card-header.tsx` | exported from `src/index.ts:64` |
| 2 | `packages/ui/src/components/quiet-link/` | exported from `src/index.ts:302` |
| 3 | `apps/wallow-auth/src/shared/lib/error-code.ts` | exists |
| 4 | `apps/wallow-auth/src/shared/components/auth-screen.tsx` | exists |
| 5–6 | `shared/hooks/use-return-url-guard.ts` | exists |
| 7 | `features/mfa-challenge/hooks/use-redirect-verdict.ts` | exists |
| 8 | `features/mfa-enroll/hooks/use-enrollment-start.ts` | exists |
| 9a–9d, 9f | `RegisterForm` `MfaEnrollForm` `MfaChallengeForm` `InvitationScreen` `AcceptTermsScreen` | all on `useAppForm` |
| 9e | `LoginScreen` | composes `AuthScreen`; it is a shell with no form of its own, so it correctly has no `useAppForm` |
| 9g / 10 | `MfaSettingsSection` + `features/mfa/hooks/use-mfa-settings.ts` | on `useAppForm` |

Outstanding, and what this plan does:

| Bead | Work | Phase |
| --- | --- | --- |
| `Wallow-86os` | `NoticeBanner` in the ui catalog + 6 call sites | A |
| `Wallow-h1ju` | migrate `OtpLoginForm` (305) | B |
| `Wallow-mfjr` | migrate `MagicLinkLoginForm` (263) | B |
| `Wallow-5pyh` + `Wallow-6uqv` | migrate wallow-web `MfaEnrollFlow` (234) | B |
| `Wallow-mhsg` | migrate `PasswordLoginForm` (197) | B |
| `Wallow-r4az` | extract `useUserPicker` | C |
| `Wallow-8sao` | reconcile the `wallow/*` rule sets | D |
| `Wallow-4q0r` | update the invalidated guides | E |
| `Wallow-9n7a` | the epic — closes last | E |

---

## Ordering decision: NoticeBanner moves to the FRONT

`Wallow-86os` carries a note sequencing it **after** the screen migrations. That note was written
when all six of its call sites sat in unmigrated screens. Five of the six now sit in screens that
are **already** on the forms layer (`LoginScreen` ×3, `MfaEnrollForm`, `MfaChallengeForm`), so
they are pure div→component conversions no matter when they happen. The sixth
(`MagicLinkLoginForm`'s `SentAlert`) is a standalone component that the 9i migration does not
otherwise touch.

So the note's own goal — "the six call sites are converted once" — is now served *better* by
shipping the banner first: `SentAlert` is converted once in Phase A and the 9i commit leaves it
alone. Doing it last would mean 9i ports a hand-rolled div and Phase A immediately re-ports it.

**Action:** before starting, correct the bead note so the record matches.

```bash
bd note Wallow-86os "SEQUENCING SUPERSEDED: original note put this after 9f-9k. Five of the six call sites now sit in already-migrated screens, and the sixth (MagicLinkLoginForm's SentAlert) is a standalone component the 9i migration does not restructure. Shipping the banner FIRST converts each site exactly once. See docs/plans/2026-08-03/2231-composition-remainder.md."
```

---

## Phase A — the catalog gap

### Task A1: `NoticeBanner`

**Bead:** `Wallow-86os`

**Files:**
- Create: `packages/ui/src/components/notice-banner/notice-banner.tsx`
- Create: `packages/ui/src/components/notice-banner/notice-banner.styles.ts`
- Create: `packages/ui/src/components/notice-banner/index.ts`
- Create: `packages/ui/src/components/notice-banner/notice-banner.test.tsx`
- Create: `packages/ui/src/components/notice-banner/notice-banner.stories.tsx`
- Modify: `packages/ui/src/index.ts`
- Modify: `packages/ui/src/index.test.ts`

**The design decision, made here so the implementation does not have to re-litigate it:** this is
a **sibling component**, not a `tone` axis added to `ErrorBanner`. Read
`packages/ui/src/components/error-banner/error-banner.styles.ts` before starting — its `surface`
axis exists because a 10% destructive tint disappears on the inverted sidebar rail, and its
`sidebar` arm drops the tint for the full-strength token. That reasoning is specific to a message
the reader **must not** miss. All six notice call sites are on page/card surfaces and none is on
the rail. Folding a second axis into `ErrorBanner` would multiply `tone × surface` into four arms,
two of which have no call site and no reasoning behind them. A sibling with a `tone` axis and no
`surface` axis is the smaller, honest shape.

**Step 1: Write the failing test**

```tsx
// packages/ui/src/components/notice-banner/notice-banner.test.tsx
import { render, screen } from "@bc-solutions-coder/testing/browser";
import { expect, test } from "vitest";

import { NoticeBanner } from "./notice-banner";

test("renders its children as the message", () => {
  render(<NoticeBanner>Check your inbox.</NoticeBanner>);

  expect(screen.getByText("Check your inbox.")).toBeTruthy();
});

test("passes data-testid through to the wrapper", () => {
  render(<NoticeBanner data-testid="login-magic-link-sent">Sent.</NoticeBanner>);

  expect(screen.getByTestId("login-magic-link-sent")).toBeTruthy();
});

test("defaults to the success tone", () => {
  render(<NoticeBanner data-testid="n">Done.</NoticeBanner>);

  expect(screen.getByTestId("n").className).toContain("border-success");
});

test("takes the warning tone when asked", () => {
  render(
    <NoticeBanner tone="warning" data-testid="n">
      Enrol a factor.
    </NoticeBanner>,
  );

  expect(screen.getByTestId("n").className).toContain("border-warning");
});

test("merges a caller className over the recipe", () => {
  render(
    <NoticeBanner className="space-y-2" data-testid="n">
      Body.
    </NoticeBanner>,
  );

  expect(screen.getByTestId("n").className).toContain("space-y-2");
});

test("accepts arbitrary children so a caller can supply its own action row", () => {
  render(
    <NoticeBanner tone="warning" data-testid="n">
      <a href="/enrol">Enrol now</a>
    </NoticeBanner>,
  );

  expect(screen.getByRole("link", { name: "Enrol now" })).toBeTruthy();
});
```

**Step 2: Run it and watch it fail**

```bash
pnpm --filter @bc-solutions-coder/ui test -- notice-banner
```

Expected: FAIL — `Failed to resolve import "./notice-banner"`.

**Step 3: Write the recipe**

```ts
// packages/ui/src/components/notice-banner/notice-banner.styles.ts
import { cva, type VariantProps } from "class-variance-authority";

/*
 * The notice banner's class recipes — the non-destructive sibling of
 * `error-banner.styles.ts`. Style decisions live here and nowhere else; this
 * file holds no JSX and imports no React. Every utility is a semantic token
 * class from `@bc-solutions-coder/styles`; no raw colour values.
 *
 * Why a sibling component rather than a `tone` arm on `ErrorBanner`: that
 * component's `surface` axis exists to keep a message the reader MUST NOT miss
 * legible on the inverted sidebar rail, where a 10% tint disappears. A notice
 * is not that message and has no rail call site. Folding `tone` into
 * `ErrorBanner` would produce four `tone x surface` arms, two of them unreached
 * and unreasoned.
 */

/** The banner surface — the outer `<div>`, the only part a caller can style. */
export const noticeBannerRecipe = cva("rounded-md border p-3", {
  variants: {
    tone: {
      success: "border-success bg-success/10",
      warning: "border-warning bg-warning/10",
    },
  },
  defaultVariants: { tone: "success" },
});

/** The surface recipe's variant props, mixed into `NoticeBannerProps`. */
export type NoticeBannerRecipeProps = VariantProps<typeof noticeBannerRecipe>;
```

Note there is **no** text recipe here, and that is the deliberate difference from `ErrorBanner`.
`ErrorBanner` splits a second recipe onto an inner `<p>` so a caller override cannot reach the
message. A notice's body is not always one paragraph — `LoginScreen`'s warning banner carries a
heading plus an action link, which is why that call site spells `space-y-2` today — so
`NoticeBanner` takes arbitrary children and the caller composes `Text` inside it.

**Step 4: Write the component**

```tsx
// packages/ui/src/components/notice-banner/notice-banner.tsx
import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { noticeBannerRecipe, type NoticeBannerRecipeProps } from "./notice-banner.styles";

export interface NoticeBannerProps
  extends HTMLAttributes<HTMLDivElement>,
    NoticeBannerRecipeProps {}

/**
 * A non-destructive banner — a confirmation or a nudge. Sourced from six
 * hand-rolled wrappers across both apps that each rebuilt `ErrorBanner`'s shape
 * in a different tone.
 *
 * Unlike `ErrorBanner` this does NOT wrap its children in a styled `<p>`: a
 * notice body ranges from one sentence to a heading plus an action link, so the
 * caller composes `Text` inside it.
 */
export function NoticeBanner({ tone, className, ...rest }: NoticeBannerProps): ReactElement {
  return <div className={cn(noticeBannerRecipe({ tone }), className)} {...rest} />;
}
```

```ts
// packages/ui/src/components/notice-banner/index.ts
export { NoticeBanner, type NoticeBannerProps } from "./notice-banner";
export { noticeBannerRecipe, type NoticeBannerRecipeProps } from "./notice-banner.styles";
```

**Step 5: Write the story**

Copy the shape of `error-banner.stories.tsx` — one story per tone. Stories are this catalog's
render coverage, not an optional extra.

**Step 6: Wire the exports — both files in the same commit**

`packages/ui/src/index.ts`: export `NoticeBanner` and `NoticeBannerProps` from
`./components/notice-banner`, keeping the file's alphabetical block order. Per
`packages/ui/CLAUDE.md`'s barrel rule the recipe stays reachable through the
`@bc-solutions-coder/ui/notice-banner` subpath alone, not the root barrel.

`packages/ui/src/index.test.ts`: add `"NoticeBanner"` to `PUBLIC_RUNTIME_EXPORTS` and
`NoticeBannerProps` to the `PublicTypeExports` tuple. **Growing one alone turns the other red** —
that is the two-file pinning rule, and it is why this is one commit.

**Step 7: Run tests**

```bash
pnpm --filter @bc-solutions-coder/ui test
```

Expected: PASS, including `src/index.test.ts`.

**Step 8: Commit**

```bash
git add packages/ui/src
git commit -m "feat(ui): add NoticeBanner for success and warning notices"
```

---

### Task A2: convert the six call sites

**Bead:** `Wallow-86os` (close after this task)

**Files:**
- Modify: `apps/wallow-auth/src/features/login/components/LoginScreen.tsx:90,117,131`
- Modify: `apps/wallow-auth/src/features/login/components/MagicLinkLoginForm.tsx:114`
- Modify: `apps/wallow-auth/src/features/mfa-enroll/components/MfaEnrollForm.tsx:262`
- Modify: `apps/wallow-auth/src/features/mfa-challenge/components/MfaChallengeForm.tsx:112`

**Step 1: Record the testid contract before changing anything**

These four testids are E2E contract and must survive byte-identical:

```
login-password-reset-notice
login-signed-in
login-mfa-enrollment-banner
login-magic-link-sent
mfa-challenge-success
```

`MfaEnrollForm.tsx:262`'s backup-codes panel carries no testid — check before you assume, and if
one is there, add it to this list.

```bash
grep -rn "data-testid" apps/wallow-auth/e2e/ | grep -E 'notice|signed-in|enrollment-banner|magic-link-sent|challenge-success'
```

**Step 2: Convert each site**

Each becomes `<NoticeBanner tone="…" data-testid="…">` with the existing children unchanged. Three
things to keep:

- `LoginScreen.tsx:90` is the warning tone and keeps its `className="space-y-2"` — it holds a
  heading plus an action link, and the recipe deliberately owns no vertical rhythm.
- Every other site is `tone="success"`, which is the default, so **omit the prop** rather than
  spelling it. A default that is never exercised is a default nobody trusts.
- The comments above `SentAlert` (`MagicLinkLoginForm.tsx:100-111`) and the enrollment banner
  explain anti-enumeration and design provenance. Those are reasoning the code does not state —
  keep them. Drop only prose describing the div's classes, which no longer exist here.

**Step 3: Verify**

```bash
pnpm --filter ./apps/wallow-auth test
pnpm lint && pnpm lint:tests
```

Expected: PASS. If a screenshot spec under `__screenshots__/` moves, inspect the diff before
accepting it — the tint values are identical, so a changed pixel means a changed structure.

**Step 4: Commit**

```bash
git add apps/wallow-auth/src
git commit -m "refactor(wallow-auth): move the six notice wrappers onto NoticeBanner"
bd close Wallow-86os
```

---

## Phase B — the four remaining screen migrations

Apply the **Phase 3 per-screen recipe** from
`docs/plans/2026-08-03/1722-component-composition-implementation.md` (Steps 1–8) for each. Do not
re-derive it. `ResetPasswordForm.tsx` remains the reference shape; `AcceptTermsScreen.tsx` is the
most recently migrated example and the closest to current house style.

Recipe Step 1 is non-negotiable and comes first every time: **record the `data-testid` contract
into the bead before changing anything.** That list is the acceptance criterion.

One screen per commit, largest first.

### Task B1: `OtpLoginForm` (305 lines)

**Bead:** `Wallow-h1ju`

**Files:**
- Modify: `apps/wallow-auth/src/features/login/components/OtpLoginForm.tsx`
- Modify: `apps/wallow-auth/src/features/login/components/OtpLoginForm.test.tsx`
- Modify: `apps/wallow-auth/src/features/login/api.ts` (the failure mapper lands here)

**Screen-specific risk:** two `<form>` elements in one component — request, then verify — driven
by four `useState` (`email`, `code`, `sent`, `rememberMe`) and two mutations
(`accountSendOtpMutation`, `accountVerifyOtpMutation`).

That is **two `useAppForm` instances**, not one form with a branch. The request form owns `email`;
the verify form owns `code` and `rememberMe`. `sent` stays a `useState` in the component — it is
the branch between them, not a field of either.

Why two rather than one: a single form would need `code` optional-then-required, so the schema
would have to encode the phase, and the request submit would have to skip validation of a field
the user has not been shown. Two schemas each stay valid on their own.

Carry `email` from the request form into the verify submit by reading it off the request form's
state — the verify call needs both, and re-entering the address is not the oracle's behaviour.

### Task B2: `MagicLinkLoginForm` (263 lines)

**Bead:** `Wallow-mfjr`

**Files:**
- Modify: `apps/wallow-auth/src/features/login/components/MagicLinkLoginForm.tsx`
- Modify: `apps/wallow-auth/src/features/login/components/MagicLinkLoginForm.test.tsx`
- Modify: `apps/wallow-auth/src/features/login/components/MagicLinkLoginForm.verify.test.tsx`

**Screen-specific risk:** **KEEP its `useEffect`.** It is correct and it is not what this
migration is for — the effect handles arrival from the emailed link (`token` prop), which is not
a form submission at all. Only the *send* half becomes a `useAppForm`: one field (`email`), one
mutation (`accountSendMagicLinkMutation`), and the `sent` flag that swaps the form for
`SentAlert`.

`SentAlert` is already on `NoticeBanner` after Phase A — leave it alone.

The anti-enumeration constant `MAGIC_LINK_SENT_MESSAGE` and its comment are load-bearing: the API
answers `200 { succeeded: true }` for an unknown address on purpose, and the confirmation is the
artefact both outcomes share. It must stay byte-identical across both branches. Do not let a
`fallbackError` introduce a second, distinguishable failure path for the send.

### Task B3: `MfaEnrollFlow` (234 lines, wallow-web)

**Beads:** `Wallow-5pyh` **and** `Wallow-6uqv` — these overlap and close together.

**Files:**
- Modify: `apps/wallow-web/src/features/mfa/components/MfaEnrollFlow.tsx`
- Modify: its co-located spec

**First, record `Wallow-6uqv`'s decision, because it is already made.** That bead asks whether the
input-with-action shape gets a new catalog primitive in `packages/forms` or stays hand-rolled.
`MfaSettingsSection` — the other panel it names — was migrated onto plain
`useAppForm`/`AppForm`/`SubmitButton` in commit `bab9432b` with no new primitive. That answered
it: **no new primitive; the existing forms layer covers the shape.** An input glued to an action
button *is* a one-field form, and the confirm step submits a code exactly like a form does.

```bash
bd note Wallow-6uqv "DECISION: no new packages/forms primitive for the input-with-action shape. MfaSettingsSection (bab9432b) migrated onto plain useAppForm/AppForm/SubmitButton and needed nothing new — a one-field panel that submits is a form. MfaEnrollFlow follows the same route under Wallow-5pyh."
```

**Screen-specific risk:** five `useState` (`secret`, `qrUri`, `code`, `backupCodes`, `error`) and
two mutations. Only `code` is a form field. The other four are **flow state** across a three-way
render branch (backup codes > secret > intro) and stay `useState`.

`enroll` fires from a button, not a form — it takes `{}` and mints the secret. Leave it a
`useMutation`. Only `confirm` becomes the `useAppForm` mutation, and it is passed **whole**:

```ts
// already correct at MfaEnrollFlow.tsx:131 — preserve this spread and its onSuccess
{
  ...mfaConfirmEnrollmentMutation({ client: sdk.client }),
  onSuccess: () => { /* invalidate mfaGetStatus */ },
}
```

Preserve exactly: the invalidation targets the `mfaGetStatus` operation via
`queriesForOperation(mfaGetStatusQueryKey(…))`, **not** the `Identity` tag — the comment at
`:126-129` explains that the tag is far broader than this write touches, and that reasoning is not
recoverable from the code.

Do not regress the closed MFA oracles under `apps/wallow-web/src/features/mfa/`
(`Wallow-6uqv`'s explicit acceptance criterion).

**Commit and close both:**

```bash
git commit -m "refactor(mfa): migrate MfaEnrollFlow onto the forms layer"
bd close Wallow-5pyh Wallow-6uqv
```

### Task B4: `PasswordLoginForm` (197 lines)

**Bead:** `Wallow-mhsg`

**Files:**
- Modify: `apps/wallow-auth/src/features/login/components/PasswordLoginForm.tsx`

**Screen-specific risk:** the easiest of the four. Three local field wrappers collapse to two
`TextField`s and one `CheckboxField` (`email`, `password`, `rememberMe`).

The `rememberMe` checkbox is why the stylesheet matters: **a browser spec that clicks it needs
real CSS or `Checkbox.Root` measures 0×0 and the click hangs to Playwright's timeout** (facts list
item 4 in the original plan).

The file's header comment at `:25` already states that this app hand-rolls no `mutationFn` — after
this migration that sentence is finally true of this file too. Keep it.

### Phase B gate

After all four, before moving on:

```bash
pnpm --filter ./apps/wallow-auth test
pnpm --filter ./apps/wallow-web test
pnpm lint && pnpm lint:tests
./scripts/e2e.sh
```

The E2E run is the one that proves the testid contracts held. Do not defer it past this point —
four screens' worth of drift is much harder to bisect than one.

---

## Phase C — the last hook

### Task C1: `useUserPicker`

**Bead:** `Wallow-r4az` (original Task 11)

**Files:**
- Create: `apps/wallow-web/src/features/organizations/hooks/use-user-picker.ts`
- Create: `apps/wallow-web/src/features/organizations/hooks/use-user-picker.test.tsx`
- Modify: `apps/wallow-web/src/features/organizations/components/MemberList.tsx`

Feature-local, not `shared/hooks/` — one feature uses it, and `apps/CLAUDE.md`'s convention puts a
single-feature hook beside its feature.

**What it absorbs:** the directory query at `MemberList.tsx:211`
(`useQuery(usersGetUsersOptions({ client }))`) and the `requestedOpen` combobox state at
`MemberList.tsx:395`, plus the `matches` filter currently memoised inside `UserIdPicker`.

Those two live in **different components today** — the query is in the add-member form, the open
state is in `UserIdPicker`, and `users` is threaded down as a prop (`:250`). Collapsing both into
one hook is the point: `UserIdPicker` calls the hook itself and the prop disappears.

**Step 1: Write the failing test**

Render a probe through the app's test harness and assert:

- it returns the directory's users once the query resolves
- `matches` filters by email substring, case-insensitively, against the trimmed value
- `open` is false when `requestedOpen` is false
- `open` is false when `requestedOpen` is true but `matches` is empty — this is the live
  behaviour at `:412` (`requestedOpen && matches.length > 0`) and dropping it pops an empty
  listbox

**Step 2: Run it and watch it fail**

```bash
pnpm --filter ./apps/wallow-web test -- use-user-picker
```

**Step 3: Implement, then rewire `UserIdPicker`**

Keep the comment block at `MemberList.tsx:380-386` — it explains that `Field.Root` publishes the
label id, the description ids and the invalid flag through context, and that `Autocomplete.Input`
reads all three, which is why the accessible name and `aria-describedby` chain land correctly
without being spelled. That is non-obvious Base UI behaviour and it survives the extraction.

Keep the `organization-member-userid` and `organization-member-userid-error` testids.

**Step 4: Verify and commit**

```bash
pnpm --filter ./apps/wallow-web test
pnpm lint && pnpm lint:tests
git commit -m "refactor(organizations): extract useUserPicker from MemberList"
bd close Wallow-r4az
```

---

## Phase D — lint reconciliation

### Task D1: reconcile the `wallow/*` rule sets

**Bead:** `Wallow-8sao`

This bead's inventory is **stale in one direction and resolved in another** — re-measure before
acting. Current state, verified:

```
wallow-web:  no-sidebar-inversion, no-source-tests, no-tinted-text, text-heading-variant, zone-dag
wallow-auth: no-hand-rolled-mutation, no-sidebar-inversion, no-source-tests, text-heading-variant, zone-dag
```

So `wallow-web` has **since gained** `no-tinted-text` and `text-heading-variant` (with a
`LandingPage` override and a `bff-demo` opt-out). Three gaps remain.

**D1a — `no-tinted-text` in `wallow-auth`.** Still off, and three files carry the exact spelling it
bans:

```
apps/wallow-auth/src/features/not-found/components/NotFoundPage.tsx:60
apps/wallow-auth/src/features/access-request/components/AccessRequestPage.tsx:43
apps/wallow-auth/src/features/error/components/ErrorPage.tsx:108
```

All three are `className="text-sm font-medium text-primary hover:text-primary/80"` on a link.
`Wallow-mlc3` deleted that same spelling from the catalog's link button variant, so these three
now hand-roll a treatment the catalog deliberately dropped. **Fix them, do not exempt them** —
move each onto `Button variant="link"` (or `hover:underline` if the element must stay an anchor
for routing), then enable the rule.

Note that `bg-success/10` in `NoticeBanner` is a translucent *surface*, not tinted *text* — it
stays legal, as `apps/CLAUDE.md` records for the drawer scrim. Confirm the rule agrees before
assuming it.

**D1b — `no-hand-rolled-mutation` in `wallow-web`.** The bead calls this "unmeasured". It is now
measured: after Phase B there is **no `mutationFn` property anywhere in
`apps/wallow-web/src`** — the only occurrences are the word inside comments. Enabling it is a
pure guard with zero fixes, so turn it on and record that it was vacuous at the time.

**D1c — `text-heading-variant` in `wallow-web`.** Already enabled; the bead's claim that it is off
is stale. Verify the `LandingPage` override still reads `h1: display, h2: title, h3: subheading`
and that the `bff-demo` opt-out is still justified, then say so in `packages/lint/CLAUDE.md`. No
code change expected.

**D1d — `minimal-app`.** Still has no nested `.oxlintrc.json`, so it registers no `jsPlugins` and
every `wallow/*` rule passes vacuously over it — the same defect `Wallow-mlc3` fixed for the three
packages. It renders UI, and an example that violates the catalog rules teaches the violation.

**Decision to make and record:** give it a nested config. It is the fork-facing example; being
outside the gate is worse there than anywhere else. Enable the same set the two apps share, run
`pnpm lint`, and fix what fires. If something genuinely cannot hold — the app is deliberately
minimal and may legitimately not use the catalog everywhere — record *that specific exemption*
with its reason rather than skipping the config.

**Step: record every divergence**

`Wallow-8sao`'s acceptance is that each config's rule set is either identical to the others or
**differs for a reason written down in `packages/lint/CLAUDE.md`**. After the above, write that
file's reconciliation section: which rule is off where, and why. `wallow-auth` forbidding raw
`<button>` while `wallow-web` cannot (because `bff-demo` ships four un-catalogued ones) is an
existing example of the shape that section should take.

**Verify and commit**

```bash
pnpm lint && pnpm lint:tests
```

Expected: PASS, **with no rule blanket-disabled** — that is in the acceptance criteria.

```bash
git commit -m "chore(lint): reconcile the wallow/* rule sets across the apps"
bd close Wallow-8sao
```

---

## Phase E — documentation, and closing out

### Task E1: update the invalidated guides

**Bead:** `Wallow-4q0r` (original Task 12)

Read `docs/CLAUDE.md` before touching anything under `docs/` — it is a DocFX site and `toc.yml`
owns navigation.

**Files:**

- `packages/ui/CLAUDE.md` — currently mentions **neither** `CardHeader` nor `QuietLink` (verified:
  zero occurrences) and now also needs `NoticeBanner`. Document all three. Say that the card
  `<h2>` is now guaranteed by `CardHeader`'s construction rather than only by
  `wallow/text-heading-variant` catching each call site. For `NoticeBanner`, record why it is a
  sibling of `ErrorBanner` rather than a `tone` axis on it — the Phase A reasoning, condensed.
- `packages/forms/CLAUDE.md` — the original plan flags a stale `dist/`-resolution claim.
  **Verify only**; the plan notes it was already removed. If it is still there, delete it: in-repo
  the `exports` map points at `src/`, and `publishConfig` swaps `dist/` in at pack time.
- `apps/CLAUDE.md` — add the hooks convention, which now has five instances backing it: feature
  hooks in `features/<name>/hooks/`, cross-feature hooks in `shared/hooks/`, **because
  `wallow/zone-dag` forbids feature-to-feature imports**. The rule is the reason; state it.
- `packages/lint/CLAUDE.md` — the reconciliation section from Task D1. (Written there, not here.)
- `docs/development/forms.md` — the migrated screen list. All 11 are done at this point; the guide
  currently has no such list.
- `docs/plans/2026-08-03/1722-component-composition-design.md` — mark `completed`.
- `docs/plans/2026-08-03/1722-component-composition-implementation.md` — mark `completed`.
- `docs/plans/2026-08-03/2231-composition-remainder.md` — this file, mark `completed`.

Per `CLAUDE.md`, plans are marked in place, never archived out of the repo — open beads cite them
by path.

```bash
git commit -m "docs: record the composition work's catalog additions and conventions"
bd close Wallow-4q0r
```

### Task E2: close the epic

```bash
bd show Wallow-9n7a          # confirm no child is still open
bd close Wallow-9n7a
```

---

## Final gate

```bash
pnpm check
./scripts/run-tests.sh
./scripts/e2e.sh
```

`pnpm check` is the full chain (format, both lint passes, manifests, deps, env, build, typecheck,
test, check:exports). `./scripts/run-tests.sh` covers the backend — nothing here touches it, but
it is the standing gate and a green run is cheap.

Then:

```bash
git pull --rebase && bd dolt push && git push
git status                                   # expect "up to date with origin"
git ls-remote origin refs/dolt/data          # the hash MUST have changed
```

`bd dolt push` is not optional and `git push` does not do it — it is the only thing that moves the
bead closures off this machine.
