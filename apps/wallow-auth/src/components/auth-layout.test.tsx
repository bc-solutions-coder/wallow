// @vitest-environment jsdom
import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { forkBranding, mergeClientBranding, type ResolvedBranding } from "../lib/branding";
import { AuthLayout } from "./auth-layout";

const clientBranding: ResolvedBranding = mergeClientBranding(forkBranding, {
  clientId: "acme-web",
  displayName: "Acme",
  tagline: "Acme things",
  logoUrl: "https://cdn.test/acme.svg",
  themeJson: null,
});

describe("AuthLayout", () => {
  it("falls back to the fork's branding when no client is identified", () => {
    render(<AuthLayout />);

    expect(screen.getByRole("heading", { level: 1 }).textContent).toBe(forkBranding.appName);
    expect(screen.getByText(forkBranding.tagline)).toBeDefined();
  });

  it("renders the page body it wraps", () => {
    render(
      <AuthLayout>
        <p data-testid="login-form">sign in</p>
      </AuthLayout>,
    );

    expect(screen.getByTestId("login-form").textContent).toBe("sign in");
  });

  it("headlines the client's branding when one is identified", () => {
    render(<AuthLayout branding={clientBranding} />);

    expect(screen.getByRole("heading", { level: 1 }).textContent).toBe("Acme");
    expect(screen.getByText("Acme things")).toBeDefined();
    expect(screen.getByRole("img", { name: "Acme" }).getAttribute("src")).toBe(
      "https://cdn.test/acme.svg",
    );
  });

  it("still attributes the fork on a client-branded page", () => {
    // The footer is what tells a user on an "Acme" login page that Wallow
    // serves it. It must never take the client's branding.
    render(<AuthLayout branding={clientBranding} />);

    const footer: HTMLElement = screen.getByText(/App$/u);

    expect(within(footer).getByText(forkBranding.appName)).toBeDefined();
  });

  it("omits the heading logo when neither client nor fork supplies one", () => {
    const nameOnly: ResolvedBranding = { ...clientBranding, logoUrl: null, tagline: null };

    render(<AuthLayout branding={nameOnly} />);

    // Only the fork's footer icon remains — no heading logo, and in particular
    // no fallback to the fork's icon under the client's name.
    const images: HTMLElement[] = screen.getAllByRole("img");
    expect(images).toHaveLength(1);
    expect(images[0]?.getAttribute("src")).toBe(forkBranding.appIcon);
  });
});
