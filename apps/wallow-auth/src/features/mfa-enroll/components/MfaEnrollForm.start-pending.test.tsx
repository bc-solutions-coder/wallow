import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

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
 * MOCKING SEAM: `../../../lib/wallow-auth-sdk`, the app's own facade — screens
 * never import `@bc-solutions-coder/sdk` directly. Plain `vi.mock` factory plus
 * `vi.hoisted` spies, never `vi.resetModules()` (bd memory
 * `vitest-resetmodules-breaks-instanceof-across-graphs`).
 */

const mocks = vi.hoisted(() => ({
  enrollTotp: vi.fn(),
  confirmEnrollment: vi.fn(),
  exchangeEnrollmentToken: vi.fn(),
  isSafeReturnUrl: vi.fn(),
  navigate: vi.fn(),
}));

vi.mock("../../../lib/wallow-auth-sdk", () => ({
  getWallowAuthSdk: () => ({
    auth: {
      enrollTotp: mocks.enrollTotp,
      confirmEnrollment: mocks.confirmEnrollment,
      exchangeEnrollmentToken: mocks.exchangeEnrollmentToken,
    },
    oidc: { isSafeReturnUrl: mocks.isSafeReturnUrl },
  }),
}));

vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const SECRET = "JBSWY3DPEHPK3PXP";
const QR_URI = "otpauth://totp/Wallow:user@test.local?secret=JBSWY3DPEHPK3PXP&issuer=Wallow";

/**
 * What the facade really throws — the reason token rides as `code`, the HTTP
 * status as `status`. 500 is the unrecognised case, which lands on the generic
 * "could not start" copy rather than the session or expired-link branches.
 */
function serverRejection(): Error & { status: number; code: string } {
  return Object.assign(new Error("Unknown error"), {
    name: "WallowError",
    status: 500,
    code: "UNKNOWN",
  });
}

/**
 * An `enrollTotp` that hangs until the returned `release` is called, so the
 * in-flight window can be asserted rather than raced. Every test releases before
 * it ends: a promise left pending outlives the test and its resolution would
 * land on an unmounted tree.
 */
function hangingEnroll(): () => void {
  let release!: () => void;
  mocks.enrollTotp.mockReturnValueOnce(
    new Promise((resolve) => {
      release = () => {
        resolve({ secret: SECRET, qrUri: QR_URI });
      };
    }),
  );

  return () => {
    release();
  };
}

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(ui: ReactElement) {
  return render(<QueryClientProvider client={newClient()}>{ui}</QueryClientProvider>);
}

function renderForm(props: { returnUrl?: string; enrollToken?: string } = {}) {
  return renderWithClient(<MfaEnrollForm {...props} />);
}

beforeEach(() => {
  vi.clearAllMocks();
  mocks.isSafeReturnUrl.mockReturnValue(true);
  mocks.enrollTotp.mockResolvedValue({ secret: SECRET, qrUri: QR_URI });
  mocks.exchangeEnrollmentToken.mockResolvedValue({ succeeded: true });
  mocks.confirmEnrollment.mockResolvedValue({ succeeded: true, backupCodes: [] });
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

    await vi.waitFor(() => {
      expect(mocks.enrollTotp).toHaveBeenCalledTimes(1);
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
      expect(mocks.enrollTotp).toHaveBeenCalledTimes(1);
    });
    expect(page.getByTestId("mfa-enroll-secret").query()).toBeNull();
    expect(page.getByTestId("mfa-enroll-error").query()).toBeNull();

    release();
    await expect.element(page.getByTestId("mfa-enroll-secret")).toBeInTheDocument();
  });

  it("stays pending across the token exchange and the start call", async () => {
    // The enrollToken path makes TWO round trips before anything renders. The
    // retry must stay withheld across BOTH — a gap between them is a live
    // begin-setup button in the middle of a flow the user did not finish.
    const release: () => void = hangingEnroll();
    renderForm({ enrollToken: "enroll-token-abc123" });

    await vi.waitFor(() => {
      expect(mocks.enrollTotp).toHaveBeenCalledTimes(1);
    });
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
    mocks.enrollTotp.mockRejectedValueOnce(serverRejection());
    const user = userEvent.setup();
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-error")).toBeInTheDocument();

    const release: () => void = hangingEnroll();
    await user.click(page.getByTestId("mfa-enroll-begin-setup"));

    await vi.waitFor(() => {
      expect(mocks.enrollTotp).toHaveBeenCalledTimes(2);
    });
    expect(page.getByTestId("mfa-enroll-error").query()).toBeNull();

    release();
    await expect.element(page.getByTestId("mfa-enroll-secret")).toBeInTheDocument();
  });

  it("withdraws the retry button so the retry cannot be doubled", async () => {
    // Same second-secret hazard as the mount call, reached by the other route.
    mocks.enrollTotp.mockRejectedValueOnce(serverRejection());
    const user = userEvent.setup();
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-begin-setup")).toBeInTheDocument();

    const release: () => void = hangingEnroll();
    await user.click(page.getByTestId("mfa-enroll-begin-setup"));

    await vi.waitFor(() => {
      expect(mocks.enrollTotp).toHaveBeenCalledTimes(2);
    });
    expect(page.getByTestId("mfa-enroll-begin-setup").query()).toBeNull();

    release();
    await expect.element(page.getByTestId("mfa-enroll-secret")).toBeInTheDocument();
  });

  it("brings the retry back when the retry itself fails", async () => {
    // The pending flag has to come back DOWN on the error path too. A mutation
    // left reading as pending after a rejection strands the user on a dead
    // screen with no way to try again.
    mocks.enrollTotp.mockRejectedValue(serverRejection());
    const user = userEvent.setup();
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-begin-setup")).toBeInTheDocument();
    await user.click(page.getByTestId("mfa-enroll-begin-setup"));

    await vi.waitFor(() => {
      expect(mocks.enrollTotp).toHaveBeenCalledTimes(2);
    });
    await expect.element(page.getByTestId("mfa-enroll-begin-setup")).toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-enroll-error")).toBeInTheDocument();
  });
});
