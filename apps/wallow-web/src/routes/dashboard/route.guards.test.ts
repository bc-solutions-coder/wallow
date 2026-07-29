import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Adoption spec (Wallow-pu6a.5.6): the `/dashboard` layout route must gate on
 * the SDK's shared auth utilities, not on logic hand-rolled in this app.
 *
 * `route.test.tsx` (the node-project behavioural spec) already pins WHAT the
 * gate does — redirect target, `reloadDocument`, `isAdmin` on the context, and
 * SSR safety — and it keeps passing either way, because a hand-rolled guard and
 * the SDK one behave identically. That is exactly why the behavioural spec
 * cannot see the difference this bead is about: the point is that the guard is
 * no longer duplicated per app. So this spec reads the route SOURCE and pins the
 * delegation itself.
 *
 * Source reading is the narrow tool for a "this logic lives in the SDK now"
 * contract, and it is the pattern the SDK already uses for its own structural
 * guarantees (`build-config.test.ts`, `bff-pattern-docs.test.ts`).
 */

const routeSource: string = readFileSync(
  fileURLToPath(new URL("./route.tsx", import.meta.url)),
  "utf8",
);

/**
 * The SDK helpers the route must import from the package root.
 *
 * `loginRedirect` is deliberately not in this list: `requireAuth` composes it
 * internally (pinned by the SDK's own `route-context.test.ts`, which asserts the
 * guard hands `redirect` exactly what `loginRedirect` built), so requiring the
 * route to name it too would force an unused import.
 */
const REQUIRED_HELPERS: readonly string[] = ["isAdmin", "requireAuth"];

describe("routes/dashboard/route (SDK auth-utility adoption)", () => {
  it.each(REQUIRED_HELPERS)("imports %s from the SDK browser entry", (helper: string) => {
    expect(routeSource).toMatch(
      new RegExp(
        `import\\s*\\{[^}]*\\b${helper}\\b[^}]*\\}\\s*from\\s*"@bc-solutions-coder/sdk"`,
        "s",
      ),
    );
  });

  it("no longer defines a local role-claim reader", () => {
    // `isAdminUser()` coped with `roles` vs `role` and array-vs-string inline;
    // that is `getRoles`/`isAdmin` in the SDK now.
    expect(routeSource).not.toMatch(/function\s+isAdminUser/);
    expect(routeSource).not.toMatch(/user\.roles\s*\?\?\s*user\.role/);
  });

  it("no longer hand-builds the BFF login target", () => {
    // The literal href and its `encodeURIComponent(...)` are `loginRedirect`'s
    // job — that is where `reloadDocument` is baked in so it cannot be dropped.
    expect(routeSource).not.toMatch(/["'`]\/bff\/login\?returnTo=/);
  });

  it("still redirects by href and never by a router `to`", () => {
    // `/bff/login` is a BFF endpoint, not a route in the tree: a `to` target is
    // committed through the client router and lands on a not-found match.
    expect(routeSource).not.toMatch(/redirect\(\s*\{[^}]*\bto:/s);
  });

  it("never imports the SDK's browser-only login() navigator", () => {
    // It assigns to the bare global `location`, which does not exist under
    // Node — a full-page SSR load of /dashboard/** returned HTTP 500
    // (Wallow-zyxe). `loginRedirect` is the SSR-safe replacement, and `\blogin\b`
    // does not match it.
    expect(routeSource).not.toMatch(
      /import\s*\{[^}]*\blogin\b[^}]*\}\s*from\s*"@bc-solutions-coder\/sdk"/s,
    );
  });
});
