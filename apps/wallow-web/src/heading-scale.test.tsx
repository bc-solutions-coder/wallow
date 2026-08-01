import { CardTitle } from "@bc-solutions-coder/ui";
import {
  createSdkHarness,
  routeHarness,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { CreateOrganizationForm, OrganizationDetail } from "@features/organizations";
import { CreateInquiryForm } from "@features/inquiries";
import { MfaSettingsSection } from "@features/mfa";
import { ProfileSection } from "@features/settings";

/**
 * The MEASURED pin on wallow-web's card headings (Wallow-io5f).
 *
 * THE CLAIM. wallow-web reaches for a card heading down TWO paths, and after
 * this bead they must arrive at the same size:
 *   1. the catalog's `CardTitle`, whose recipe this bead retunes from `text-lg`
 *      (18px) to `text-xl` (20px);
 *   2. `Text` `variant="subheading"`, which is ALREADY `text-xl` — plus the five
 *      headings that reach it indirectly through `EmptyState`.
 * Neither path's call sites are edited by this bead, which is exactly what makes
 * them worth measuring: the standard only holds if no call site is quietly
 * overriding the size its recipe owns. `subheading` staying at 20px is a
 * load-bearing premise of the whole change rather than a coincidence, and the
 * second describe below is where it stops being taken on trust in this app
 * (`packages/ui/src/components/text/text.stories.tsx` → `SubheadingStandard`
 * pins the recipe itself).
 *
 * WHY 20px AND NOT 16px. `text-base` is the browser's default body size, so a
 * heading standardised there computes the SAME size as the copy beneath it and
 * the page's hierarchy survives on weight alone. `does not collapse into the
 * body copy beside it` below is that argument as an assertion rather than as a
 * comment.
 *
 * WHY MEASURED, NOT `toHaveClass("text-xl")`. The live class list is the
 * `twMerge` of the recipe with whatever `className` the call site passed, so a
 * caller utility can win the size axis the recipe thought it owned while a
 * class-string assertion stays green over the wrong box. Two of wallow-web's
 * OTHER headings do exactly that today — `LandingPage`'s two `<h3>`s pass
 * `className="text-lg"` over `variant="subheading"` — so this is a live
 * mechanism in this app, not a hypothetical.
 *
 * WHY PROBES RATHER THAN A `20px` LITERAL. Asserting the number would bake
 * Tailwind's current `text-xl` into this app's spec and go quietly wrong for a
 * fork that retunes its scale. Every claim is a comparison against a sibling
 * carrying the utility the heading is supposed to land on, and the probes double
 * as the vacuity guard: without `distinguishes the three type steps` proving the
 * stylesheet resolved, "the heading matches text-xl" would also be "the heading
 * matches text-lg" and this file could not fail.
 *
 * WHAT THIS FILE DOES NOT COVER. The two `CardTitle`s in
 * `src/app/routes/__root.tsx` (the error and not-found boundaries) need a router
 * to mount, and `apps/examples/minimal-app`'s two need a Tailwind pipeline its
 * vitest config does not wire. Both are covered by the recipe's own measured
 * story, `packages/ui/src/components/card/card.stories.tsx` → `HeadingScale`.
 */

/** A user payload rich enough for every section mounted here. */
const PROFILE = {
  id: "u1",
  email: "ada@lovelace.io",
  firstName: "Ada",
  lastName: "Lovelace",
  roles: ["Owner"],
  permissions: [],
};

/** The MFA status shape `MfaSettingsSection` gates its card on. */
const MFA_STATUS = { isEnabled: false, backupCodesRemaining: 0 };

/** The organization `OrganizationDetail` renders its sections under. */
const ORGANIZATION = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "0" };

/**
 * The live `CardTitle` the subheading path is compared against. `CardTitle`
 * renders an `<h2>`, so the sweep below excludes it by this id: it is the other
 * side of the comparison, not one of the app's own headings.
 */
const CROSS_PATH_TITLE = "cross-path-card-title";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

beforeEach(() => {
  harness = createSdkHarness();
});

