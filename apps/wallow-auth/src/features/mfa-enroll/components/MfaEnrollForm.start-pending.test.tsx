import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import {
  type SdkCall,
  type SdkHarness,
  type SdkResponder,
} from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "../../../test/harness";
import { MfaEnrollForm } from "./MfaEnrollForm";

/**
 * The IN-FLIGHT surface of enrollment start (Wallow-evd5.3.2).
 *
 * The sibling `MfaEnrollForm.test.tsx` pins what this screen shows once the
 * enrollment call has SETTLED — the secret, the QR, the error copy per failure
 * token. What no test covers today is the window WHILE the call is in flight,
 * and that window is precisely what the refactor rewires: `loading` stops being
 * a hand-rolled `useState` toggled either side of a `try/catch` and becomes the
 * mutation's `isPending`. A mutation whose pending flag is not threaded back
 * into the intro branch would leave an enabled retry button sitting over a live
 * request — a second `enrollTotp` that mints a second secret and invalidates the
 * QR the user has already scanned, which is the exact bug the oracle's whole
 * `PersistedEnrollment` relay existed to prevent.
 *
 * So these tests are the regression net for the refactor rather than its red:
 * they hold on the pre-refactor implementation too, and they are here to FAIL
 * loudly if the mutation's pending/error state is not wired back into the same
 * three render decisions the useState pair drove.
 *
 * TEST SEAM: `@bc-solutions-coder/testing/sdk-harness` (Wallow-pu6a.5.1). The
 * SDK is the REAL one and only its `fetch` is faked, so the screen's whole
 * pipeline — request-scoped SDK -> generated operation -> CSRF interceptor ->
 * serialization -> error shaping -> React Query — runs here. Nothing mocks the
 * SDK package, and there is no app-level facade left to mock (Wallow-pu6a.5.5).
 * `renderWithWallow` supplies the router context the screen reads its SDK off,
 * and `createAuthHarness()` pins the harness origin to this app's root-mounted
 * API surface, so the recorded `call.path` is the endpoint path verbatim.
 *
 * That matters more here than in a settled-state spec: "in flight" is a property
 * of the TRANSPORT, and holding the real fetch open is a truer statement of it
 * than resolving a stubbed promise late. `isSafeReturnUrl` is now the
 * real pure builder rather than a spy forced to `true` — these tests pass no
 * returnUrl, so it is never consulted.
 */

const mocks = vi.hoisted(() => ({
  navigate: vi.fn(),
}));

vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const SECRET = "JBSWY3DPEHPK3PXP";
const QR_URI = "otpauth://totp/Wallow:user@test.local?secret=JBSWY3DPEHPK3PXP&issuer=Wallow";

/** `POST /v1/identity/mfa/enroll/totp` — the call whose in-flight window this spec is about. */
const ENROLL_PATH = "/v1/identity/mfa/enroll/totp";

/** `POST /v1/identity/mfa/enroll/exchange-token` — the enrollToken path's first hop. */
const EXCHANGE_PATH = "/v1/identity/mfa/enroll/exchange-token";

/** `POST /v1/identity/mfa/enroll/confirm` — settled-state surface, defaulted for completeness. */
const CONFIRM_PATH = "/v1/identity/mfa/enroll/confirm";

/** The enrollment payload: a one-time secret plus the QR the user scans. */
function enrolledResponse(): Response {
  return Response.json({ secret: SECRET, qrUri: QR_URI });
}

/**
 * What the endpoint really puts on the wire for the unrecognised failure: a 500
 * whose body names no machine code, which the client's error interceptor shapes
 * into a `WallowError` with `status: 500` and `code: "UNKNOWN"`. That lands on
 * the generic "could not start" copy rather than the session or expired-link
 * branches — the same case the old hand-built rejection object stood in for,
 * now produced by the real pipeline.
 */
function serverRejectionResponse(): Response {
  return Response.json({ succeeded: false }, { status: 500 });
}

let harness: SdkHarness;

/**
 * One-shot enroll responders, consumed in order — the transport-level equivalent
 * of the `mockReturnValueOnce` / `mockRejectedValueOnce` the facade spies used.
 * Anything left over falls through to {@link enrollDefault}.
 */
let enrollQueue: SdkResponder[] = [];

/** The standing enroll behaviour once the queue is drained (`mockResolvedValue`). */
let enrollDefault: SdkResponder;

/** Every recorded call to the enroll endpoint — the "how many starts" question. */
function enrollCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path === ENROLL_PATH);
}

/**
 * An enroll response that hangs until the returned `release` is called, so the
 * in-flight window can be asserted rather than raced. Every test releases before
 * it ends: a promise left pending outlives the test and its resolution would
 * land on an unmounted tree.
 *
 * Queued as a ONE-SHOT so a later start falls back to the standing behaviour,
 * matching the `mockReturnValueOnce` this replaces.
 */
function hangingEnroll(): () => void {
  let release!: () => void;
  const hanging: Promise<Response> = new Promise<Response>((resolve) => {
    release = () => {
      resolve(enrolledResponse());
    };
  });
  enrollQueue.push(async () => await hanging);

  return () => {
    release();
  };
}

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

function renderForm(props: { returnUrl?: string; enrollToken?: string } = {}) {
  return renderWithClient(<MfaEnrollForm {...props} />);
}

