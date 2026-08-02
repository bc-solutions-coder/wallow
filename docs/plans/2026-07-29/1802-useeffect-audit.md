**status: active**

# useEffect audit — every effect in the workspace, and what replaces it

Scope: all of `apps/` + `packages/` (excluding `node_modules/`, `dist/`). **10 `useEffect`
call sites total — 8 in production code, 2 in fixtures.** No `useLayoutEffect` anywhere.

Verdict up front: **6 of the 8 production effects should go.** Two are legitimate and stay.
None of the removals need new machinery — every replacement is a pattern this repo already
uses and already has tests for.

---

## Inventory

| # | Site | What the effect does | Verdict |
|---|------|----------------------|---------|
| 1 | `apps/wallow-auth/src/features/consent/components/ConsentScreen.tsx:324` | navigate to `/error` when `returnUrl` is unsafe | **remove** → route `beforeLoad` |
| 2 | `apps/wallow-auth/src/features/mfa-enroll/components/MfaEnrollForm.tsx:490` | returnUrl guard **+** sequence token-exchange → enrollment-start, behind a `useRef` latch | **remove** → `beforeLoad` + composition |
| 3 | `apps/wallow-auth/src/features/mfa-challenge/components/MfaChallengeForm.tsx:433` | navigate to `/error` when the allow-list verdict is "refuse" | **remove** → `beforeLoad` + `ensureQueryData` |
| 4 | `apps/wallow-auth/src/features/login/components/MagicLinkLoginForm.tsx:166` | redeem a one-time magic-link token on mount, behind a `useRef` latch | **remove** → `useQuery` (dedupe replaces the latch) |
| 5 | `apps/wallow-web/src/routes/bff-demo.tsx:84` | fetch `getUser()` on mount into two `useState`s, with a `cancelled` flag | **remove** → `useQuery` |
| 6 | `apps/wallow-web/src/components/DashboardNav.tsx:233` | global `keydown` listener to close the mobile drawer on Escape | **remove** → catalog `Drawer` |
| 7 | `packages/ui/src/components/focus-on-navigate/focus-on-navigate.tsx:46` | move focus to `<h1>` after a committed navigation | **keep** |
| 8 | `packages/ui/src/components/ready-indicator/ready-indicator.tsx:34` | stamp `data-app-ready` on `document.body` post-commit | **keep** |

Fixtures, out of scope: `packages/ui/src/components/toast/toast.stories.tsx:51` (a story that
fires a toast on mount) and `packages/testing/src/render-with-wallow.test.tsx:54` (a probe
component asserting the harness mounts). Both are test scaffolding, not shipped behaviour.

`apps/wallow-web/src/lib/use-is-desktop.ts` already uses `useSyncExternalStore` — it is the
model for any future "subscribe to a browser API" need, and no new effect should be written
for that shape.

---

## The dominant anti-pattern: navigation decided in an effect