/**
 * The three type steps in play, rendered beside whichever section is mounted.
 *
 * `text-xl` is the standard; `text-lg` is where `cardTitleRecipe` used to sit;
 * `text-base` is the body step a heading must stay above.
 */
function ScaleProbes() {
  return (
    <div data-testid="probes">
      <div data-testid="probe-base" className="text-base">
        probe
      </div>
      <div data-testid="probe-lg" className="text-lg">
        probe
      </div>
      <div data-testid="probe-xl" className="text-xl">
        probe
      </div>
    </div>
  );
}

/**
 * Mount every `CardTitle`-bearing section this app can render without a router,
 * probes alongside, and settle on the last of them to paint.
 *
 * One tree rather than one render per section because the claim is that the
 * headings AGREE: measured in a single layout, "they all match the probe" and
 * "they all match each other" are the same assertion.
 */
async function renderSections(): Promise<void> {
  harness.respond((call) => Response.json(call.path.includes("mfa") ? MFA_STATUS : PROFILE));

  renderWithWallow(
    <>
      <ProfileSection />
      <MfaSettingsSection />
      <CreateOrganizationForm />
      <ScaleProbes />
    </>,
    { harness },
  );

  await expect.element(page.getByTestId("organization-create-form")).toBeInTheDocument();
}

/** The computed value of `property` on the element carrying `testId`. */
function computed(testId: string, property: string): string {
  return globalThis.getComputedStyle(page.getByTestId(testId).element()).getPropertyValue(property);
}

/**
 * Every card heading on screen, as its computed `font-size`.
 *
 * Level 2 is the card-heading level here — these sections render no `<h1>` of
 * their own (the page shell's `PageHeader` owns that) — so this picks out
 * exactly the headings this bead governs whatever their testids, and two of the
 * three carry none at all.
 */
function headingFontSizes(): string[] {
  return page
    .getByRole("heading", { level: 2 })
    .all()
    .map((heading) => heading.element())
    .filter((heading) => heading.dataset["testid"] !== CROSS_PATH_TITLE)
    .map((heading) => globalThis.getComputedStyle(heading).getPropertyValue("font-size"));
}

/** The three steps, as the browser computed them. */
function steps(): { base: string; lg: string; xl: string } {
  return {
    base: computed("probe-base", "font-size"),
    lg: computed("probe-lg", "font-size"),
    xl: computed("probe-xl", "font-size"),
  };
}

describe("wallow-web's card headings take the catalog-wide scale", () => {
  it("distinguishes the three type steps at all", async () => {
    // The vacuity guard for every assertion below. With no stylesheet all three
    // probes compute the inherited size, and "the heading matches text-xl"
    // becomes true of a heading still rendering at text-lg.
    await renderSections();

    const { base, lg, xl } = steps();

    expect(
      new Set([base, lg, xl]),
      "the Tailwind pipeline resolved all three steps",
    ).toHaveProperty("size", 3);
  });

  it("mounts a card heading for every section under test", async () => {
    // The other half of the vacuity guard: a tree that rendered nothing, or a
    // section that swallowed its heading behind a loading branch, would make
    // the "every heading" assertions below true of a short list.
    await renderSections();

    expect(headingFontSizes()).toHaveLength(3);
  });

  it("renders every card heading at the subheading step", async () => {
    await renderSections();

    expect(headingFontSizes()).toEqual(Array.from({ length: 3 }, () => steps().xl));
  });

  it("leaves the step cardTitleRecipe used to hard-code", async () => {
    // Stated as an explicit absence because it is the regression, not a
    // restatement: `text-lg` is where every one of these sat before this bead.
    await renderSections();

    expect(new Set(headingFontSizes()).has(steps().lg), "no card heading left at text-lg").toBe(
      false,
    );
  });

  it("does not collapse into the body copy beside it", async () => {
    // The reason the standard is 20px rather than 16px. `text-base` is the
    // browser's default body size, so a heading standardised there computes the
    // same size as the copy under it. This assertion is what makes the choice
    // load-bearing instead of a preference recorded in a comment.
    await renderSections();

    const { base } = steps();
    const headings: string[] = headingFontSizes();

    expect(headings.length, "the tree rendered its headings").toBe(3);
    for (const heading of headings) {
      expect(
        px(heading),
        `a card heading at ${heading} does not outrank body copy at ${base}`,
      ).toBeGreaterThan(px(base));
    }
  });
});

