import { ApiFailure, ClientErrorCode, ErrorCode } from "@bc-solutions-coder/api-errors";
import { expectNavigationEscape } from "@bc-solutions-coder/testing/navigation-escape";
import { userEvent } from "vitest/browser";
import { render } from "@bc-solutions-coder/testing/render";
import { FailureToaster } from "@bc-solutions-coder/ui/failure-toast";
import { act } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { log } from "./log";
import { reportUnhandledFailure, UNHANDLED_FAILURE_EVENT } from "./unhandled-failure";

/*
 * The query client's unhandled-failure callback against the real toaster: which
 * sentence lands on screen, when the reference line appears, and what the one
 * log record carries. sonner replays its singleton to the next mounted toaster,
 * so every case closes what it raised (the app does not depend on sonner, so
 * the close button is the way). The no-document branch is
 * `unhandled-failure.ssr.test.ts`.
 */

const SERVER_FAILURE = new ApiFailure({
  status: 500,
  code: "Server.Error",
  title: "Internal Server Error",
  traceId: "trace-1",
  requestId: "request-1",
});

function toasts(): HTMLElement[] {
  return [...document.querySelectorAll<HTMLElement>("[data-sonner-toast]")];
}

function report(kind: "mutation" | "query", error: unknown): void {
  act(() => {
    reportUnhandledFailure({ kind, error });
  });
}

describe("reportUnhandledFailure", () => {
  const warn = vi.spyOn(log, "warn");

  beforeEach(async () => {
    warn.mockImplementation(() => {
      // The ingest route is not mounted here; the record is the assertion.
    });
    await render(<FailureToaster />);
  });

  afterEach(async () => {
    for (const open of toasts()) {
      open.querySelector<HTMLButtonElement>("[data-close-button]")?.click();
    }
    await expect.poll(() => toasts().length).toBe(0);
    warn.mockReset();
  });

  it("toasts the registry's sentence for an MFA code, without a reference line", async () => {
    report(
      "mutation",
      new ApiFailure({ status: 400, code: "Mfa.CodeInvalid", title: "Bad Request" }),
    );

    await expect.poll(() => toasts().length).toBe(1);
    expect(toasts()[0]?.textContent).toContain("That verification code is not valid.");
    expect(toasts()[0]?.querySelector("button[data-button]")).toBeNull();
    expect(toasts()[0]?.textContent).not.toContain("Reference");
  });

  it.each([
    ErrorCode.AUTH_UNAUTHENTICATED,
    ClientErrorCode.BFF_SESSION_MISSING,
    ClientErrorCode.BFF_SESSION_REFRESH_FAILED,
  ])("offers sign-in with the current return path for %s", async (code) => {
    const currentPath = `${location.pathname}${location.search}`;
    report(
      "mutation",
      new ApiFailure({ status: 401, code, title: "Unauthorized", requestId: "not-quotable" }),
    );

    await expect.poll(() => toasts().length).toBe(1);
    const action = toasts()[0]?.querySelector<HTMLButtonElement>("button[data-button]");
    expect(action?.textContent).toBe("Sign in");
    expect(toasts()[0]?.textContent).not.toContain("Reference");
    await userEvent.click(action as HTMLButtonElement);
    const navigation = await expectNavigationEscape();
    const destination = new URL(navigation.url);
    expect(destination.origin).toBe(location.origin);
    expect(destination.pathname).toBe("/bff/login");
    expect(destination.searchParams.get("returnTo")).toBe(currentPath);
  });

  it("shows the quotable reference and logs the trace id ahead of the request id", async () => {
    report("query", SERVER_FAILURE);

    await expect.poll(() => toasts().length).toBe(1);
    expect(toasts()[0]?.textContent).toContain(
      "Something went wrong on our side. Please try again later.",
    );
    expect(toasts()[0]?.textContent).toContain("Reference trace-1");
    expect(toasts()[0]?.querySelector("button[data-button]")?.textContent).toBe("Copy reference");
    expect(warn).toHaveBeenCalledTimes(1);
    expect(warn).toHaveBeenCalledWith(
      UNHANDLED_FAILURE_EVENT,
      { kind: "query", code: "Server.Error", status: 500, reference: "trace-1" },
      SERVER_FAILURE,
    );
  });

  it("classifies a bare rejection before toasting it", async () => {
    report("mutation", new TypeError("fetch failed"));

    await expect.poll(() => toasts().length).toBe(1);
    expect(toasts()[0]?.textContent).toContain(
      "Unable to reach the server. Check your connection and try again.",
    );
    expect(warn).toHaveBeenCalledWith(
      UNHANDLED_FAILURE_EVENT,
      expect.objectContaining({ code: ClientErrorCode.TRANSPORT_NETWORK_ERROR }),
      expect.any(ApiFailure),
    );
  });

  it("says nothing for an aborted request", async () => {
    report(
      "query",
      new ApiFailure({ status: 0, code: ClientErrorCode.TRANSPORT_ABORTED, title: "Aborted" }),
    );

    // Give sonner a frame: a toast that never appears is only proven by waiting.
    await new Promise<void>((resolve) => {
      requestAnimationFrame(() => resolve());
    });
    expect(toasts()).toHaveLength(0);
    expect(warn).not.toHaveBeenCalled();
  });
});