Three of the eight effects (#1, #2, #3) are the same shape:

```tsx
useEffect(() => {
  if (returnUrlIsUnsafe) void navigate({ href: ERROR_HREF });
}, [returnUrlIsUnsafe, navigate]);

if (returnUrlIsUnsafe) return null;   // ...and a matching render-nothing branch
```

Each costs **an effect + a dead render branch + a paint of nothing** before the redirect
commits, and on the server it renders a 200 with an empty body instead of a redirect.

This repo already decided against that shape and wrote the reasoning down:
`apps/wallow-auth/src/routes/index.test.tsx:71` — *"redirects from beforeLoad so the server
emits a real HTTP redirect"* — with `apps/wallow-web/src/routes/index.tsx:49` as the worked
example (`await ensureCurrentUser(...)` then `throw redirect(...)`). The router context
already carries `{ queryClient, sdk }` (`apps/wallow-auth/src/router.tsx:41`), so
`beforeLoad` has everything these guards need.

`consent.tsx`'s header argues against putting the guard in `validateSearch` — and it is right,
because a throwing `validateSearch` cannot land the user on `/error?reason=invalid_redirect_uri`.
**`beforeLoad` is not `validateSearch`.** It can `throw redirect({ href: ERROR_HREF })`, which
produces exactly the specified outcome, on both the server and the client. The refuse-don't-
sanitize contract (`returnurl-guard-refuse-dont-sanitize`) is preserved verbatim.

### 1. ConsentScreen

```tsx
// routes/consent.tsx
export const Route = createFileRoute("/consent")({
  validateSearch,
  beforeLoad: ({ search }) => {
    if (search.returnUrl !== undefined && !isSafeReturnUrl(search.returnUrl)) {
      throw redirect({ href: ERROR_HREF });
    }
  },
  component: ConsentRoute,
});
```

Deletes from `ConsentScreen.tsx`: the `useEffect`, the `returnUrlIsUnsafe` local, the
`returnUrlIsUnsafe` prop on `ConsentState`, its `if (returnUrlIsUnsafe) return null` branch,
and the `&& !returnUrlIsUnsafe` half of the query's `enabled`. The screen stops knowing about
`/error` at all. Six-prop `ConsentState` drops to five and its ordering comment gets shorter.

### 2. MfaEnrollForm — the guard half

Same edit against `routes/mfa/enroll.tsx`. Removes the guard branch from the mount effect and
the `if (returnUrlIsUnsafe) return null` at line 603.

### 3. MfaChallengeForm — the async guard

This one's verdict depends on a network probe, which is precisely what `beforeLoad` +
`ensureQueryData` is for:

```tsx
beforeLoad: async ({ context, search }) => {
  const local = localDecisionOf(search.returnUrl, /* isRelativeSafe */ ...);
  if (local === "refuse") throw redirect({ href: ERROR_HREF });
  if (local === "ask") {
    const body = await context.queryClient.ensureQueryData(
      accountValidateRedirectUriOptions({ client: context.sdk.client, ...validateRedirectUriArgs(...) }),
    );
    if (!isRedirectUriAllowed(body)) throw redirect({ href: ERROR_HREF });
  }
},
```

This is the biggest single win in the audit. It deletes the effect, the `ReturnUrlVerdict`
type, `verdictOf()`, the `"pending"` state, and the `if (returnUrlVerdict !== "accept") return
null` branch — because a route that got past `beforeLoad` is, by construction, accepted. The
component keeps `useQuery` for the same key (a cache hit, no second request) only where
`redirect()` still needs `validation.data === true` to mint the `AllowListedReturnUrl`.

`localDecisionOf` / `isRedirectUriAllowed` are already pure module functions — they move to a
sibling module and are imported by both the route and the screen. No logic is rewritten.

**Tradeoff, stated plainly:** the probe moves ahead of first paint, so an absolute-returnUrl
challenge blocks on it instead of rendering nothing while it resolves. Today the screen
already renders nothing in that window, so nothing is lost visually — but it does become
server-side latency on that one path. `__root.tsx:117` already ships a branded pending
component for exactly this.

---

## 4. MagicLinkLoginForm — a `useRef` latch fighting StrictMode

```tsx
const verifyStartedRef = useRef(false);
useEffect(() => { if (verifyStartedRef.current) return; ... }, [token, onAuthResult, onError, verifyMutation]);
```

The file's own comment names the problem: `verifyMutation` is a fresh object every render, so
without the latch the effect re-fires forever and redeems a one-time token in a loop. The
latch is a workaround for a dependency array that cannot be made correct.

**Replace with `useQuery`.** React Query's in-flight dedupe and cache are a *stronger*
exactly-once guarantee than a ref — a ref is per-mount and StrictMode remounts; a query key is
not:

```tsx
const verification = useQuery({
  queryKey: ["magic-link-verify", token],
  queryFn: async () => await accountVerifyMagicLink({ client: sdk.client, query: { token } }),
  enabled: token !== undefined && token !== "",
  retry: false,
  staleTime: Infinity,
  gcTime: Infinity,
  refetchOnMount: false,
  refetchOnWindowFocus: false,
  refetchOnReconnect: false,
});
```

...then report up from `verification.data` / `verification.error` rather than from mutation
callbacks. Deletes the effect, the ref, and the whole "known tradeoff" paragraph.

Two notes:

- A POST in a `useQuery` reads oddly. It is defensible here — this is a *fetch-on-arrival*,
  not a user-initiated write, and every one of the options above exists to say "once, ever."
  The alternative framing (a route `loader`) is **not** recommended: the loader runs during
  SSR, and this call's whole purpose is to get a `Set-Cookie` into the *browser's* jar.
- The exactly-once test stays behavioural, exactly as the current comment insists.

## 5. bff-demo.tsx — the textbook case

`useEffect` + `let cancelled = false` + two `useState`s to fetch on mount. This is the
canonical "you might not need an effect":

```tsx
const session = useQuery({
  queryKey: ["bff", "user"],
  queryFn: async () => {
    const user = await getUser();
    setCsrfToken(user === null ? null : (typeof user.csrfToken === "string" ? user.csrfToken : null));
    return user;
  },
});
const status = session.data == null ? "anonymous" : "authenticated";
const email  = session.data == null ? "" : (session.data.email ?? session.data.sub ?? "");
```

Deletes the effect, the cancel flag, and both `useState`s; `status`/`email` become derived
values, which is what they always were. Arming the CSRF interceptor stays inside the fetch
because it *is* part of fetching the session. The `bff-*` testid contract is untouched.

While here: `apiResult`/`mutateResult` are two `useState`s driven by hand-rolled
`try/catch` + a `"…"` sentinel. Both are `useMutation`s waiting to happen — `isPending`
replaces the sentinel and `error` replaces the catch. Same refactor `MfaEnrollForm` already
took under Wallow-evd5.3.2.

## 6. DashboardNav — hand-rolled dialog behaviour

```tsx
useEffect(() => {
  const onKeyDown = (e: KeyboardEvent) => { if (isMobileNavOpen && e.key === "Escape") closeMobileNav(); };
  globalThis.addEventListener("keydown", onKeyDown);
  return () => globalThis.removeEventListener("keydown", onKeyDown);
}, [isMobileNavOpen, closeMobileNav]);
```

A global keydown listener is a legitimate *use* of an effect — but it should not exist here at
all, because the catalog already ships the component that owns this behaviour.
`packages/ui/src/components/drawer/` wraps Base UI's `Drawer` (17 parts, `Root`/`Portal`/
`Backdrop`/`Popup`/…), which brings Escape dismissal, backdrop dismissal, **focus trap**,
scroll lock, and `aria-modal` — none of which the hand-rolled drawer has today.

