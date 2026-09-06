import {
  ApiFailure,
  ClientErrorCode,
  ErrorCode,
  resolveFailureMessage,
} from "@bc-solutions-coder/api-errors";
import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { FailureBanner } from "./failure-banner";

/*
 * FailureBanner resolves its own sentence and adds exactly the affordances the
 * failure's status rule allows: "Try again" when a retry is offered, "Sign in"
 * on a 401 code, and the copyable reference only for transport and 5xx
 * failures — never the API's detail for those.
 */

const CONFLICT = new ApiFailure({
  status: 409,
  code: "Orders.Closed",
  title: "Closed",
  detail: "The order is already closed.",
  traceId: "trace-409",
});

const SERVER = new ApiFailure({
  status: 500,
  code: ErrorCode.SERVER_ERROR,
  title: "Internal",
  detail: "NullReferenceException at Wallow.Orders",
  traceId: "trace-500",
  requestId: "request-500",
});

function banner(): HTMLElement {
  const element = document.querySelector<HTMLElement>('[data-testid="banner"]');
  expect(element).not.toBeNull();
  return element as HTMLElement;
}

function button(name: string): HTMLButtonElement | null {
  return (
    [...banner().querySelectorAll<HTMLButtonElement>("button")].find(
      (candidate) => candidate.textContent === name,
    ) ?? null
  );
}

function link(name: string): HTMLAnchorElement | null {
  return (
    [...banner().querySelectorAll<HTMLAnchorElement>("a")].find(
      (candidate) => candidate.textContent === name,
    ) ?? null
  );
}

describe("FailureBanner", () => {
  it("renders the ErrorBanner primitive with the resolved sentence and nothing else for a 4xx", async () => {
    await render(<FailureBanner error={CONFLICT} data-testid="banner" />);

    expect(banner().textContent).toContain("The order is already closed.");
    expect(banner().textContent).not.toContain("Reference");
    expect(button("Try again")).toBeNull();
    expect(link("Sign in")).toBeNull();
  });

  it("offers Try again only when a retry is given", async () => {
    const onRetry = vi.fn();
    await render(<FailureBanner error={CONFLICT} onRetry={onRetry} data-testid="banner" />);

    await userEvent.click(button("Try again") as HTMLButtonElement);

    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it.each([
    ErrorCode.AUTH_UNAUTHENTICATED,
    ClientErrorCode.BFF_SESSION_MISSING,
    ClientErrorCode.BFF_SESSION_REFRESH_FAILED,
  ])("links to the BFF login with the current path on %s", async (code) => {
    const error = new ApiFailure({ status: 401, code, title: "Unauthorized" });
    await render(<FailureBanner error={error} data-testid="banner" />);

    const returnTo = `${globalThis.location.pathname}${globalThis.location.search}`;
    const signIn = link("Sign in");
    expect(signIn?.getAttribute("href")).toBe(
      `/bff/login?returnTo=${encodeURIComponent(returnTo)}`,
    );
    expect(signIn?.getAttribute("role")).toBe("link");
  });

  it("does not offer Sign in for a 403", async () => {
    const error = new ApiFailure({ status: 403, code: "Auth.Forbidden", title: "Forbidden" });
    await render(<FailureBanner error={error} data-testid="banner" />);

    expect(link("Sign in")).toBeNull();
  });

  it("shows the reference and copies it for a 5xx, never the detail", async () => {
    await render(<FailureBanner error={SERVER} data-testid="banner" />);

    expect(banner().textContent).toContain(resolveFailureMessage(SERVER));
    expect(banner().textContent).not.toContain("NullReferenceException");
    expect(banner().textContent).toContain("Reference trace-500");
    expect(banner().textContent).not.toContain("request-500");

    // The label flips only once the clipboard write resolves, so it stands in
    // for reading the clipboard back — which the toast spec does, and two spec
    // files sharing one Chromium's clipboard would race each other.
    await userEvent.click(button("Copy reference") as HTMLButtonElement);

    await expect.poll(() => button("Copied")).not.toBeNull();
  });

  it("renders nothing for a nullish error", async () => {
    await render(
      <>
        <FailureBanner error={null} data-testid="banner" />
        <FailureBanner error={undefined} data-testid="banner" />
      </>,
    );

    expect(document.querySelector('[data-testid="banner"]')).toBeNull();
  });

  it("swaps the reference line whole when the failure changes", async () => {
    const screen = await render(<FailureBanner error={SERVER} data-testid="banner" />);
    await userEvent.click(button("Copy reference") as HTMLButtonElement);
    await expect.poll(() => button("Copied")).not.toBeNull();

    const next = new ApiFailure({
      status: 502,
      code: ErrorCode.SERVER_ERROR,
      title: "Bad Gateway",
      traceId: "trace-502",
    });
    await screen.rerender(<FailureBanner error={next} data-testid="banner" />);

    expect(banner().textContent).toContain("Reference trace-502");
    expect(button("Copied")).toBeNull();
    expect(button("Copy reference")).not.toBeNull();
  });

  it("takes a caller's sign-in destination over the BFF default", async () => {
    const error = new ApiFailure({
      status: 401,
      code: ErrorCode.AUTH_UNAUTHENTICATED,
      title: "Unauthorized",
    });
    await render(<FailureBanner error={error} signInHref="/auth/login" data-testid="banner" />);

    expect(link("Sign in")?.getAttribute("href")).toBe("/auth/login");
  });

  it("shows the reference for a transport failure", async () => {
    const error = new ApiFailure({
      status: 503,
      code: ClientErrorCode.TRANSPORT_NETWORK_ERROR,
      title: "Unreachable",
      requestId: "request-503",
    });
    await render(<FailureBanner error={error} data-testid="banner" />);

    expect(banner().textContent).toContain("Reference request-503");
    expect(button("Copy reference")).not.toBeNull();
  });

  it("omits the reference line for a 4xx even when the failure carries one", async () => {
    await render(<FailureBanner error={CONFLICT} data-testid="banner" />);

    expect(banner().textContent).not.toContain("trace-409");
    expect(button("Copy reference")).toBeNull();
  });

  it("passes call-site messages and fallback to the resolver", async () => {
    const teapot = new ApiFailure({ status: 418, code: "Kitchen.Teapot", title: "Teapot" });
    await render(
      <>
        <FailureBanner
          error={CONFLICT}
          messages={{ "Orders.Closed": () => "This order is done." }}
          data-testid="banner"
        />
        <FailureBanner error={teapot} fallback="Short and stout." data-testid="fallback" />
      </>,
    );

    expect(banner().textContent).toContain("This order is done.");
    expect(document.querySelector('[data-testid="fallback"]')?.textContent).toContain(
      "Short and stout.",
    );
  });

  it("renders children after the sentence", async () => {
    await render(
      <FailureBanner error={CONFLICT} data-testid="banner">
        <span data-testid="aside">Contact support if this keeps happening.</span>
      </FailureBanner>,
    );

    expect(banner().querySelector('[data-testid="aside"]')).not.toBeNull();
  });
});
