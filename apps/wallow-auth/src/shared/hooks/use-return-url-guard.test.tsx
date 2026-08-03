import type { ReactElement } from "react";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useReturnUrlGuard } from "./use-return-url-guard";

/**
 * The shared open-redirect guard, extracted from the copies MfaEnrollForm and
 * ConsentScreen each carried.
 *
 * Security-relevant, so the boundary cases are the point: `undefined` is the
 * ordinary direct path and must NOT bail, while `""` is a present-and-unsafe
 * value that must.
 */

// Only the ROUTER is stubbed — `useNavigate` is how the hook reports a refusal,
// and the hook touches nothing else.
const mocks = vi.hoisted(() => ({
  navigate: vi.fn(),
}));

vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/** Renders the verdict so a test can read it without reaching into the hook. */
function Probe({ returnUrl }: { readonly returnUrl: string | undefined }): ReactElement {
  const verdict = useReturnUrlGuard(returnUrl);
  return <div data-testid="verdict">{verdict}</div>;
}

async function verdictFor(returnUrl: string | undefined): Promise<string> {
  const { container } = await render(<Probe returnUrl={returnUrl} />);
  return container.querySelector('[data-testid="verdict"]')?.textContent ?? "";
}

describe("useReturnUrlGuard", () => {
  beforeEach(() => {
    mocks.navigate.mockClear();
  });

  it("accepts an absent returnUrl without navigating", async () => {
    // The ordinary direct path — not an attack, and a bail here would break
    // every screen reached without an OIDC hand-off.
    expect(await verdictFor(undefined)).toBe("accept");
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("accepts a same-origin relative returnUrl", async () => {
    expect(await verdictFor("/connect/authorize?client_id=wallow-web")).toBe("accept");
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("refuses an absolute returnUrl and routes to the error page", async () => {
    // REFUSE, don't sanitize: no silent "/" fallback, which would swallow the
    // attempt and leave the screen looking as though nothing was wrong.
    expect(await verdictFor("https://evil.example/steal")).toBe("refuse");
    expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
  });

  it("refuses a protocol-relative returnUrl", async () => {
    // "//evil.example" is a leading slash away from looking relative, and is the
    // shape a naive `startsWith("/")` check lets through.
    expect(await verdictFor("//evil.example/steal")).toBe("refuse");
    expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
  });

  it("refuses an empty returnUrl", async () => {
    // "" is PRESENT, so it is the unsafe case and not the nullish one. The two
    // pure helpers read it the other way on purpose; see the hook's comment.
    expect(await verdictFor("")).toBe("refuse");
    expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
  });

  it("navigates once for a single refusal", async () => {
    // The effect keys on the VERDICT, not the url, so a re-render for any other
    // reason must not re-fire the bail.
    await verdictFor("https://evil.example/steal");

    expect(mocks.navigate).toHaveBeenCalledTimes(1);
  });
});
