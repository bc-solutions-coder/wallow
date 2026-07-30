import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { Badge } from "./badge";
import { badgeRecipe } from "./badge.styles";

/*
 * SPEC for Wallow-lrlm.3.4 (Badge).
 *
 * The neutral variant is not a new design: it is the chip six wallow-web
 * surfaces already hand-roll as the SAME literal class string (MfaSettingsSection,
 * ProfileSection, OrganizationList, AppList, InquiryList, InquiryDetail). Pinning
 * it byte-for-byte is what lets those call sites migrate onto the catalog without
 * a visual diff.
 *
 * The state variants are the new capability. MfaSettingsSection's chip carries a
 * comment saying it stays state-independent because "there is no success token in
 * the theme" — that constraint is lifted now that Wallow-lrlm.1.1 shipped
 * success/success-foreground, and this component is what spends it.
 *
 * Class assertions are order-free sets, per the Button and MutedText exemplars,
 * so tailwind-merge may reorder freely.
 */

/** The shape half of the chip — identical across every variant. */
const SHAPE_CLASSES = [
  "inline-block",
  "text-xs",
  "font-medium",
  "px-2.5",
  "py-0.5",
  "rounded-full",
];

/**
 * The surface/foreground token pair each variant paints, and nothing else. A
 * filled pill needs BOTH halves of a token pair: a variant that set only the
 * surface would leave the label at the inherited colour and fail contrast on its
 * own background.
 *
 * `warning` maps onto `primary` deliberately: the fork's primary is the amber
 * oklch(0.72 0.15 85), and the theme has no dedicated warning token. Adding one
 * belongs to F1, not here (F3's scope guard) — so warning spends the amber the
 * palette already carries rather than inventing a sixth token or a raw hue.
 */
const VARIANT_COLOURS = {
  neutral: ["bg-accent", "text-accent-foreground"],
  success: ["bg-success", "text-success-foreground"],
  warning: ["bg-primary", "text-primary-foreground"],
  destructive: ["bg-destructive", "text-destructive-foreground"],
} as const;

/** The literal chip string the six wallow-web call sites carry today. */
const SHIPPED_CHIP =
  "inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full";

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function onlySpan(container: HTMLElement): HTMLSpanElement {
  const span = container.querySelector("span");
  expect(span).not.toBeNull();
  return span as HTMLSpanElement;
}

describe("Badge", () => {
  it("renders a span reproducing the shipped wallow-web chip by default", async () => {
    const { container } = await render(<Badge>Owner</Badge>);

    const span = onlySpan(container);
    expect(classSet(span)).toEqual(SHIPPED_CHIP.split(" ").toSorted());
    expect(span.textContent).toBe("Owner");
  });

  it("defaults to the neutral variant", async () => {
    const { container: bare } = await render(<Badge>Owner</Badge>);
    const { container: explicit } = await render(<Badge variant="neutral">Owner</Badge>);

    expect(classSet(onlySpan(bare))).toEqual(classSet(onlySpan(explicit)));
  });

  for (const [variant, colours] of Object.entries(VARIANT_COLOURS)) {
    it(`paints the ${variant} variant with its semantic token pair`, async () => {
      const { container } = await render(
        <Badge variant={variant as keyof typeof VARIANT_COLOURS}>Status</Badge>,
      );

      expect(classSet(onlySpan(container))).toEqual([...SHAPE_CLASSES, ...colours].toSorted());
    });
  }

  it("gives every variant a distinct surface", async () => {
    // Four variants that all painted the same background would satisfy the
    // per-variant assertions above while being useless as status colours.
    const surfaces = Object.values(VARIANT_COLOURS).map(([surface]) => surface);

    for (const [variant] of Object.entries(VARIANT_COLOURS)) {
      const rendered = badgeRecipe({ variant: variant as keyof typeof VARIANT_COLOURS });
      const painted = surfaces.filter((surface) => rendered.split(" ").includes(surface));

      expect(painted, variant).toHaveLength(1);
    }
  });

  it("uses only semantic token utilities — never a raw colour", async () => {
    // The catalog rule (packages/ui/CLAUDE.md): a recipe references only tokens
    // @bc-solutions-coder/styles already defines. This catches an arbitrary
    // value (`bg-[#16a34a]`) and a stock Tailwind palette hue (`bg-green-500`)
    // alike, either of which would ignore the fork's branding.json.
    const semanticToken =
      /^(?:bg|text)-(?:accent|success|primary|destructive|secondary|muted|card|popover|sidebar|background|foreground|border)(?:-foreground)?$/u;

    for (const variant of Object.keys(VARIANT_COLOURS)) {
      const colourUtilities = badgeRecipe({ variant: variant as keyof typeof VARIANT_COLOURS })
        .split(" ")
        .filter((utility) => /^(?:bg|text)-/u.test(utility) && utility !== "text-xs");

      expect(colourUtilities.length, `${variant} paints nothing`).toBeGreaterThan(0);
      for (const utility of colourUtilities) {
        expect(utility, `${variant} -> ${utility}`).toMatch(semanticToken);
      }
    }
  });

  it("lets a caller className override the surface", async () => {
    const { container } = await render(<Badge className="bg-secondary">Custom</Badge>);

    const span = onlySpan(container);
    expect(span.classList.contains("bg-secondary")).toBe(true);
    expect(span.classList.contains("bg-accent")).toBe(false);
    expect(span.classList.contains("rounded-full")).toBe(true);
  });

  it("appends a non-conflicting caller className to the recipe", async () => {
    const { container } = await render(<Badge className="ml-2">Owner</Badge>);

    expect(classSet(onlySpan(container))).toEqual([...SHIPPED_CHIP.split(" "), "ml-2"].toSorted());
  });

  it("passes through an app-owned data-testid and the rest of its props", async () => {
    const { container } = await render(
      <Badge data-testid="settings-mfa-status" variant="success" title="Two-factor is on">
        Enabled
      </Badge>,
    );

    const span = container.querySelector('[data-testid="settings-mfa-status"]');
    expect(span).not.toBeNull();
    expect(span?.getAttribute("title")).toBe("Two-factor is on");
  });
});
