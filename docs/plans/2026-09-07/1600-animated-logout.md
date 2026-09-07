**status: completed**

# Animated logout

Approved design: keep the Wallow auth layout and theme tokens. Center the logout card around an amber sign-out illustration; reveal a checkmark on the signed-out landing. Use a short entrance animation and finite icon motion. Respect reduced motion, keyboard focus, light mode, and dark mode. Preserve the explicit logout navigation and server-validated return link.

## Implementation

1. Add a decorative SVG illustration and scoped CSS beside `apps/wallow-auth/src/features/logout/components/LogoutScreen.tsx`.
2. Style both phases with the shared Card, Text, and Button components; use "You're signed out" and "See you next time." for the completed state.
3. Update existing rendered-copy expectations in the component and E2E tests.
4. Run logout browser tests, auth typechecking, formatting and lint checks; inspect the rendered page at desktop/mobile sizes and with reduced motion.

## Verification

`pnpm check` passed, including workspace builds, typechecks, browser tests, lint, formatting, and external-consumer checks. All 54 logout browser tests passed. Manual browser inspection covered desktop light/dark and 390px mobile layouts. Reduced motion produced zero animations; mobile had no horizontal overflow. A final CSS color interpolation adjustment uses Oklab to keep the amber tint when mixed with white.

The separately reported favicon issue was not reproduced: both production app builds declare `/piggy-icon.svg` and serve the canonical 67,205-byte SVG with HTTP 200 and `image/svg+xml`. Browser and affected URL are needed to investigate that environment.
