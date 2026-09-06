**status: completed**

# Sidebar sizing and theme icons implementation plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Restore the expanded sidebar width, align its collapsed footer separator and controls, and replace sidebar theme names with changing icons.

**Architecture:** Keep navigation state in the existing nav store and theme preference in ThemeProvider. Fix width at the CSS or layout boundary demonstrated by browser measurements. Add an icon presentation to the shared ThemeToggle and use it in the sidebar.

**Tech Stack:** React, TypeScript, Tailwind CSS, Zustand, Vitest browser tests, Playwright.

## Scope and evidence

Implementation reproduced the expanded sidebar at 64px and the mobile drawer at approximately 203px. Explicit navigation CSS source registration restored both to 256px. SignOut duplicated the shell footer padding and border; removing that duplicate fixed the bottom layout.

- `packages/navigation/src/app-nav.tsx` declares a 4rem collapsed rail and a 16rem expanded rail through `data-[nav-open=true]:w-64`.
- Destination, theme, and footer sections all use 1rem horizontal padding, including when collapsed. The theme band has top padding but no bottom padding before the footer border.
- `ThemeToggle` always renders Light, Dark, or System text today.
- The local DocFX bundle chooses sun, moon, or circle-half according to the selected preference. Follow that changing-icon convention, as requested. System should keep its auto icon even when the OS changes between light and dark.
- The app stylesheet explicitly scans its app directory and imports the UI source stylesheet. Navigation's browser test stylesheet additionally scans navigation source. Check whether app CSS generation includes navigation utilities before changing the state logic; the test harness may conceal a consumer stylesheet issue.

## Task 1: Reproduce the width defect in the consuming app

Inspect `apps/wallow-web/src/app/styles.css`, `packages/navigation/src/app-nav.tsx`, `packages/navigation/src/app-shell.tsx`, and `packages/navigation/src/nav-store.ts`.

1. Run the web app using its existing local development setup. At a desktop viewport, measure the sidebar bounding rectangle in expanded, collapsed, and expanded-again states after its 200ms transition settles.
2. Record the toggle state, `data-nav-open`, computed width, winning width rule, and parent flex constraints. Expected widths are 16rem and 4rem, or 256px and 64px with the default root font size.
3. Inspect the consuming app's generated CSS for the expanded-width selector. Establish whether the defect is missing CSS, an override, or incorrect state before applying a fix.
4. Add a behavior regression in the existing app browser/E2E setup that measures both widths using the app stylesheet. A navigation-only test is insufficient if its extra source scan hides the failure.

## Task 2: Fix the demonstrated width cause

Primary files: `apps/wallow-web/src/app/styles.css`, `packages/navigation/src/app-nav.tsx`. Conditional files: `packages/navigation/package.json` and a new `packages/navigation/source.css` if an explicit reusable source export is needed.

1. If navigation utilities are missing, explicitly register navigation source in the consuming stylesheet. Use the existing UI source stylesheet convention if multiple consumers need the same contract. Preserve the single JavaScript entry and shared store identity.
2. If the selector already exists, fix the measured overriding rule or layout constraint instead. Retain the existing state and widths unless the reproduction proves those are wrong.
3. Run the width regression and verify the page content occupies the remaining space without overlap or horizontal scrolling.
4. Verify mobile still uses the full-width drawer and that desktop collapse state does not narrow it.

## Task 3: Align the collapsed bottom section

Modify `packages/navigation/src/app-nav.tsx`. Extend `packages/navigation/src/app-nav.modes.test.tsx` with rendered geometry checks.

1. Give collapsed sections 0.5rem horizontal padding and center square controls on the 4rem rail. Keep expanded navigation padding at 1rem. Confirm navigation icons, theme control, and footer control share the same horizontal center when collapsed.
2. Give the theme band bottom spacing so the footer separator does not touch its button. Keep the separator horizontal across the sidebar width, with balanced vertical spacing above and below it.
3. Keep footer actions owned by the app's existing slot. Pass presentation state to the footer wrapper if needed; do not duplicate sign-out behavior.
4. Measure child bounds and check for clipping or overflow in both widths. Cover a supplied footer and an absent footer, and visually inspect a short viewport with scrolling content.

## Task 4: Use a changing theme icon

Modify:

- `packages/ui/src/components/theme-toggle/theme-toggle.tsx`
- `packages/ui/src/components/theme-toggle/theme-toggle.styles.ts`
- `packages/ui/src/components/theme-toggle/theme-toggle.stories.tsx`
- `packages/ui/src/components/theme-toggle/theme-toggle.test.tsx`
- `packages/navigation/src/app-nav.tsx`
- `packages/navigation/src/app-nav.theme.test.tsx`

1. Add an explicit icon presentation to ThemeToggle while retaining its current text default for other consumers. Render decorative sun, moon, and half-circle auto glyphs for light, dark, and system. Follow the UI package's icon convention; do not introduce a new icon library solely for this control.
2. Use the icon presentation in expanded, collapsed, and mobile sidebar modes. Keep a consistent square hit target, centered in the collapsed rail and aligned with content in the expanded sidebar.
3. Preserve the existing light → dark → system → light cycle and preference persistence. This request changes presentation, not the selection interaction.
4. Provide an accessible name describing the current preference and next action, plus a hover/focus tooltip naming the preference. Keep the SVG hidden from assistive technology and retain visible keyboard focus.
5. Test click and keyboard cycling, controlled callbacks, and system preference while the resolved OS mode changes. Assert rendered behavior and accessible names. Add stories for each icon state.

## Task 5: Verify the complete sidebar

Run:

```sh
pnpm --filter @bc-solutions-coder/navigation test
pnpm --filter @bc-solutions-coder/ui test
pnpm --filter @bc-solutions-coder/navigation typecheck
pnpm --filter @bc-solutions-coder/ui typecheck
pnpm --filter @bc-solutions-coder/wallow-web build
pnpm lint
pnpm format:check
```

Run the app-level width regression through the repository's existing E2E command as well. A production build succeeding does not establish correct browser geometry.

Inspect the running app in expanded desktop, collapsed desktop, and mobile modes under light, dark, and system preferences. Capture before/after screenshots. Confirm the expanded rail is 16rem, the collapsed rail is 4rem, bottom controls fit and align, the separator has balanced spacing, and the theme icon represents the selected preference.

Use behavior-focused tests only. Do not add raw-source assertions. Keep all existing test IDs stable. Implementation is complete only when the original app-level reproduction passes, including with built app CSS.

## Verification results

- Browser regression reproduced the original 64px expanded width before the CSS change.
- All 27 dashboard browser tests pass with both development CSS and the production CSS artifact.
- Screenshots inspected for expanded and collapsed layouts confirm one separator and aligned icons.
- Theme stories cover light, dark, system resolving to either mode, and pointer/Space/Enter cycling. Navigation tests verify the focus tooltip.
- Repository formatting, lint, dependency/generated-file checks, builds, type checks, and all frontend suites passed. Package export checks passed after excluding the raw navigation CSS entry from TypeScript resolution analysis, matching the existing styles package treatment.
- The authenticated live-route E2E suite was not run. Browser coverage renders the app's DashboardLayout with its existing router and logout test doubles.
