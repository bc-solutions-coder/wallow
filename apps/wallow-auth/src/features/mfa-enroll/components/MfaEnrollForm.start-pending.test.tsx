import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import {
  createPassthroughHarness,
  type SdkCall,
  type SdkHarness,
  type SdkResponder,
} from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { MfaEnrollForm } from "./MfaEnrollForm";

/**
 * The IN-FLIGHT window of enrollment start; the sibling `MfaEnrollForm.test.tsx` covers what
 * the screen shows once the call has SETTLED.
 *
 * A retry button left live over an open request means a second `enrollTotp`, which mints a
 * second secret and invalidates the QR the user has already scanned.
 *
 * Real SDK over a faked fetch (sdk-harness) — "in flight" is a property of the TRANSPORT, so
 * the response is held open rather than a stubbed promise resolved late.
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

/** The call whose in-flight window this spec is about. */
const ENROLL_PATH = "/v1/identity/mfa/enroll/totp";

/** The enrollToken path's first hop. */
const EXCHANGE_PATH = "/v1/identity/mfa/enroll/exchange-token";

const CONFIRM_PATH = "/v1/identity/mfa/enroll/confirm";

function enrolledResponse(): Response {
  return Response.json({ secret: SECRET, qrUri: QR_URI });
}

/**
 * A 500 whose body names no machine code, so the error interceptor shapes it into
 * `code: "UNKNOWN"` and it lands on the generic "could not start" copy rather than the
 * session or expired-link branches.
 */
function serverRejectionResponse(): Response {
  return Response.json({ succeeded: false }, { status: 500 });
}

let harness: SdkHarness;

/** One-shot enroll responders, consumed in order; leftovers fall to {@link enrollDefault}. */
let enrollQueue: SdkResponder[] = [];

/** The standing enroll behaviour once the queue is drained. */
let enrollDefault: SdkResponder;

function enrollCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path === ENROLL_PATH);
}

/**
 * An enroll response that hangs until the returned `release` is called, so the in-flight
 * window can be asserted rather than raced. Every test must release before it ends: a promise
 * left pending outlives the test and resolves onto an unmounted tree.
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

function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

function renderForm(props: { returnUrl?: string; enrollToken?: string } = {}) {
  return renderWithClient(<MfaEnrollForm {...props} />);
}

beforeEach(() => {
  vi.clearAllMocks();
  harness = createPassthroughHarness();
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
    // `mfa-enroll-begin-setup` is a RETRY in all but name: the intro branch is reachable
    // only once enrollment has failed.
    const release: () => void = hangingEnroll();
    renderForm();

    // Wait for the request to REACH the transport before asserting or releasing: the screen
    // enters its pending state a tick before `fetch` is called, and releasing into that gap
    // would leave the never-settling response installed.
    await vi.waitFor(() => {
      expect(enrollCalls()).toHaveLength(1);
    });
    expect(page.getByTestId("mfa-enroll-begin-setup").query()).toBeNull();

    release();
    await expect.element(page.getByTestId("mfa-enroll-secret")).toBeInTheDocument();
  });

  it("shows neither the secret nor an error until the call settles", async () => {
    // The in-flight state is its own thing: not the success branch, and not a failure.
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
    // The enrollToken path makes TWO round trips before anything renders, and the retry must
    // stay withheld across BOTH — a gap between them is a live begin-setup button in the
    // middle of a flow the user did not finish.
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
    // A stale error above an in-flight retry is a lie about the request the user just made,
    // so the clear has to be EAGER (a `mutate`-time reset, or reading `isPending` ahead of
    // `error`) — deriving the banner from `mutation.error` alone keeps the dead error on
    // screen for the whole retry.
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
    // The pending flag has to come back DOWN on the error path too, or the user is stranded
    // on a dead screen with no way to try again.
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