/**
 * Mount the app's `subheading` path — every `Text variant="subheading"` heading
 * wallow-web renders, plus the `EmptyState` ones — probes alongside.
 *
 * `OrganizationDetail` is mounted rather than its parts because it CONTAINS
 * them: its own "Bound Clients" heading, the nested `MemberList`'s "Members"
 * heading, and — with both collections seeded empty — the two `EmptyState`
 * headings, which are the indirect route to `subheading` this app takes five
 * times. `CreateInquiryForm` adds the last direct call site.
 */
async function renderSubheadingPath(): Promise<void> {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": ORGANIZATION,
      "GET /v1/identity/organizations/o1/members": [],
      "GET /v1/identity/clients/by-tenant/o1": [],
    },
    { fallback: [] },
  );

  renderWithWallow(
    <>
      <OrganizationDetail orgId="o1" />
      <CreateInquiryForm />
      <CardTitle data-testid={CROSS_PATH_TITLE}>The other spelling</CardTitle>
      <ScaleProbes />
    </>,
    { harness },
  );

  await expect.element(page.getByTestId("inquiry-create-heading")).toBeInTheDocument();
}

describe("wallow-web's subheading headings sit on the same standard", () => {
  it("mounts the subheading path's headings", async () => {
    // The vacuity guard: `OrganizationDetail` renders most of these behind a
    // settled query, and a tree that hit an error branch would make every
    // "each of them" assertion below true of a shorter list. The count is
    // asserted as a floor rather than an exact number because this tree grows
    // whenever the screen gains a section, and a growing tree is not a
    // regression — an empty one is.
    await renderSubheadingPath();

    expect(headingFontSizes().length).toBeGreaterThanOrEqual(4);
  });

  it("renders every subheading heading at the standard step", async () => {
    // Green before this bead as well as after it — `subheading` is already
    // `text-xl`, and that is the point: this is the pin on the premise the whole
    // change rests on, not a restatement of the change.
    await renderSubheadingPath();

    const { xl } = steps();

    expect(new Set(headingFontSizes()), "one step across the subheading path").toEqual(
      new Set([xl]),
    );
  });

  it("agrees with the card heading rendered beside it", async () => {
    // The claim that makes 20px a STANDARD rather than a second opinion: the two
    // spellings of a card heading are indistinguishable on screen. Asserted
    // against a LIVE `CardTitle` in the same tree rather than against the probe
    // again — a spec that names a relation and then compares two literals cannot
    // fail when the relation breaks, which is the defect Wallow-dati records.
    await renderSubheadingPath();

    const cardTitle: string = computed(CROSS_PATH_TITLE, "font-size");

    for (const heading of headingFontSizes()) {
      expect(heading, `a subheading at ${heading} against a CardTitle at ${cardTitle}`).toBe(
        cardTitle,
      );
    }
  });

  it("keeps the page title a real step above its section headings", async () => {
    // The hierarchy this bead must not flatten, measured at the one place in
    // wallow-web where an h1 and an h2 share a screen: `OrganizationDetail`'s
    // `variant="title"` page heading over its `subheading` sections.
    await renderSubheadingPath();

    const title: number = px(computed("organization-detail-heading", "font-size"));

    for (const heading of headingFontSizes()) {
      expect(title, `a section heading at ${heading} is not below the page title`).toBeGreaterThan(
        px(heading),
      );
    }
  });
});

/**
 * A computed CSS length as a number, for the one claim that is an ORDERING
 * rather than an equality. The unit is stripped rather than parsed off: `Number`
 * on a `"16px"` string is `NaN`, which would make every comparison above fail
 * for the wrong reason.
 */
function px(length: string): number {
  return Number(length.replace("px", ""));
}
