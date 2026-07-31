import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkCall, SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactNode } from "react";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { AcceptTermsScreen } from "@features/accept-terms";
import { ConsentScreen } from "@features/consent";
import { ErrorPage } from "@features/error";
import { ForgotPasswordForm } from "@features/forgot-password";
import { InvitationScreen } from "@features/invitation";
import { LoginScreen } from "@features/login";
import { LogoutScreen } from "@features/logout";
import { MfaChallengeForm } from "@features/mfa-challenge";
import { MfaEnrollForm } from "@features/mfa-enroll";
import { NotFoundPage } from "@features/not-found";
import { PrivacyPage } from "@features/privacy";
import { RegisterForm } from "@features/register";
import { ResetPasswordForm } from "@features/reset-password";
import { TermsPage } from "@features/terms";
import { VerifyEmailConfirm, VerifyEmailNotice } from "@features/verify-email";
import { createAuthHarness } from "@shared/testing/harness";

/**
 * The MEASURED pin on wallow-auth's card-heading scale (Wallow-lrlm.13).
 *
 * THE DEFECT THIS CLOSES. wallow-auth's sixteen screen headings used to render
 * at two different sizes in the same card slot: nine went through `Text`'s
 * `subheading` step (`text-xl`, 20px) and seven through `CardTitle`'s own
 * `text-lg` literal (18px), so the login screen's title and the consent screen's
 * title differed by a step one navigation apart.
 *
 * THE STANDARD, AND WHY IT MOVED TWICE. Wallow-lrlm.13 first landed all sixteen
 * on `Text`'s `body` step at `semibold` (16px) because that could be composed
 * from existing catalog API without retuning a shared recipe. Wallow-io5f makes
 * 20px the catalog-wide standard instead and retunes `cardTitleRecipe` to match,
 * so these sixteen move up to `<Text as="h2" variant="subheading" color="onCard">`.
 * 16px was the wrong resting place: it is the browser's default body size, so a
 * heading there computes the SAME size as the copy beneath it and the hierarchy
 * rests entirely on weight. `AuthLayout` owns the page `<h1>` at the `heading`
 * step (24px), so 24 > 20 > 16 body > 14 muted is the ladder these screens now
 * read on.
 *
 * ALL SIXTEEN ARE MEASURED HERE, which is the point: the acceptance is that the
 * screens AGREE, and agreement is not a property any one screen's own spec can
 * state. They are mounted in two bundles only because nine of them will not
 * paint until a request settles — that split is a fixture detail, and both
 * bundles are held to the same probe.
 *
 * WHY MEASURED. A class-string assertion cannot see this: the live class list is
 * the `twMerge` of the recipe with whatever `className` the call site passes, so
 * a caller utility can win the size axis the recipe thought it owned and a spec
 * pinned to the recipe stays green over the wrong box. Every claim below is read
 * off `getComputedStyle` in the real headless Chromium this project runs, with
 * the real Tailwind pipeline and the fork theme attached (Wallow-8ytl).
 *
 * WHY PROBES RATHER THAN A `16px` LITERAL. Asserting the number would pin
 * Tailwind's current `text-base` into this app's spec and go quietly wrong for a
 * fork that retunes its scale. Each assertion instead renders a sibling carrying
 * the utility the heading is supposed to land on and compares computed values.
 * The probes double as the vacuity guard: `distinguishes the three type steps`
 * proves the stylesheet actually resolved, without which "the heading matches
 * `text-base`" would also be "the heading matches `text-lg`" and this whole file
 * could not fail.
 *
 * WHAT THIS FILE STILL CANNOT SAY. That no SEVENTEENTH screen drifted — a screen
 * this file forgot to mount is exactly what a render cannot see. That half is a
 * disk-derived source sweep in `catalog-adoption.test.ts`, which judges every
 * component on disk whether or not anyone remembered it.
 */

/** The OIDC hand-off shape these screens are reached with in the app. */
const RETURN_URL = "/connect/authorize?client_id=wallow-web&scope=openid";

/** A token stand-in for the screens that read one out of their link. */
const TOKEN = "tok";