Swapping `NavDrawer` onto `Drawer.Root` (open bound to `isMobileNavOpen`, `onOpenChange` to
`closeMobileNav`) deletes:

- this effect,
- `NavBackdrop` in `DashboardLayout.tsx:86` — a full-screen `<button>` acting as a backdrop,
  which is an a11y smell in its own right (it is announced as a button and is in the tab order),
- the `showBackdrop` derivation at `DashboardLayout.tsx:102`.

It also **fixes real bugs**: today, opening the mobile drawer leaves focus behind it, Tab
walks into the page underneath, and the page still scrolls. This is the highest
user-visible-value item in the audit.

## 7 & 8 — keep

`FocusOnNavigate` and `ReadyIndicator` are the two effects that are *correct*. Both
synchronize with an external system (DOM focus; `document.body` attributes), both genuinely
need post-commit timing, and neither has a non-effect equivalent. `ReadyIndicator`'s timing is
load-bearing for the whole E2E suite (`.claude/rules/E2E.md` readiness). Leave both alone.

---

## Prop drilling / composition findings

The user's second ask. These are independent of the effects and can land separately.

### A. `MfaEnrollForm` — split the screen, kill the sequencing effect

`renderBody({ backupCodes, secret, qrUri, code, loading, pending, onCodeChange, onSubmit,
onDone, onBeginSetup })` (line 641) is a **ten-key props bag** threaded into a render function
that immediately branches on three of them. That is a state machine flattened into arguments.

Composition fixes both the threading and effect #2's remaining half. The mount effect exists
only to sequence *exchange → start*. Make that ordering **structural** instead of temporal:

