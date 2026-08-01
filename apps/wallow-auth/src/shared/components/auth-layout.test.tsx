import {
  FORK_LINKS_GLOBAL_KEY,
  forkBranding,
  mergeClientBranding,
  type ResolvedBranding,
} from "@bc-solutions-coder/styles";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";

import { AuthLayout } from "./auth-layout";

/**
 * The fork's icon as the layout must render it: rooted, so it resolves to one
 * file from every route depth. Built from the branding value rather than
 * written as a literal — the filename stays the fork's to choose.
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

  it("leaves its heading reachable by FocusOnNavigate and unreachable by Tab", async () => {
    // `FocusOnNavigate` moves focus to the page's `<h1>` after each client-side
    // navigation, and a bare heading is not focusable — hence `tabindex="-1"`,
    // which also keeps it out of the Tab order. `Text` spreads its rest props,
    // so both attributes land on the heading itself rather than on a wrapper.
    await render(<AuthLayout />);

    const heading = page.getByRole("heading", { level: 1 }).element() as HTMLElement;

    expect(Object.hasOwn(heading.dataset, "focusTarget")).toBe(true);
    expect(heading.getAttribute("tabindex")).toBe("-1");
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
    // `branding.json` names the icon by bare filename, and rendering that value
    // verbatim 404s on every nested route: the browser resolves it against the
    // page, asking /mfa/challenge for /mfa/piggy-icon.svg. React does not
    // normalise against the app base, so the layout renders the rooted URL. On a
    // fork-branded page the icon appears twice, and both are the same bug.
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

/**
 * A deployment that set `WALLOW_REPOSITORY_URL`. The shell's inline script
 * writes exactly this global before hydration, which is how a server-resolved
 * link reaches the attribution with no provider above the layout.
 */
describe("AuthLayout fork attribution under a deployment's own link", () => {
  const deployed = {
    repositoryUrl: "https://git.example.test/acme/app",
    docsUrl: "https://docs.example.test/",
  };

  beforeEach(() => {
    (globalThis as Record<string, unknown>)[FORK_LINKS_GLOBAL_KEY] = deployed;
  });

  afterEach(() => {
    delete (globalThis as Record<string, unknown>)[FORK_LINKS_GLOBAL_KEY];
  });

  it("points the attribution at the repository this deployment names", async () => {
    await render(<AuthLayout />);

    await expect
      .element(page.getByRole("link", { name: new RegExp(forkBranding.appName, "iu") }))
      .toHaveAttribute("href", deployed.repositoryUrl);
  });
});