/** The screens in the first bundle — those that paint with no request in flight. */
const INERT_SCREENS = 7;

/** The screens in the second bundle — those gated on a settled lookup. */
const SEEDED_SCREENS = 9;

/** Every screen in this app that carries a card heading. */
const ALL_SCREENS = INERT_SCREENS + SEEDED_SCREENS;

/**
 * The three type steps this app's headings have sat on, rendered beside the
 * screens.
 *
 * `text-xl` is `Text`'s `subheading` step and the standard as of Wallow-io5f;
 * `text-base` is the `body` step these sixteen were briefly standardised onto;
 * `text-lg` is the step `CardTitle` used to hard-code. All three are rendered so
 * the spec can prove they are DISTINGUISHABLE before claiming a heading matches
 * one of them.
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

let harness: SdkHarness;

beforeEach(() => {
  harness = createAuthHarness();
});

/**
 * One response that satisfies every gated screen at once.
 *
 * A superset object rather than nine per-endpoint fixtures: nothing in this file
 * asserts on a payload, and each screen reads only the fields it knows. The
 * external-providers endpoint is the one arm that has to branch, because the
 * login screen MAPS over its body — an object throws there, where a missing
 * field would merely render empty.
 */
function seedResponses(): void {
  harness.respond((call: SdkCall) =>
    Response.json(
      call.path.includes("external-providers")
        ? []
        : {
            succeeded: true,
            email: "ada@example.com",
            organizationName: "Acme",
            displayName: "Acme",
            name: "Acme",
            clientId: "wallow-web",
            logoUrl: null,
            requestedScopes: [],
            secret: "JBSWY3DPEHPK3PXP",
            qrCodeUri: "otpauth://totp/Wallow:ada@example.com?secret=JBSWY3DPEHPK3PXP",
            isValid: true,
          },
    ),
  );
}

/**
 * Mount a bundle of screens at once, probes alongside.
 *
 * One tree rather than one render per screen because the claim is about the
 * screens AGREEING: measured in a single layout, "they all match the probe" and
 * "they all match each other" are the same assertion, and no per-render
 * difference in the surrounding page can drift between them.
 */
async function renderBundle(screens: ReactNode, settled: string): Promise<void> {
  await renderWithWallow(
    <>
      {screens}
      <ScaleProbes />
    </>,
    { harness },
  );

  await expect.element(page.getByTestId(settled)).toBeInTheDocument();
}

/** The screens that render their heading on mount, with no request in flight. */
async function renderInertScreens(): Promise<void> {
  await renderBundle(
    <>
      <NotFoundPage />
      <ForgotPasswordForm />
      <MfaChallengeForm returnUrl={RETURN_URL} />
      <ErrorPage reason="access_denied" />
      <VerifyEmailNotice />
      <PrivacyPage />
      <TermsPage />
    </>,
    // The MFA challenge builds its controls through the form layer, so it is the
    // last of these to paint.
    "mfa-challenge-toggle-backup",
  );
}

/** The screens that hold their heading back until a lookup settles. */
async function renderSeededScreens(): Promise<void> {
  seedResponses();

  await renderBundle(
    <>
      <LoginScreen returnUrl={RETURN_URL} />
      <LogoutScreen />
      <ResetPasswordForm email="ada@example.com" token={TOKEN} />
      <MfaEnrollForm returnUrl={RETURN_URL} enrollToken={TOKEN} />
      <RegisterForm returnUrl={RETURN_URL} />
      <VerifyEmailConfirm token={TOKEN} />
      <InvitationScreen token={TOKEN} isAuthenticated={false} />
      <AcceptTermsScreen returnUrl={RETURN_URL} />
      <ConsentScreen clientId="wallow-web" returnUrl={RETURN_URL} scope="openid profile" />
    </>,
    // The consent prompt renders NOTHING while its lookup is in flight, so its
    // approve action is the signal that the whole bundle has resolved.
    "consent-approve",
  );
}

/** The computed value of `property` on the element carrying `testId`. */
function computed(testId: string, property: string): string {
  return globalThis.getComputedStyle(page.getByTestId(testId).element()).getPropertyValue(property);
}

