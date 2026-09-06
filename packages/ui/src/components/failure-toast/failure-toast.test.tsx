import { expectNavigationEscape } from "@bc-solutions-coder/testing/navigation-escape";
import { render } from "@bc-solutions-coder/testing/render";
import { act } from "react";
import { toast } from "sonner";
import { afterEach, describe, expect, it } from "vitest";
import { userEvent } from "vitest/browser";

import { ThemeProvider } from "../theme-provider/theme-provider";
import { FailureToaster, toastFailure } from "./failure-toast";

/*
 * The sonner failure toast: what `toastFailure` puts on screen, and that the
 * copy action copies the reference without dismissing the toast. sonner keeps
 * its toasts in a module singleton that replays to the next mounted toaster,
 * so every case clears it.
 */

afterEach(() => {
  toast.dismiss();
});

function toasts(): HTMLElement[] {
  return [...document.querySelectorAll<HTMLElement>("[data-sonner-toast]")];
}

function raise(message: string, options?: Parameters<typeof toastFailure>[1]): void {
  act(() => {
    toastFailure(message, options);
  });
}

describe("FailureToaster", () => {
  it("mounts bottom-right with a close button once a toast is raised", async () => {
    await render(<FailureToaster />);
    raise("Something went wrong on our side.");

    await expect.poll(() => toasts().length).toBe(1);
    const toaster = document.querySelector<HTMLElement>("[data-sonner-toaster]");
    expect(toaster?.dataset["xPosition"]).toBe("right");
    expect(toaster?.dataset["yPosition"]).toBe("bottom");
    expect(toasts()[0]?.querySelector("[data-close-button]")).not.toBeNull();
  });

  it("dismisses a toast from its close button", async () => {
    await render(<FailureToaster />);
    raise("Something went wrong on our side.");

    await expect.poll(() => toasts().length).toBe(1);
    const close = toasts()[0]?.querySelector<HTMLButtonElement>("[data-close-button]");
    // sonner's close button has no text and the browser project loads no
    // Tailwind for sonner's own stylesheet either, so the DOM's own click.
    close?.click();

    await expect.poll(() => toasts().length).toBe(0);
  });

  it("takes its theme from the theme provider", async () => {
    await render(
      <ThemeProvider defaultMode="dark">
        <FailureToaster />
      </ThemeProvider>,
    );
    raise("Something went wrong on our side.");

    await expect.poll(() => toasts().length).toBe(1);
    expect(
      document.querySelector<HTMLElement>("[data-sonner-toaster]")?.dataset["sonnerTheme"],
    ).toBe("dark");
  });
});

describe("toastFailure", () => {
  it("shows the message alone when there is no reference", async () => {
    await render(<FailureToaster />);
    raise("You don't have permission to do that.");

    await expect.poll(() => toasts().length).toBe(1);
    const [only] = toasts();
    expect(only?.textContent).toContain("You don't have permission to do that.");
    expect(only?.textContent).not.toContain("Reference");
    expect(only?.querySelector("button[data-button]")).toBeNull();
  });

  it("navigates to the sign-in destination with a single action", async () => {
    await render(<FailureToaster />);
    raise("Please sign in again.", {
      signInHref: "/auth/login?returnTo=%2Fsettings",
      reference: { requestId: "not-for-copying" },
    });

    await expect.poll(() => toasts().length).toBe(1);
    const [only] = toasts();
    const actions = only?.querySelectorAll<HTMLButtonElement>("button[data-button]");
    expect(actions).toHaveLength(1);
    expect(actions?.[0]?.textContent).toBe("Sign in");
    expect(only?.textContent).not.toContain("Copy reference");
    expect(only?.textContent).not.toContain("Reference");
    await userEvent.click(actions?.[0] as HTMLButtonElement);

    const navigation = await expectNavigationEscape();
    expect(navigation.url).toBe(new URL("/auth/login?returnTo=%2Fsettings", location.origin).href);
  });

  it("keeps sign-in and referenced toasts after an ordinary toast expires", async () => {
    await render(<FailureToaster />);
    raise("Please sign in again.", { signInHref: "/auth/login" });
    raise("The server is unavailable.", { reference: { requestId: "request-persistent" } });
    raise("Temporary notice.");

    await expect.poll(() => toasts().length).toBe(3);
    await userEvent.unhover(
      document.querySelector<HTMLElement>("[data-sonner-toaster]") as HTMLElement,
    );
    await expect
      .poll(() => toasts().some((element) => element.textContent?.includes("Temporary notice.")), {
        timeout: 7000,
      })
      .toBe(false);
    expect(toasts()).toHaveLength(2);
    expect(toasts().some((element) => element.textContent?.includes("Sign in"))).toBe(true);
    expect(
      toasts().some((element) => element.textContent?.includes("Reference request-persistent")),
    ).toBe(true);
  }, 10000);

  it("shows the reference line, preferring the trace id", async () => {
    await render(<FailureToaster />);
    raise("Something went wrong on our side.", { traceId: "trace-1", requestId: "request-1" });
    raise("Unable to reach the server.", { requestId: "request-2" });

    await expect.poll(() => toasts().length).toBe(2);
    const text = toasts()
      .map((element) => element.textContent)
      .join("\n");
    expect(text).toContain("Reference trace-1");
    expect(text).not.toContain("request-1");
    expect(text).toContain("Reference request-2");
  });

  it.each(["legacy", "options"])(
    "copies the %s reference without dismissing the toast",
    async (shape) => {
      await render(<FailureToaster />);
      const reference = { traceId: "4bf92f3577b34da6a3ce929d0e0e4736" };
      raise("Something went wrong on our side.", shape === "legacy" ? reference : { reference });

      await expect.poll(() => toasts().length).toBe(1);
      const copy = toasts()[0]?.querySelector<HTMLButtonElement>("button[data-button]");
      expect(copy?.textContent).toBe("Copy reference");
      await userEvent.click(copy as HTMLButtonElement);

      await expect
        .poll(() => navigator.clipboard.readText())
        .toBe("4bf92f3577b34da6a3ce929d0e0e4736");
      // Dismissal is animation-deferred, so a bare synchronous check would pass
      // either way; wait past the removal delay before asserting it stayed.
      await new Promise((resolve) => {
        setTimeout(resolve, 400);
      });
      expect(toasts()).toHaveLength(1);
    },
  );
});
