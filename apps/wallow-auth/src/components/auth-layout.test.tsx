import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import { forkBranding, mergeClientBranding, type ResolvedBranding } from "../lib/branding";
import { AuthLayout } from "./auth-layout";

/**
 * The fork's icon as the layout must render it: rooted, so it resolves to one
 * file from every route depth. Written from the JSON rather than as a literal —
 * the filename stays the fork's to choose, only the leading slash is the
 * platform's.
 */
const rootedAppIcon = `/${forkBranding.appIcon}`;

const clientBranding: ResolvedBranding = mergeClientBranding(forkBranding, {
  clientId: "acme-web",
  displayName: "Acme",
  tagline: "Acme things",
  logoUrl: "https://cdn.test/acme.svg",
  themeJson: null,
});

describe("AuthLayout", () => {
  it("falls back to the fork's branding when no client is identified", async () => {
    await render(<AuthLayout />);

    expect(page.getByRole("heading", { level: 1 }).element().textContent).toBe(
      forkBranding.appName,
    );
    expect(page.getByText(forkBranding.tagline).element()).toBeDefined();
  });

  it("renders the page body it wraps", async () => {
    await render(
      <AuthLayout>
        <p data-testid="login-form">sign in</p>
      </AuthLayout>,
    );

    expect(page.getByTestId("login-form").element().textContent).toBe("sign in");
  });

  it("headlines the client's branding when one is identified", async () => {
    await render(<AuthLayout branding={clientBranding} />);

    expect(page.getByRole("heading", { level: 1 }).element().textContent).toBe("Acme");
    expect(page.getByText("Acme things").element()).toBeDefined();
    expect(page.getByRole("img", { name: "Acme" }).element().getAttribute("src")).toBe(
      "https://cdn.test/acme.svg",
    );
  });

  it("still attributes the fork on a client-branded page", async () => {
    // The footer is what tells a user on an "Acme" login page that Wallow
    // serves it. It must never take the client's branding.
    await render(<AuthLayout branding={clientBranding} />);

    const footer = page.getByText(/App$/u);

    expect(footer.getByText(forkBranding.appName).element()).toBeDefined();
  });

  it("omits the heading logo when neither client nor fork supplies one", async () => {
    const nameOnly: ResolvedBranding = { ...clientBranding, logoUrl: null, tagline: null };

    await render(<AuthLayout branding={nameOnly} />);

    // Only the fork's footer icon remains — no heading logo, and in particular
    // no fallback to the fork's icon under the client's name.
    const images: Element[] = page.getByRole("img").elements();
    expect(images).toHaveLength(1);
    expect(images[0]?.getAttribute("src")).toBe(rootedAppIcon);
  });

  it("serves every fork icon it renders from the site root", async () => {
    // api/branding.json names the icon by bare filename (`piggy-icon.svg`), and
    // rendering that value verbatim is what made the icon 404 on every nested
    // route: the browser resolved it against the page, asking /mfa/challenge for
    // /mfa/piggy-icon.svg. React does not normalise the path against the app
    // base, so the layout must render the rooted URL.
    //
    // On a fork-branded page the icon appears twice — as the heading logo and in
    // the footer attribution — and both are the same bug.
    await render(<AuthLayout />);

    const sources: (string | null)[] = page
      .getByRole("img")
      .elements()
      .map((image: Element): string | null => image.getAttribute("src"));

    expect(sources).toHaveLength(2);
    expect(sources).toEqual([rootedAppIcon, rootedAppIcon]);
  });

  it("resolves its icons to the same file from a nested route", async () => {
    await render(<AuthLayout />);

    const resolved: string[] = page
      .getByRole("img")
      .elements()
      .map(
        (image: Element): string =>
          new URL(image.getAttribute("src") ?? "", "http://localhost:3002/mfa/challenge").pathname,
      );

    expect(new Set(resolved)).toEqual(new Set([rootedAppIcon]));
  });
});
