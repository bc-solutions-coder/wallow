import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { BrandedHeader } from "./branded-header";

/**
 * Edges the stories cannot express: variant structure (h1 vs wrapperless span),
 * derived part test ids, and the empty-string/null collapse of the optional
 * logo, tagline and organization props.
 */
describe("BrandedHeader", () => {
  it("renders the page variant as an h1 focus target with logo and tagline", async () => {
    const { container } = await render(
      <BrandedHeader
        name="Acme Dashboard"
        tagline="Ship faster"
        logoUrl="https://cdn.example.com/acme.png"
      />,
    );

    const heading = container.querySelector("h1");
    expect(heading).not.toBeNull();
    expect((heading as HTMLHeadingElement).textContent).toBe("Acme Dashboard");
    // The route-change focus target: FocusOnNavigate moves focus here, so the
    // heading must be programmatically focusable without joining the tab order.
    expect((heading as HTMLHeadingElement).hasAttribute("data-focus-target")).toBe(true);
    expect((heading as HTMLHeadingElement).getAttribute("tabindex")).toBe("-1");

    const logo = container.querySelector("img");
    expect(logo).not.toBeNull();
    expect((logo as HTMLImageElement).getAttribute("src")).toBe("https://cdn.example.com/acme.png");
    expect((logo as HTMLImageElement).getAttribute("alt")).toBe("Acme Dashboard");

    expect(container.textContent).toContain("Ship faster");
  });

  it("renders neither logo nor tagline when absent", async () => {
    const { container } = await render(<BrandedHeader name="Wallow" tagline={null} />);

    expect(container.querySelector("img")).toBeNull();
    expect((container.querySelector("h1") as HTMLHeadingElement).textContent).toBe("Wallow");
  });

  it("attributes the owning organization beneath the heading", async () => {
    const { container } = await render(
      <BrandedHeader name="BFF Example" organizationName="Wallow" data-testid="auth-header" />,
    );

    const organization = container.querySelector('[data-testid="auth-header-organization"]');
    expect(organization).not.toBeNull();
    expect((organization as HTMLElement).textContent).toBe("by Wallow");
  });

  it("renders no organization line when none is given", async () => {
    const { container } = await render(<BrandedHeader name="Wallow" data-testid="auth-header" />);

    expect(container.querySelector('[data-testid="auth-header-organization"]')).toBeNull();
    expect(container.textContent).not.toContain("by ");
  });

  it("derives part test ids from the header's own", async () => {
    const { container } = await render(
      <BrandedHeader
        name="Acme"
        tagline="Ship faster"
        logoUrl="/acme.png"
        data-testid="auth-header"
      />,
    );

    expect(container.querySelector('[data-testid="auth-header"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="auth-header-logo"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="auth-header-name"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="auth-header-tagline"]')).not.toBeNull();
  });

  it("renders the card variant as an embeddable fragment with a span heading", async () => {
    const { container } = await render(
      <div data-testid="preview">
        <BrandedHeader
          variant="card"
          name="Acme Dashboard"
          tagline="Ship faster"
          logoUrl="/acme.png"
          data-testid="preview"
        />
      </div>,
    );

    // The caller owns the wrapper: the card variant contributes only parts, so
    // its test id never collides with the wrapper the caller stamped.
    expect(container.querySelectorAll('[data-testid="preview"]')).toHaveLength(1);
    expect(container.querySelector("h1")).toBeNull();
    expect((container.querySelector('[data-testid="preview-name"]') as HTMLElement).tagName).toBe(
      "SPAN",
    );
    expect(container.querySelector('[data-testid="preview-logo"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="preview-tagline"]')).not.toBeNull();
  });
});
