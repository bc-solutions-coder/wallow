import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { ForkAttribution } from "./fork-attribution";

describe("ForkAttribution", () => {
  it("renders a plain inline group with the fork icon and name when no repo url", async () => {
    const { container } = await render(<ForkAttribution appName="Wallow" iconUrl="/piggy.svg" />);

    // No link when there is no repository to point at.
    expect(container.querySelector("a")).toBeNull();

    const icon = container.querySelector("img");
    expect(icon).not.toBeNull();
    expect((icon as HTMLImageElement).getAttribute("src")).toBe("/piggy.svg");
    expect((icon as HTMLImageElement).getAttribute("alt")).toBe("Wallow");
    expect((icon as HTMLImageElement).classList.contains("size-8")).toBe(true);

    expect(container.textContent).toContain("Wallow");
  });

  it("renders an external link when a repository url is supplied", async () => {
    const { container } = await render(
      <ForkAttribution
        appName="Wallow"
        iconUrl="/piggy.svg"
        repositoryUrl="https://github.com/example/fork"
      />,
    );

    const link = container.querySelector("a");
    expect(link).not.toBeNull();
    expect((link as HTMLAnchorElement).getAttribute("href")).toBe(
      "https://github.com/example/fork",
    );
    expect((link as HTMLAnchorElement).getAttribute("target")).toBe("_blank");
    expect((link as HTMLAnchorElement).getAttribute("rel")).toBe("noopener noreferrer");
    expect((link as HTMLAnchorElement).querySelector("img")).not.toBeNull();
  });

  it("treats an empty repository url as no link", async () => {
    const { container } = await render(
      <ForkAttribution appName="Wallow" iconUrl="/piggy.svg" repositoryUrl="" />,
    );

    expect(container.querySelector("a")).toBeNull();
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(
      <ForkAttribution appName="Wallow" iconUrl="/piggy.svg" data-testid="fork-attribution" />,
    );

    expect(container.querySelector('[data-testid="fork-attribution"]')).not.toBeNull();
  });
});
