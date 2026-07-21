import type { ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it, vi } from "vitest";

import { PublicLayout } from "./PublicLayout";

// `PublicLayout`'s nav uses TanStack `Link`s in the green implementation; stub
// `Link` to a plain anchor (passing `to` through as `href`) so it renders in
// isolation, mirroring `DashboardNav.test.tsx`. Any other react-router export a
// nav might reach for is passed through untouched.
vi.mock("@tanstack/react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@tanstack/react-router")>();
  return {
    ...actual,
    Link: ({
      to,
      children,
      ...rest
    }: { to: string; children?: ReactNode } & Record<string, unknown>) => (
      <a href={to} {...rest}>
        {children}
      </a>
    ),
  };
});

/**
 * PublicLayout spec (Wallow-ffpq.3.6) — the React port of Blazor
 * `PublicLayout.razor`. The public chrome must render a navbar (home/logo link
 * to "/", Features/Docs/GitHub links, a "Get Started" CTA into the BFF login)
 * and a footer ("MIT Licensed", GitHub + Docs links) around its children, so
 * the marketing page is reachable and navigable. Testids follow the repo's
 * `{page}-{element}` kebab-case rule under the `public-` page prefix.
 */
describe("PublicLayout", () => {
  it("renders its children (the page body)", async () => {
    await render(
      <PublicLayout>
        <p data-testid="public-body-probe">body</p>
      </PublicLayout>,
    );
    await expect.element(page.getByTestId("public-body-probe")).toBeInTheDocument();
  });

  it("renders a nav home/logo link back to the landing page", async () => {
    await render(<PublicLayout />);
    await expect.element(page.getByTestId("public-nav-home")).toHaveAttribute("href", "/");
  });

  it("renders the Features/Docs/GitHub nav links", async () => {
    await render(<PublicLayout />);
    await expect.element(page.getByTestId("public-nav-features")).toBeInTheDocument();
    await expect.element(page.getByTestId("public-nav-docs")).toBeInTheDocument();
    await expect.element(page.getByTestId("public-nav-github")).toBeInTheDocument();
  });

  it("renders a Get Started CTA into the BFF login flow", async () => {
    await render(<PublicLayout />);
    const cta = page.getByTestId("public-nav-get-started");
    await expect.element(cta).toBeInTheDocument();
    expect(cta.element().getAttribute("href") ?? "").toContain("/bff/login");
  });

  it("renders a footer with the MIT license notice and GitHub/Docs links", async () => {
    await render(<PublicLayout />);
    const footer = page.getByTestId("public-footer");
    await expect.element(footer).toHaveTextContent(/MIT/iu);
    await expect.element(page.getByTestId("public-footer-github")).toBeInTheDocument();
    await expect.element(page.getByTestId("public-footer-docs")).toBeInTheDocument();
  });
});
