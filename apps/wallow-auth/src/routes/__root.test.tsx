// @vitest-environment jsdom
import { describe, expect, it } from "vitest";

import { forkBranding } from "../lib/branding";
import { render } from "../ssr";

/**
 * The document shell's half of the brand-asset contract (Wallow-ffpq.1.2).
 *
 * Driven through the SSR entry rather than by rendering `__root.tsx` directly:
 * the shell renders an `<Outlet/>` and has no meaning outside a router, and the
 * head is what these tests are about — including the `<link>` tags React emits
 * on the shell's behalf, which only appear on a real render pass.
 *
 * Every assertion here is about the same thing: an asset URL in this head must
 * be root-relative. The head is byte-identical on every route, so a relative URL
 * in it is not a shell bug that shows up somewhere — it is one that shows up on
 * every nested route at once.
 */
const rootedAppIcon = `/${forkBranding.appIcon}`;

async function renderHead(path: string): Promise<string> {
  const response: Response = await render(new Request(`http://localhost:3002${path}`));
  const html: string = await response.text();
  const head: RegExpMatchArray | null = html.match(/<head>(?<body>.*)<\/head>/su);

  if (head?.groups?.body === undefined) {
    throw new Error(`SSR response for ${path} has no <head>`);
  }
  return head.groups.body;
}

describe("the wallow-auth document shell", () => {
  it("points the favicon at the fork's icon, from the site root", async () => {
    // The fork's icon is the favicon — api/branding.json names one asset, and
    // the tab icon is the other place it belongs.
    const head: string = await renderHead("/login");

    expect(head).toMatch(new RegExp(`<link[^>]*rel="icon"[^>]*href="${rootedAppIcon}"`, "u"));
  });

  it("references no brand asset relative to the current route", async () => {
    // Catches the `<link rel="preload" as="image">` React emits for the icon as
    // well as the tags the shell writes itself: any occurrence of the bare
    // filename without a leading slash is a 404 waiting on a nested route.
    const head: string = await renderHead("/mfa/challenge");

    expect(head).toContain(forkBranding.appIcon);
    expect(head).not.toMatch(new RegExp(`href="(?!/)[^"]*${forkBranding.appIcon}`, "u"));
  });

  it("emits the same brand asset URLs on a nested route as on a top-level one", async () => {
    const [top, nested]: string[] = await Promise.all([
      renderHead("/login"),
      renderHead("/mfa/challenge"),
    ]);

    const iconRefs = (head: string): string[] =>
      [...head.matchAll(new RegExp(`href="(?<url>[^"]*${forkBranding.appIcon})"`, "gu"))].map(
        (match: RegExpMatchArray): string => match.groups?.url ?? "",
      );

    expect(iconRefs(nested ?? "")).toEqual(iconRefs(top ?? ""));
    expect(new Set(iconRefs(nested ?? ""))).toEqual(new Set([rootedAppIcon]));
  });
});