```
<MfaEnrollRoute>
  └─ <EnrollmentGate enrollToken>        // owns the exchange; renders children only once it resolves
       └─ <EnrollmentStart>              // owns mfaEnrollTotp; renders its child with the secret
            └─ <ConfirmEnrollment secret> // owns confirm + backup codes
```

A child that does not exist until its parent's call resolved cannot fire out of order — no
effect, no `startedRef`, no `exchanging` `useState` (line 445), no `loading: exchanging ||
startEnrollment.isPending` union. Each layer keeps one mutation and passes exactly what it
minted. The 679-line file becomes three ~150-line ones with no props bag.

Note: `MfaEnrollForm.start-mutation.test.ts` is a **source-reading structural spec** that
greps this exact file for `useMutation`/`try`/`setLoading`/`useCallback`. It will need
retargeting as part of the split — expect it to red, and update it deliberately rather than
working around it.

### B. `LoginScreen` → `LoginTabs` → three panels

`LoginTabs` (line 193) takes **seven props and uses one of them** (`activeTab`); the other six
are pure pass-through to the panels. `onAuthResult`/`onError` in particular are shell-owned
and wanted by all three panels — the definition of what context is for.

Two options, in order of preference:

1. **Pass the panels as children.** `LoginScreen` composes `<MagicLinkLoginForm …/>` at the
   level where `handleAuthResult` and `setErrorMessage` already live, and `LoginTabs` becomes
   a dumb strip taking `activeTab`, `onSelect`, and the panel elements. Zero new abstractions.
2. **A `LoginShellContext`** providing `{ onAuthResult, onError }`, consumed by each panel via
   a `useLoginShell()` hook. Better if the panel count grows.

**Not zustand.** This state is per-screen and dies with the screen; a global store would
outlive the mount and leak one login attempt's banner into the next. The existing `ui-store`
boundary comment is right and should be quoted at anyone tempted: zustand is for UI state
shared by components that *cannot* pass props (the nav rail and its controls are siblings),
not for a parent talking to its own children.

### C. `ConsentScreen` → `ConsentState`

Six props, five of which are booleans/nullables that encode one state machine
(`clientIsKnown`, `returnUrlIsUnsafe`, `info`, `isPending`, `isError`). Derive a single
discriminated union in `ConsentScreen` and pass that:

```ts
type ConsentView = { kind: "loading" } | { kind: "error" } | { kind: "prompt"; info: ConsentPrompt };
```

The ordering-matters comment (lines 243-255) becomes unnecessary — order is enforced by the
derivation, in one place, instead of by the sequence of `if`s in the consumer. Fixing effect
#1 already removes one of the six.

### D. `DashboardNav` — minor

`showOrganizations` threads three levels (`DashboardNav` → `NavRail`/`NavDrawer` →
`NavDestinationList`). Small enough to leave, and it comes from route context (`isAdmin`), so
it is auth-derived data — it must **not** move into `ui-store`, which is documented as
UI-only. If it ever gets noisy, read it from the route context in `NavDestinationList`
directly rather than threading.

---

## Suggested order

1. **DashboardNav → catalog `Drawer`** — fixes real a11y bugs, deletes effect #6 plus
   `NavBackdrop`. Highest value, self-contained.
2. **bff-demo → `useQuery`** — smallest, lowest risk, purely a demo route.
3. **The three `beforeLoad` guards** (#1, #2-guard, #3) — one shared pattern, three routes,
   already-established convention. #3 is the biggest deletion.
4. **MagicLink `useQuery`** (#4) — behaviour-sensitive; the exactly-once E2E path is the net.
5. **MfaEnrollForm split** (#A + #2-sequencing) — the largest change; do it last, and expect
   to update `MfaEnrollForm.start-mutation.test.ts` deliberately.
6. **LoginScreen composition** (#B) and **ConsentScreen union** (#C) — pure cleanups.

Each step is independently shippable and independently revertible. Every one of them is
covered by existing browser-mode specs plus the Playwright suites, so the gate is
`pnpm check` + `./scripts/e2e.sh`.
