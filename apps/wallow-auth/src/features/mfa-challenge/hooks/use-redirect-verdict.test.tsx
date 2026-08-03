import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import {
  createPassthroughHarness,
  type SdkCall,
  type SdkHarness,
  type SdkResponder,
} from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useRedirectVerdict } from "./use-redirect-verdict";

/**
 * The challenge screen's guard, as a unit.
 *
 * `MfaChallengeForm.test.tsx` already drives every one of these paths through the
 * rendered screen; what this pins that the screen cannot is the guard's OWN
 * contract — that a locally-decided returnUrl costs no probe at all, and that
 * `handOff` returns a bare string for one and the allow-list proof for the other.
 * Through the screen both shapes disappear into the same exchange URL.
 *
 * Real SDK over a faked fetch, so "no probe fired" is read off the recorded
 * calls rather than off a spy.
 */

// Only the router's `useNavigate` is mocked; it is the seam for the bail to /error.
const mocks = vi.hoisted(() => ({
  navigate: vi.fn(),
}));

vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const VALIDATE_PATH = "/v1/identity/auth/redirect-uri/validate";
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/** The client that started the flow. */
const CLIENT_ID = "client-a";

/** The relative returnUrl the password path threads — settled without a probe. */
const RELATIVE_RETURN_URL = "/connect/authorize?client_id=web";

/** The external-login hand-off's returnUrl: ABSOLUTE, and genuinely allow-listed. */
const EXTERNAL_RETURN_URL = "https://app.example.com/callback";

/** Absolute, from an origin the allow-list has never heard of. */
const EVIL_RETURN_URL = "https://evil.example.com/steal";

let harness: SdkHarness;
let validateWith: SdkResponder;

/** The server's rule, mirrored: a fake answering a constant would pass for the wrong reason. */
const allowListResponder: SdkResponder = (call: SdkCall): Response => {
  const uri: string = new URL(call.url).searchParams.get("uri") ?? "";
  return Response.json({ allowed: uri.startsWith("https://app.example.com") });
};

/** A probe that never answers, which is how the "pending" verdict is held open. */
const neverResponder: SdkResponder = () => new Promise<Response>(() => {});

function validateCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path === VALIDATE_PATH);
}

/**
 * Renders the verdict and what `handOff` produces, JSON-encoded so the branded
 * object and the bare string are distinguishable in one read.
 */
function Probe({
  returnUrl,
  clientId,
}: {
  readonly returnUrl: string | undefined;
  readonly clientId: string | undefined;
}): ReactElement {
  const guard = useRedirectVerdict(returnUrl, clientId);
  const handOff =
    guard.verdict === "accept" && returnUrl !== undefined && returnUrl !== ""
      ? JSON.stringify(guard.handOff(returnUrl))
      : "";

  return (
    <div>
      <div data-testid="verdict">{guard.verdict}</div>
      <div data-testid="hand-off">{handOff}</div>
    </div>
  );
}

async function renderProbe(returnUrl: string | undefined, clientId?: string) {
  return renderWithWallow(<Probe returnUrl={returnUrl} clientId={clientId} />, { harness });
}

/** The verdict, once it has stopped being "pending". */
async function settledVerdict(returnUrl: string | undefined, clientId?: string): Promise<string> {
  await renderProbe(returnUrl, clientId);
  await expect.element(page.getByTestId("verdict")).not.toHaveTextContent("pending");
  return page.getByTestId("verdict").element().textContent ?? "";
}

beforeEach(() => {
  vi.clearAllMocks();
  harness = createPassthroughHarness();
  validateWith = allowListResponder;
  harness.respond((call: SdkCall) =>
    call.path === VALIDATE_PATH ? validateWith(call) : Response.json({}),
  );
});

describe("useRedirectVerdict", () => {
  describe("the returnUrls it settles without asking the server", () => {
    it("accepts an absent returnUrl and fires no probe", async () => {
      // The ordinary direct (non-OIDC) sign-in. A probe here would sit between
      // the user and their code field for a destination that does not exist.
      expect(await settledVerdict(undefined)).toBe("accept");
      expect(validateCalls()).toHaveLength(0);
      expect(mocks.navigate).not.toHaveBeenCalled();
    });

    it("accepts a relative returnUrl and fires no probe", async () => {
      // The password path — the common case, decided for free.
      expect(await settledVerdict(RELATIVE_RETURN_URL)).toBe("accept");
      expect(validateCalls()).toHaveLength(0);
    });

    it("refuses an empty returnUrl without asking the server", async () => {
      // `?returnUrl=` is a malformed link, not a destination worth a request.
      expect(await settledVerdict("")).toBe("refuse");
      expect(validateCalls()).toHaveLength(0);
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
  });

  describe("the absolute returnUrl only the allow-list can judge", () => {
    it("accepts one the server allows, asking scoped to the flow's client", async () => {
      expect(await settledVerdict(EXTERNAL_RETURN_URL, CLIENT_ID)).toBe("accept");

      // Unscoped, the endpoint answers against the UNION of every registered
      // client's origins — a URI any client at all registered would pass here.
      expect(new URL(validateCalls()[0]!.url).searchParams.get("clientId")).toBe(CLIENT_ID);
      expect(mocks.navigate).not.toHaveBeenCalled();
    });

    it("refuses one the server does not allow", async () => {
      expect(await settledVerdict(EVIL_RETURN_URL, CLIENT_ID)).toBe("refuse");
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });

    it("stays pending while the probe is in flight", async () => {
      // "pending" is a verdict of its own rather than collapsing into "accept":
      // the caller renders nothing until the answer lands, so an undecided
      // destination never gets a code typed into it.
      validateWith = neverResponder;
      await renderProbe(EXTERNAL_RETURN_URL, CLIENT_ID);

      await expect.element(page.getByTestId("verdict")).toHaveTextContent("pending");
      expect(mocks.navigate).not.toHaveBeenCalled();
    });
  });

  describe("the hand-off value", () => {
    it("hands a locally-decided returnUrl straight through as a string", async () => {
      // Relative, and `isSafeReturnUrl` is the whole proof. The mint's
      // relative-only rule applies, and nothing claims allow-listing.
      await settledVerdict(RELATIVE_RETURN_URL);

      expect(page.getByTestId("hand-off").element().textContent).toBe(
        JSON.stringify(RELATIVE_RETURN_URL),
      );
    });

    it("carries the allow-list verdict WITH an absolute returnUrl", async () => {
      // The proof travels with the value rather than being re-derived at the
      // hand-off, which is what stops the exchange mint from having to trust a
      // bare absolute URL it cannot check.
      await settledVerdict(EXTERNAL_RETURN_URL, CLIENT_ID);

      expect(page.getByTestId("hand-off").element().textContent).toBe(
        JSON.stringify({ url: EXTERNAL_RETURN_URL, allowListed: true }),
      );
    });
  });
});
