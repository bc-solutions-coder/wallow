import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { RegisterAppForm } from "./RegisterAppForm";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * App branding/logo-upsert reachability spec (Wallow-ffpq.3.6) — the optional
 * "Branding" section that upserts a display name, tagline, and logo file for the
 * app.
 * The branding section lives on the same register-app page, so once
 * `RegisterAppForm` is mounted (at `/dashboard/apps/register`, Wallow-ffpq.3.5)
 * the branding display-name / tagline / logo inputs must be reachable in the
 * form view. Testids follow the component's own `app-*` convention (the
 * `register-app-*` testids were renamed to `app-*`).
 *
 * This is a render-only reachability spec (no query/mutation fires); the SDK
 * client mock is installed only to guard against a real network call
 * (Wallow-evd5.2.6 — the retired `getWallowSdk()` facade is no longer in the
 * path).
 */

describe("RegisterAppForm branding/logo upsert", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders an optional branding display-name input in the form view", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });
    await expect.element(page.getByTestId("app-branding-display-name")).toBeInTheDocument();
  });

  it("renders an optional branding tagline input", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });
    await expect.element(page.getByTestId("app-branding-tagline")).toBeInTheDocument();
  });

  it("renders a logo file input for the branding upsert", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });
    await expect.element(page.getByTestId("app-logo-input")).toBeInTheDocument();
  });
});
