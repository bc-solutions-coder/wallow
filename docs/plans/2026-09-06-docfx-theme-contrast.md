**status: completed**

# DocFX Theme Contrast Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Give inline code and the fixed dark navbar readable colors in both DocFX themes while removing a broad, ineffective navigation override.

**Architecture:** Keep `packages/styles/branding.json` as the palette source and fix the semantic mapping in `scripts/generate-docs-theme.mjs`. Scope navbar variables to `.navbar` because its background does not switch with the document theme, then regenerate the committed CSS artifact.

**Tech Stack:** Node.js, generated CSS, Bootstrap 5 variables, DocFX

---

### Task 1: Correct the theme mappings

**Files:**

- Modify: `scripts/generate-docs-theme.mjs:84-116`
- Regenerate: `docfx/templates/wallow/public/main.css`

**Step 1: Confirm the failing behavior**

Run the temporary contrast checker against the generated stylesheet.

Expected failures:

```text
FAIL light inline code: 1.32:1
FAIL dark inline code: 1.30:1
FAIL light-mode navbar text: 1.05:1
FAIL dark-mode navbar text: 1.14:1
```

**Step 2: Fix inline-code text**

Change the Bootstrap code mapping in `generateCssVars`:

```js
--bs-code-color: ${vars.accentForeground};
```

Remove all `--bs-navbar-*` declarations from `generateCssVars`.

**Step 3: Scope the navbar palette**

Generate the fixed navbar surface and its text variables together:

```css
.navbar {
  --bs-navbar-brand-color: ${theme.dark.foreground};
  --bs-navbar-brand-hover-color: ${theme.dark.foreground};
  --bs-navbar-color: ${theme.dark.foreground};
  --bs-navbar-hover-color: ${theme.dark.foreground};
  --bs-navbar-active-color: ${theme.dark.foreground};
  background-color: ${theme.dark.background} !important;
}
```

Delete both global `.nav-link` override blocks. DocFX sidebar links do not use
that class, and Bootstrap already receives the branded link variables.

**Step 4: Regenerate the CSS**

Run:

```bash
node scripts/generate-docs-theme.mjs packages/styles/branding.json
```

Expected: `docfx/templates/wallow/public/main.css` contains the corrected
semantic mappings and no broad `.nav-link` rule.

**Step 5: Confirm the fixed behavior**

Run the same temporary contrast checker.

Expected minimum results:

```text
PASS light inline code: 15.98:1
PASS dark inline code: 11.56:1
PASS light-mode navbar text: 13.10:1
PASS dark-mode navbar text: 13.10:1
```

### Task 2: Verify the generated artifact

**Files:**

- Verify: `scripts/generate-docs-theme.mjs`
- Verify: `docfx/templates/wallow/public/main.css`

**Step 1: Check deterministic generation**

Run the generator again, then run:

```bash
git diff --exit-code -- docfx/templates/wallow/public/main.css
```

Expected: the second generation adds no new diff beyond the intended artifact
change.

**Step 2: Build the documentation site**

Run:

```bash
./scripts/docs-serve.sh --build-only
```

Expected: DocFX exits successfully and writes `.docfx/_site`.

**Step 3: Run repository checks**

Run:

```bash
pnpm exec oxfmt --check scripts/generate-docs-theme.mjs
git diff --check
pnpm test
```

Expected: each command exits successfully. Existing informational test output
does not count as a failure.

**Step 4: Review and commit**

Inspect `git diff` and `git status`. Stage only the generator, generated CSS,
and plan. If `issues.jsonl` appears, leave it out of the commit.

```bash
git add scripts/generate-docs-theme.mjs \
  docfx/templates/wallow/public/main.css \
  docs/plans/2026-09-06-docfx-theme-contrast.md
git commit -m "fix(docs): restore DocFX theme contrast"
```

### Task 3: Add rendered regression coverage

**Files:**

- Add: `packages/testing/scripts/docfx-contrast.mjs`
- Modify: `packages/testing/package.json`
- Modify: `.github/workflows/docs.yml`
- Modify: `docs/plans/2026-09-06-docfx-theme-contrast-design.md`
- Modify: `docs/plans/2026-09-06-docfx-theme-contrast.md`

**Step 1: Prove the rendered test fails before the fix**

Build the fork guide with the pre-fix theme artifact, then run:

```bash
pnpm --filter @bc-solutions-coder/testing test:docfx
```

Expected: the real table-cell inline code and Bootstrap navbar link fail the
WCAG AA 4.5:1 threshold in both themes.

**Step 2: Prove the rendered test passes after the fix**

Rebuild the site from the corrected generator and run the same command.

Expected: all four rendered contrast checks pass without reading CSS or source
text. The browser resolves computed colors through a canvas before calculating
their contrast.

**Step 3: Run the test in the docs workflow**

Install the testing package dependencies and Chromium in the `build-site` job.
Run `test:docfx` after DocFX builds and before uploading the site artifact. Keep
the workflow path filters in sync with the test and package manifest.
