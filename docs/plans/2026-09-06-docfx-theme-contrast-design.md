# DocFX theme contrast design

**Status:** Approved

## Problem

The DocFX theme generator maps Bootstrap text variables to tokens that describe
backgrounds or text on a different surface. Inline `code` uses `accent`, which
has about 1.3:1 contrast against the page background in both modes. The navbar
keeps a dark background but inherits mode-specific text values intended for a
gold primary background, which also has about 1.1:1 contrast.

The generated `.nav-link` override is labeled as sidebar styling, but DocFX's
sidebar links do not use that class. The rule can affect unrelated Bootstrap
navigation instead.

## Options

1. Map each Bootstrap variable to a semantic branding token and scope fixed
   surface variables to that surface. This fixes forks as they change their
   palette and removes the ineffective navigation override.
2. Add `code` and navbar selector overrides after the generated variables. This
   fixes Wallow's current colors but adds another cascade layer.
3. Swap light and dark values. This happens to invert the current colors but
   breaks the meaning of the branding tokens and will fail for other palettes.

## Decision

Use semantic mappings in `scripts/generate-docs-theme.mjs`:

- Map `--bs-code-color` to each mode's `accentForeground` token.
- Remove the navbar variables from the mode roots.
- Define the navbar background and its Bootstrap text variables together in
  `.navbar`, using `theme.dark.background` and `theme.dark.foreground`.
- Remove the broad `.nav-link` blocks. The generated Bootstrap link variables
  already provide the theme's primary color.
- Regenerate `docfx/templates/wallow/public/main.css`; do not edit it by hand.

## Verification

Run the contrast check against the generated CSS before and after the change.
Each inline-code and navbar combination must meet the WCAG AA 4.5:1 threshold.
Then regenerate the theme a second time to prove that the committed file is
stable, build the DocFX site, and run the repository checks relevant to the
changed JavaScript and generated CSS.

No source-text regression test will be added. The repository policy prefers
rendered behavior tests, and this repository has no DocFX browser-test entry
point. The generated artifact and contrast check exercise the reported output
without pinning implementation text in a test.
