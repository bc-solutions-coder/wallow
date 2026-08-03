import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import {
  createPassthroughHarness,
  type SdkCall,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { useState } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { useEnrollmentStart } from "./use-enrollment-start";

/**
 * The enrollment opening sequence, as a unit.
 *
 * `MfaEnrollForm.test.tsx` drives this through the screen, but the four properties
 * the sequence is built around are invisible from there: the screen shows one busy
 * state and cannot say which hop produced it, nor how many `enroll/totp` calls
 * stood behind a single QR. All four are read here off the recorded requests and
 * off a LOG of every render's `loading`, which is the only way to assert that a
 * value was never briefly false.
 *
 * Real SDK over a faked fetch, so "fired once" is counted on the wire rather than
 * on a spy.
 */

const SECRET = "JBSWY3DPEHPK3PXP";
const QR_URI = "otpauth://totp/Wallow:user@test.local?secret=JBSWY3DPEHPK3PXP&issuer=Wallow";
const ENROLL_TOKEN = "enroll-token-abc123";

const TOTP_ENDPOINT = "/v1/identity/mfa/enroll/totp";
const EXCHANGE_ENDPOINT = "/v1/identity/mfa/enroll/exchange-token";

const OK = 200;
const BAD_REQUEST = 400;

type EndpointResponder = () => Response | Promise<Response>;

const okTotp: EndpointResponder = () =>
  Response.json({ secret: SECRET, qrUri: QR_URI }, { status: OK });

const okExchange: EndpointResponder = () => Response.json({ succeeded: true }, { status: OK });

/** The 60-second hand-off token, missed. */
const expiredExchange: EndpointResponder = () =>
  Response.json({ succeeded: false, error: "invalid_or_expired_token" }, { status: BAD_REQUEST });

/** A hop that never answers, which is how an in-flight state is held open to look at. */
const neverAnswers: EndpointResponder = () => new Promise<Response>(() => {});

let harness: SdkHarness;

/** Every render's `loading`, in order — the seam for properties (3) and (4). */
let loadingLog: boolean[] = [];

function program(overrides: { totp?: EndpointResponder; exchange?: EndpointResponder } = {}): void {
  const totp: EndpointResponder = overrides.totp ?? okTotp;
  const exchange: EndpointResponder = overrides.exchange ?? okExchange;

  harness.respond((call: SdkCall) => {
    switch (call.path) {
      case TOTP_ENDPOINT: {
        return totp();
      }
      case EXCHANGE_ENDPOINT: {
        return exchange();
      }
      default: {
        return Response.json({}, { status: OK });
      }
    }
  });
}

/** The paths this hook touched, in the order it touched them. */
function pathsCalled(): readonly string[] {
  return harness.calls
    .map((call: SdkCall) => call.path)
    .filter((path: string) => path === TOTP_ENDPOINT || path === EXCHANGE_ENDPOINT);
}

function callsTo(path: string): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path === path);
}

/**
 * Renders the hook's whole surface, plus the two levers that could re-fire the
 * sequence: a re-render that changes no input, and a `blocked` flip — which DOES
 * change one of the effect's dependencies, and is the reachable path a screen has
 * (its returnUrl verdict is a rendered value).
 */
function Probe({
  enrollToken,
  blocked = false,
}: {
  readonly enrollToken?: string;
  readonly blocked?: boolean;
}): ReactElement {
  const [nudges, setNudges] = useState(0);
  const [isBlocked, setIsBlocked] = useState(blocked);
  const start = useEnrollmentStart(enrollToken, isBlocked);

  loadingLog.push(start.loading);

  return (
    <div>
      <div data-testid="secret">{start.secret ?? ""}</div>
      <div data-testid="qr-uri">{start.qrUri ?? ""}</div>
      <div data-testid="loading">{String(start.loading)}</div>
      <div data-testid="error">{start.error ?? ""}</div>
      <div data-testid="nudges">{nudges}</div>
      <div data-testid="blocked">{String(isBlocked)}</div>
      <button
        type="button"
        data-testid="toggle-block"
        onClick={() => {
          setIsBlocked((was: boolean) => !was);
        }}
      >
        Toggle block
      </button>
      <button
        type="button"
        data-testid="begin-setup"
        onClick={() => {
          start.beginSetup();
        }}
      >
        Begin setup
      </button>
      <button
        type="button"
        data-testid="nudge"
        onClick={() => {
          setNudges((count: number) => count + 1);
        }}
      >
        Re-render
      </button>
    </div>
  );
}

function renderProbe(props: { enrollToken?: string; blocked?: boolean } = {}) {
  return renderWithWallow(<Probe {...props} />, { harness });
}

/** Wait for `enroll/totp` to land. */
async function waitForSecret(): Promise<void> {
  await expect.element(page.getByTestId("secret")).toHaveTextContent(SECRET);
}

beforeEach(() => {
  harness = createPassthroughHarness();
  loadingLog = [];
  program();
});

