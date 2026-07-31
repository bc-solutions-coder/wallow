import type { ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it, vi } from "vitest";

import { PublicLayout } from "./PublicLayout";
import { docsUrl, getStartedHref, repositoryUrl } from "@shared/lib/site-links";

// Stub TanStack `Link` to a plain anchor (`to` passed through as `href`) so the
// nav renders without a router. Every other export passes through untouched.
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
 * The public chrome wraps its children in a navbar (home/logo link to "/",
 * Features/Docs/GitHub links, a "Get Started" CTA into the BFF login) and a
 * footer (MIT notice, GitHub + Docs links), so the marketing page is reachable
 * and navigable. Testids take the `public-` page prefix.
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
    // The CTA target is owned by `site-links`, not by a layout-local constant.
    await expect.element(cta).toHaveAttribute("href", getStartedHref);
  });

  it("renders a footer with the MIT license notice and GitHub/Docs links", async () => {
    await render(<PublicLayout />);
    const footer = page.getByTestId("public-footer");
    await expect.element(footer).toHaveTextContent(/MIT/iu);
    await expect.element(page.getByTestId("public-footer-github")).toBeInTheDocument();
    await expect.element(page.getByTestId("public-footer-docs")).toBeInTheDocument();
  });
});

/**
 * The chrome's GitHub/Docs targets come from `site-links`, the single source
 * reading the fork's configured `repositoryUrl`/`docsUrl`, so nav and footer
 * cannot drift apart or point at the upstream repository. Nothing here mocks
 * `@bc-solutions-coder/styles`, so these run against the real branding values.
 */
describe("PublicLayout link targets", () => {
  it("points the Features nav link at the landing page's features section", async () => {
    await render(<PublicLayout />);
    await expect
      .element(page.getByTestId("public-nav-features"))
      .toHaveAttribute("href", "/#features");
  });

  it("points both Docs links at the fork's docs site", async () => {
    await render(<PublicLayout />);
    await expect.element(page.getByTestId("public-nav-docs")).toHaveAttribute("href", docsUrl);
    await expect.element(page.getByTestId("public-footer-docs")).toHaveAttribute("href", docsUrl);
  });

  it("points both GitHub links at the fork's repository", async () => {
    await render(<PublicLayout />);
    await expect
      .element(page.getByTestId("public-nav-github"))
      .toHaveAttribute("href", repositoryUrl);
    await expect
      .element(page.getByTestId("public-footer-github"))
      .toHaveAttribute("href", repositoryUrl);
  });

  it("exposes absolute link targets, with the docs site independent of the repository", () => {
    expect(repositoryUrl).toMatch(/^https:\/\/\S+/u);
    expect(docsUrl).toMatch(/^https:\/\/\S+/u);
    // The docs site is its own address, not a path derived from the repository.
    expect(docsUrl).not.toBe(`${repositoryUrl}/tree/main/docs`);
    expect(getStartedHref).toContain("/bff/login");
  });
});