beforeEach(() => {
  vi.clearAllMocks();
  harness = createAuthHarness();
  enrollQueue = [];
  enrollDefault = enrolledResponse;

  harness.respond((call: SdkCall) => {
    if (call.path === ENROLL_PATH) {
      return (enrollQueue.shift() ?? enrollDefault)(call);
    }
    if (call.path === EXCHANGE_PATH) {
      return Response.json({ succeeded: true });
    }
    if (call.path === CONFIRM_PATH) {
      return Response.json({ succeeded: true, backupCodes: [] });
    }
    return Response.json({});
  });
});

describe("MfaEnrollForm — while enrollment is starting", () => {
  it("does not offer the retry over a live request", async () => {
    // `mfa-enroll-begin-setup` is a RETRY in all but name (see the component
    // header): the intro branch is reachable only once enrollment has failed.
    // Offering it while the first call is still open invites a second
    // `enrollTotp`, and a second secret silently invalidates the QR the user is
    // mid-way through scanning.
    const release: () => void = hangingEnroll();
    renderForm();

    // Wait for the request to REACH the transport before asserting or releasing:
    // the screen enters its pending state a tick before `fetch` is called, and
    // releasing into that gap would leave the never-settling response installed.
    await vi.waitFor(() => {
      expect(enrollCalls()).toHaveLength(1);
    });
    expect(page.getByTestId("mfa-enroll-begin-setup").query()).toBeNull();

    release();
    await expect.element(page.getByTestId("mfa-enroll-secret")).toBeInTheDocument();
  });

  it("shows neither the secret nor an error until the call settles", async () => {
    // The in-flight state is its own thing: not the success branch, and not a
    // failure. A mutation whose `isPending` is dropped on the floor would fall
    // straight through to one of the other two.
    const release: () => void = hangingEnroll();
    renderForm();

    await vi.waitFor(() => {
      expect(enrollCalls()).toHaveLength(1);
    });
    expect(page.getByTestId("mfa-enroll-secret").query()).toBeNull();
    expect(page.getByTestId("mfa-enroll-error").query()).toBeNull();

    release();
    await expect.element(page.getByTestId("mfa-enroll-secret")).toBeInTheDocument();
  });

  it("stays pending across the token exchange and the start call", async () => {
    // The enrollToken path makes TWO round trips before anything renders. The
    // retry must stay withheld across BOTH — a gap between them is a live
    // begin-setup button in the middle of a flow the user did not finish. With
    // the real transport under the spec, both hops are now really on the wire.
    const release: () => void = hangingEnroll();
    renderForm({ enrollToken: "enroll-token-abc123" });

    await vi.waitFor(() => {
      expect(enrollCalls()).toHaveLength(1);
    });
    expect(harness.calls.filter((call: SdkCall) => call.path === EXCHANGE_PATH)).toHaveLength(1);
    expect(page.getByTestId("mfa-enroll-begin-setup").query()).toBeNull();

    release();
    await expect.element(page.getByTestId("mfa-enroll-secret")).toBeInTheDocument();
  });
});

describe("MfaEnrollForm — while a retry is in flight", () => {
  it("clears the standing error as soon as the retry starts", async () => {
    // The component header's rule, previously the `setErrorMessage(null)` at the
    // top of `startEnroll`: a stale error sitting above an in-flight retry is a
    // lie about the request the user just made. Under a mutation this has to be
    // an eager reset (a `mutate`-time clear, or reading `isPending` ahead of
    // `error`) — deriving the banner from `mutation.error` alone would keep the
    // dead error on screen for the whole retry.
    enrollQueue.push(serverRejectionResponse);
    const user = userEvent.setup();
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-error")).toBeInTheDocument();

    const release: () => void = hangingEnroll();
    await user.click(page.getByTestId("mfa-enroll-begin-setup"));

    await vi.waitFor(() => {
      expect(enrollCalls()).toHaveLength(2);
    });
    expect(page.getByTestId("mfa-enroll-error").query()).toBeNull();

    release();
    await expect.element(page.getByTestId("mfa-enroll-secret")).toBeInTheDocument();
  });

  it("withdraws the retry button so the retry cannot be doubled", async () => {
    // Same second-secret hazard as the mount call, reached by the other route.
    enrollQueue.push(serverRejectionResponse);
    const user = userEvent.setup();
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-begin-setup")).toBeInTheDocument();

    const release: () => void = hangingEnroll();
    await user.click(page.getByTestId("mfa-enroll-begin-setup"));

    await vi.waitFor(() => {
      expect(enrollCalls()).toHaveLength(2);
    });
    expect(page.getByTestId("mfa-enroll-begin-setup").query()).toBeNull();

    release();
    await expect.element(page.getByTestId("mfa-enroll-secret")).toBeInTheDocument();
  });

  it("brings the retry back when the retry itself fails", async () => {
    // The pending flag has to come back DOWN on the error path too. A mutation
    // left reading as pending after a rejection strands the user on a dead
    // screen with no way to try again.
    enrollDefault = serverRejectionResponse;
    const user = userEvent.setup();
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-begin-setup")).toBeInTheDocument();
    await user.click(page.getByTestId("mfa-enroll-begin-setup"));

    await vi.waitFor(() => {
      expect(enrollCalls()).toHaveLength(2);
    });
    await expect.element(page.getByTestId("mfa-enroll-begin-setup")).toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-enroll-error")).toBeInTheDocument();
  });
});