describe("useEnrollmentStart", () => {
  describe("the sequence", () => {
    it("starts enrollment straight away when there is no hand-off token", async () => {
      await renderProbe();
      await waitForSecret();

      expect(pathsCalled()).toEqual([TOTP_ENDPOINT]);
      await expect.element(page.getByTestId("qr-uri")).toHaveTextContent(QR_URI);
      await expect.element(page.getByTestId("loading")).toHaveTextContent("false");
    });

    it("exchanges the hand-off token BEFORE starting enrollment", async () => {
      // Order is the whole contract: the exchange is what mints the
      // `Identity.MfaPartial` cookie, and an `enroll/totp` fired first has no
      // session to resolve and 401s — reporting a session problem when the real
      // fault is the link.
      await renderProbe({ enrollToken: ENROLL_TOKEN });
      await waitForSecret();

      expect(pathsCalled()).toEqual([EXCHANGE_ENDPOINT, TOTP_ENDPOINT]);
    });

    it("does not start enrollment at all when the exchange fails", async () => {
      // Enrolling anyway would just 401 and blame the session for an expired link.
      program({ exchange: expiredExchange });
      await renderProbe({ enrollToken: ENROLL_TOKEN });

      await expect.element(page.getByTestId("error")).toHaveTextContent("enrollment link");
      expect(callsTo(TOTP_ENDPOINT)).toHaveLength(0);
      await expect.element(page.getByTestId("loading")).toHaveTextContent("false");
    });

    it("fires nothing while blocked", async () => {
      // A refused destination never enrolls: do not make a user set up a second
      // factor for somewhere already decided against.
      await renderProbe({ enrollToken: ENROLL_TOKEN, blocked: true });
      await expect.element(page.getByTestId("loading")).toHaveTextContent("true");

      expect(pathsCalled()).toEqual([]);
    });
  });

  describe("firing once", () => {
    it("does not re-enroll when the component re-renders", async () => {
      // A second `enroll/totp` mints a SECOND secret and silently invalidates the
      // QR the user has already scanned, so the sequence is bound to the mount.
      await renderProbe({ enrollToken: ENROLL_TOKEN });
      await waitForSecret();

      await userEvent.click(page.getByTestId("nudge"));
      await userEvent.click(page.getByTestId("nudge"));
      await expect.element(page.getByTestId("nudges")).toHaveTextContent("2");

      expect(callsTo(EXCHANGE_ENDPOINT)).toHaveLength(1);
      expect(callsTo(TOTP_ENDPOINT)).toHaveLength(1);
    });

    it("does not re-enroll when `blocked` flips back and forth", async () => {
      // The test above cannot catch a missing guard on its own: every dependency
      // of the effect is stable, so a plain re-render never re-runs it. `blocked`
      // is the one that genuinely moves — it is a screen's rendered returnUrl
      // verdict — and re-running the sequence on it is what `startedRef` stops.
      await renderProbe({ enrollToken: ENROLL_TOKEN });
      await waitForSecret();

      await userEvent.click(page.getByTestId("toggle-block"));
      await expect.element(page.getByTestId("blocked")).toHaveTextContent("true");
      await userEvent.click(page.getByTestId("toggle-block"));
      await expect.element(page.getByTestId("blocked")).toHaveTextContent("false");

      expect(callsTo(EXCHANGE_ENDPOINT)).toHaveLength(1);
      expect(callsTo(TOTP_ENDPOINT)).toHaveLength(1);
    });

    it("re-enrolls only when `beginSetup` asks it to", async () => {
      // The intro branch's retry — the one deliberate second start, and it does
      // not repeat the exchange, whose cookie is already minted.
      await renderProbe({ enrollToken: ENROLL_TOKEN });
      await waitForSecret();

      await userEvent.click(page.getByTestId("begin-setup"));
      await expect.poll(() => callsTo(TOTP_ENDPOINT).length).toBe(2);

      expect(callsTo(EXCHANGE_ENDPOINT)).toHaveLength(1);
    });
  });

  describe("the busy state", () => {
    it("reads as busy on the FIRST paint of a token flow", async () => {
      // The intro branch's "Begin setup" is a retry, and offering it before the
      // cookie exists invites an `enroll/totp` that can only 401. Seeded from the
      // prop, so no frame between mount and the exchange offers that button.
      program({ exchange: neverAnswers });
      await renderProbe({ enrollToken: ENROLL_TOKEN });

      expect(loadingLog[0]).toBe(true);
      expect(callsTo(TOTP_ENDPOINT)).toHaveLength(0);
    });

    it("is idle on the first paint when there is no token to exchange", async () => {
      // The other half of the same seed: nothing is in flight yet, and claiming
      // otherwise would be a busy state with no request behind it.
      program({ totp: neverAnswers });
      await renderProbe();

      expect(loadingLog[0]).toBe(false);
    });

    it("never drops between the exchange and the start", async () => {
      // The hand-off is the whole point of ordering the two updates: a single
      // false here is a frame in which the screen offers a live retry button
      // between two requests that are still mid-sequence.
      program({ totp: neverAnswers });
      await renderProbe({ enrollToken: ENROLL_TOKEN });

      await expect.poll(() => callsTo(TOTP_ENDPOINT).length).toBe(1);
      expect(loadingLog).not.toContain(false);
    });
  });

  describe("failure copy", () => {
    it("blames the session, not the code, when the start has none", async () => {
      // No number of retries mints a partial-auth cookie, so a "try again"
      // message would loop the user forever.
      program({ totp: () => Response.json({ error: "no_auth_session" }, { status: 401 }) });
      await renderProbe();

      await expect.element(page.getByTestId("error")).toHaveTextContent("sign in again");
      await expect.element(page.getByTestId("loading")).toHaveTextContent("false");
    });

    it("clears the message when a retry opens", async () => {
      // Cleared EAGERLY at mutate time; deriving it from the mutation's error
      // would leave the dead message standing above an in-flight retry.
      let failed = false;
      program({
        totp: () => {
          if (failed) {
            return neverAnswers();
          }
          failed = true;
          return Response.json({ error: "no_auth_session" }, { status: 401 });
        },
      });
      await renderProbe();
      await expect.element(page.getByTestId("error")).toHaveTextContent("sign in again");

      await userEvent.click(page.getByTestId("begin-setup"));

      await expect.element(page.getByTestId("error")).toHaveTextContent("");
    });
  });
});
