/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";

import { PublicLayout } from "./PublicLayout";

// No global `expect` (vitest `globals` is off); register jest-dom matchers.
expect.extend(matchers);

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
  it("renders its children (the page body)", () => {
    render(
      <PublicLayout>
        <p data-testid="public-body-probe">body</p>
      </PublicLayout>,
    );
    expect(screen.getByTestId("public-body-probe")).toBeInTheDocument();
  });

  it("renders a nav home/logo link back to the landing page", () => {
    render(<PublicLayout />);
    expect(screen.getByTestId("public-nav-home")).toHaveAttribute("href", "/");
  });

  it("renders the Features/Docs/GitHub nav links", () => {
    render(<PublicLayout />);
    expect(screen.getByTestId("public-nav-features")).toBeInTheDocument();
    expect(screen.getByTestId("public-nav-docs")).toBeInTheDocument();
    expect(screen.getByTestId("public-nav-github")).toBeInTheDocument();
  });

  it("renders a Get Started CTA into the BFF login flow", () => {
    render(<PublicLayout />);
    const cta = screen.getByTestId("public-nav-get-started");
    expect(cta).toBeInTheDocument();
    expect(cta.getAttribute("href") ?? "").toContain("/bff/login");
  });

  it("renders a footer with the MIT license notice and GitHub/Docs links", () => {
    render(<PublicLayout />);
    const footer = screen.getByTestId("public-footer");
    expect(footer).toHaveTextContent(/MIT/iu);
    expect(screen.getByTestId("public-footer-github")).toBeInTheDocument();
    expect(screen.getByTestId("public-footer-docs")).toBeInTheDocument();
  });
});
