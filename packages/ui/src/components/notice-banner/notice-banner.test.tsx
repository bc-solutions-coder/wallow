import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { NoticeBanner } from "./notice-banner";

/*
 * SPEC for Wallow-86os (NoticeBanner).
 *
 * Sourced from six hand-rolled wrappers across wallow-auth that each rebuilt
 * `ErrorBanner`'s shape in a non-destructive tone: five `border-success
 * bg-success/10` confirmations and one `border-warning bg-warning/10` nudge.
 *
 * Two deliberate differences from `ErrorBanner`, and both are asserted below:
 *
 *  - NO inner `<p>`, so no second sealed recipe. A notice body ranges from one
 *    sentence to a heading plus an action link (LoginScreen's MFA banner), so
 *    the caller composes `Text` inside it and children pass through untouched.
 *  - `tone` instead of `surface`. ErrorBanner's surface axis exists because a
 *    10% tint vanishes on the inverted rail, and none of these six call sites
 *    is on the rail.
 *
 * Class assertions are order-free sets, per the Button and ErrorBanner
 * exemplars, so tailwind-merge may reorder freely.
 */

/** The shape half of the banner — identical across every tone. */
const SHAPE_CLASSES = ["rounded-md", "border", "p-3"];

/** The border/tint token pair each tone paints, and nothing else. */
const TONE_COLOURS = {
  success: ["border-success", "bg-success/10"],
  warning: ["border-warning", "bg-warning/10"],
} as const;

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function wrapperOf(container: HTMLElement): HTMLElement {
  const wrapper = container.firstElementChild;
  expect(wrapper).not.toBeNull();
  return wrapper as HTMLElement;
}

describe("NoticeBanner", () => {
  it("renders its children as the message, with no wrapper paragraph", async () => {
    // The ErrorBanner contrast: that component wraps children in a styled `<p>`
    // the caller cannot reach. A notice does not, so a caller's own `Text` is
    // the only paragraph in the tree.
    const { container } = await render(<NoticeBanner>Check your inbox.</NoticeBanner>);

    const wrapper = wrapperOf(container);
    expect(wrapper.textContent).toBe("Check your inbox.");
    expect(wrapper.querySelector("p")).toBeNull();
  });

  it("passes through an app-owned data-testid onto the wrapper", async () => {
    const { container } = await render(
      <NoticeBanner data-testid="login-magic-link-sent">Sent.</NoticeBanner>,
    );

    const tagged = container.querySelector('[data-testid="login-magic-link-sent"]');
    expect(tagged).not.toBeNull();
    expect((tagged as HTMLElement).classList.contains("border-success")).toBe(true);
  });

  it("defaults to the success tone", async () => {
    const { container: bare } = await render(<NoticeBanner>Done.</NoticeBanner>);
    const { container: explicit } = await render(<NoticeBanner tone="success">Done.</NoticeBanner>);

    expect(classSet(wrapperOf(bare))).toEqual(classSet(wrapperOf(explicit)));
  });

  for (const [tone, colours] of Object.entries(TONE_COLOURS)) {
    it(`paints the ${tone} tone with its semantic token pair`, async () => {
      const { container } = await render(
        <NoticeBanner tone={tone as keyof typeof TONE_COLOURS}>Status</NoticeBanner>,
      );

      expect(classSet(wrapperOf(container))).toEqual([...SHAPE_CLASSES, ...colours].toSorted());
    });
  }

  it("gives the two tones distinct surfaces", async () => {
    // Two tones that painted the same tint would satisfy the per-tone
    // assertions above while being useless as a success/warning distinction.
    const { container: success } = await render(<NoticeBanner tone="success">A</NoticeBanner>);
    const { container: warning } = await render(<NoticeBanner tone="warning">A</NoticeBanner>);

    expect(classSet(wrapperOf(success))).not.toEqual(classSet(wrapperOf(warning)));
  });

  it("lets a caller className override a recipe utility", async () => {
    const { container } = await render(<NoticeBanner className="p-6">Done.</NoticeBanner>);

    const wrapper = wrapperOf(container);
    expect(wrapper.classList.contains("p-6")).toBe(true);
    expect(wrapper.classList.contains("p-3")).toBe(false);
  });

  it("keeps utilities the caller never mentions", async () => {
    // LoginScreen's MFA banner is the real call site: it adds `space-y-2` for a
    // heading plus an action link, and the recipe deliberately owns no vertical
    // rhythm of its own for it to fight with.
    const { container } = await render(
      <NoticeBanner className="space-y-2" tone="warning">
        Body.
      </NoticeBanner>,
    );

    expect(classSet(wrapperOf(container))).toEqual(
      [...SHAPE_CLASSES, ...TONE_COLOURS.warning, "space-y-2"].toSorted(),
    );
  });

  it("accepts arbitrary children so a caller can supply its own action row", async () => {
    const { container } = await render(
      <NoticeBanner tone="warning">
        <a href="/enrol">Enrol now</a>
      </NoticeBanner>,
    );

    const link = container.querySelector("a");
    expect(link).not.toBeNull();
    expect(link?.textContent).toBe("Enrol now");
  });

  it("passes through the rest of its props", async () => {
    const { container } = await render(
      <NoticeBanner data-testid="login-signed-in" role="status" aria-live="polite">
        You are now signed in.
      </NoticeBanner>,
    );

    const wrapper = container.querySelector('[data-testid="login-signed-in"]');
    expect(wrapper?.getAttribute("role")).toBe("status");
    expect(wrapper?.getAttribute("aria-live")).toBe("polite");
  });
});
