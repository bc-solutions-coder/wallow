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

function raise(message: string, reference?: Parameters<typeof toastFailure>[1]): void {
  act(() => {
    toastFailure(message, reference);
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

  it("copies the reference without dismissing the toast", async () => {
    await render(<FailureToaster />);
    raise("Something went wrong on our side.", { traceId: "4bf92f3577b34da6a3ce929d0e0e4736" });

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
  });
});
