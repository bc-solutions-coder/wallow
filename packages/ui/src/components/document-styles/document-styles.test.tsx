import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { DocumentStyles } from "./document-styles";

// A representative `renderThemeStyle(branding)` output. Kept as a literal so the
// test never imports @bc-solutions-coder/styles — packages/ui stays decoupled
// from branding data, exactly like fork-attribution.test.tsx uses literal props.
const THEME_CSS =
  ":root { --primary: oklch(0.6 0.2 250); }\n.dark { --primary: oklch(0.7 0.2 250); }";

describe("DocumentStyles", () => {
  it("renders the theme css inside a <style> tag", async () => {
    const { container } = await render(
      <DocumentStyles themeCss={THEME_CSS} stylesheetHref={null} />,
    );

    const style = container.querySelector("style");
    expect(style).not.toBeNull();
    expect((style as HTMLStyleElement).textContent).toBe(THEME_CSS);
  });

  it("renders no stylesheet <link> when stylesheetHref is null (dev branch)", async () => {
    const { container } = await render(
      <DocumentStyles themeCss={THEME_CSS} stylesheetHref={null} />,
    );

    expect(container.querySelector('link[rel="stylesheet"]')).toBeNull();
  });

  it("renders a stylesheet <link> with the given href when stylesheetHref is set (prod branch)", async () => {
    const { container } = await render(
      <DocumentStyles themeCss={THEME_CSS} stylesheetHref="/client.css" />,
    );

    const link = container.querySelector('link[rel="stylesheet"]');
    expect(link).not.toBeNull();
    expect((link as HTMLLinkElement).getAttribute("href")).toBe("/client.css");
  });

  it("still renders the theme <style> when a stylesheet is linked", async () => {
    const { container } = await render(
      <DocumentStyles themeCss={THEME_CSS} stylesheetHref="/client.css" />,
    );

    const style = container.querySelector("style");
    expect(style).not.toBeNull();
    expect((style as HTMLStyleElement).textContent).toBe(THEME_CSS);
  });
});
