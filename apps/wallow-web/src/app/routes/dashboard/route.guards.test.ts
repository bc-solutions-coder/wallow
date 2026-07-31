import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The `/dashboard` layout route gates on the SDK's shared auth utilities, not on
 * logic hand-rolled in this app.
 *
 * Read from SOURCE: a hand-rolled guard and the SDK one behave identically, so
 * no behavioural spec can see the difference. `route.ssr.test.tsx` pins what the
 * gate does; this pins where the logic lives.
 */

const routeSource: string = readFileSync(
  fileURLToPath(new URL("./route.tsx", import.meta.url)),
  "utf8",
);

/**
 * The shared helpers the route must import rather than hand-roll.
 *
 * `loginRedirect` is deliberately not in this list: `requireAuth` composes it
 * internally, so requiring the route to name it too would force an unused
 * import.
 */
const REQUIRED_HELPERS: readonly string[] = ["isAdmin", "requireAuth"];

/**
 * Either shared package is a correct source: the SDK browser entry defines these
 * guards, and `@bc-solutions-coder/auth` re-exports them BY REFERENCE so an
 * app's auth imports can come from one package instead of two. What this spec
 * forbids is neither — a guard hand-rolled in this app.
 */
const SHARED_SOURCES = String.raw`@bc-solutions-coder/(?:sdk|auth)`;

describe("routes/dashboard/route (shared auth-utility adoption)", () => {
  it.each(REQUIRED_HELPERS)("imports %s from a shared package", (helper: string) => {
    expect(routeSource).toMatch(
      new RegExp(`import\\s*\\{[^}]*\\b${helper}\\b[^}]*\\}\\s*from\\s*"${SHARED_SOURCES}"`, "s"),
    );
  });

  it("no longer defines a local role-claim reader", () => {
    // Reading `roles` vs `role`, array vs string, is `getRoles`/`isAdmin`'s job.
    expect(routeSource).not.toMatch(/function\s+isAdminUser/);
    expect(routeSource).not.toMatch(/user\.roles\s*\?\?\s*user\.role/);
  });

  it("no longer hand-builds the BFF login target", () => {
    // The literal href and its `encodeURIComponent(...)` belong to
    // `loginRedirect`, which bakes in `reloadDocument` so it cannot be dropped.
    expect(routeSource).not.toMatch(/["'`]\/bff\/login\?returnTo=/);
  });

  it("still redirects by href and never by a router `to`", () => {
    // `/bff/login` is a BFF endpoint, not a route in the tree: a `to` target is
    // committed through the client router and lands on a not-found match.
    expect(routeSource).not.toMatch(/redirect\(\s*\{[^}]*\bto:/s);
  });

  it("never imports the SDK's browser-only login() navigator", () => {
    // It assigns to the bare global `location`, which does not exist under Node,
    // so a full-page SSR load of /dashboard/** would return HTTP 500.
    // `loginRedirect` is the SSR-safe alternative, and `\blogin\b` misses it.
    expect(routeSource).not.toMatch(
      new RegExp(`import\\s*\\{[^}]*\\blogin\\b[^}]*\\}\\s*from\\s*"${SHARED_SOURCES}"`, "s"),
    );
  });
});