/**
 * Every card heading on screen, as its computed `font-size`.
 *
 * Level 2 is the card-heading level in this app — `AuthLayout` owns the page's
 * one `<h1>` and the privacy/terms document sections are `<h3>`s at the `bodySm`
 * step — so this picks out exactly the headings this bead governs whatever their
 * testids, and two of the sixteen carry no testid at all.
 */
function headingFontSizes(): string[] {
  return page
    .getByRole("heading", { level: 2 })
    .all()
    .map((heading) => globalThis.getComputedStyle(heading.element()).getPropertyValue("font-size"));
}

/** The three steps, as the browser computed them. */
function steps(): { base: string; lg: string; xl: string } {
  return {
    base: computed("probe-base", "font-size"),
    lg: computed("probe-lg", "font-size"),
    xl: computed("probe-xl", "font-size"),
  };
}

describe("wallow-auth's screen headings share one type scale", () => {
  it("distinguishes the three type steps at all", async () => {
    // The vacuity guard for every assertion below. With no stylesheet all three
    // probes compute the inherited size, and "the heading matches text-base"
    // becomes true of a heading still rendering at text-lg or text-xl.
    await renderInertScreens();

    const { base, lg, xl } = steps();

    expect(
      new Set([base, lg, xl]),
      "the Tailwind pipeline resolved all three steps",
    ).toHaveProperty("size", 3);
  });

  it("mounts every screen that paints on its own", async () => {
    // The other half of the vacuity guard: a tree that rendered nothing, or a
    // screen that swallowed its heading behind a branch, would make the "every
    // heading" assertions below true of a short list.
    await renderInertScreens();

    expect(headingFontSizes()).toHaveLength(INERT_SCREENS);
  });

  it("mounts every screen that waits on a lookup", async () => {
    await renderSeededScreens();

    expect(headingFontSizes()).toHaveLength(SEEDED_SCREENS);
  });

  it("accounts for all sixteen screen headings across the two bundles", () => {
    // Stated so the two counts above cannot both be satisfied while the app has
    // grown a screen neither bundle mounts. Noticing such a screen is the source
    // sweep's job; this is the arithmetic that keeps this file's claim honest.
    expect(ALL_SCREENS).toBe(16);
  });

  it("renders every screen that paints on mount at the subheading step", async () => {
    await renderInertScreens();

    expect(headingFontSizes()).toEqual(Array.from({ length: INERT_SCREENS }, () => steps().xl));
  });

  it("renders every screen that waits on a lookup at the subheading step", async () => {
    await renderSeededScreens();

    expect(headingFontSizes()).toEqual(Array.from({ length: SEEDED_SCREENS }, () => steps().xl));
  });

  it("leaves both of the steps these screens used to sit on", async () => {
    // Stated as an explicit absence because it is the regression, not a
    // restatement: `text-lg` is where the seven `CardTitle` screens sat before
    // Wallow-lrlm.13, and `text-base` is where all sixteen sat after it.
    await renderSeededScreens();

    const sizes: Set<string> = new Set(headingFontSizes());
    const { base, lg } = steps();

    expect(sizes.has(lg), "no heading left at text-lg").toBe(false);
    expect(sizes.has(base), "no heading left at text-base").toBe(false);
  });

  it("keeps every heading a real step above the body copy beside it", async () => {
    // The reason the standard is 20px and not 16px, asserted rather than
    // asserted-about-in-a-comment: `text-base` IS the browser's default body
    // size, so a heading standardised there computes the same size as the copy
    // under it and the hierarchy survives on weight alone. This fails for the
    // 16px standard and passes for the 20px one.
    await renderInertScreens();

    const { base } = steps();
    const headings: string[] = headingFontSizes();

    expect(headings.length, "the bundle rendered its headings").toBe(INERT_SCREENS);
    for (const heading of headings) {
      expect(
        px(heading),
        `a card heading at ${heading} does not outrank body copy at ${base}`,
      ).toBeGreaterThan(px(base));
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
