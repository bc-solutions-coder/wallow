import { afterEach, describe, expect, it, vi } from "vitest";

import { createLogger, type Logger, type LogBatch } from "./index";

/**
 * The two page-lifecycle flushes, in a real browser.
 *
 * These are the whole reason a browser project exists here: `pagehide` and
 * `visibilitychange` are the paths where "logs vanish when the tab closes"
 * hides, and `navigator.sendBeacon` is the only transport a browser promises to
 * finish after the document is gone.
 */

const ENDPOINT = "/bff/logs";

function makeLogger(overrides: Partial<Parameters<typeof createLogger>[0]> = {}): Logger {
  return createLogger({
    service: "wallow-web",
    endpoint: ENDPOINT,
    flushAtEvents: 1000,
    flushIntervalMs: 0,
    ...overrides,
  });
}

async function beaconBody(blob: Blob): Promise<LogBatch> {
  return JSON.parse(await blob.text()) as LogBatch;
}

function setVisibility(state: DocumentVisibilityState): void {
  Object.defineProperty(document, "visibilityState", { value: state, configurable: true });
}

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  setVisibility("visible");
});

describe("pagehide", () => {
  it("sends the buffer through sendBeacon", async () => {
    const beacon = vi.spyOn(navigator, "sendBeacon").mockReturnValue(true);
    const fetchImpl = vi.fn<typeof fetch>();
    vi.stubGlobal("fetch", fetchImpl);
    const logger = makeLogger();

    logger.info("form.submitted");
    globalThis.dispatchEvent(new PageTransitionEvent("pagehide"));

    expect(beacon).toHaveBeenCalledTimes(1);
    // fetch cannot carry this flush: a discarded page may never run the
    // continuation that reads the response.
    expect(fetchImpl).not.toHaveBeenCalled();

    const [url, body] = beacon.mock.calls[0]!;

    expect(url).toBe(ENDPOINT);
    const batch = await beaconBody(body as Blob);

    expect(batch.events.map((event) => event.event)).toEqual(["form.submitted"]);

    logger.dispose();
  });

  it("puts the csrf token in the BODY, where sendBeacon can carry it", async () => {
    const beacon = vi.spyOn(navigator, "sendBeacon").mockReturnValue(true);
    const logger = makeLogger({ getCsrfToken: () => "t0ken" });

    logger.info("form.submitted");
    globalThis.dispatchEvent(new PageTransitionEvent("pagehide"));

    const batch = await beaconBody(beacon.mock.calls[0]![1] as Blob);

    expect(batch.csrfToken).toBe("t0ken");

    logger.dispose();
  });

  it("reports a beacon the browser refused to queue", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    vi.spyOn(navigator, "sendBeacon").mockReturnValue(false);
    const logger = makeLogger();

    logger.info("form.submitted");
    globalThis.dispatchEvent(new PageTransitionEvent("pagehide"));

    expect(warn).toHaveBeenCalled();

    logger.dispose();
  });

  it("queues nothing when there is nothing buffered", () => {
    const beacon = vi.spyOn(navigator, "sendBeacon").mockReturnValue(true);
    const logger = makeLogger();

    globalThis.dispatchEvent(new PageTransitionEvent("pagehide"));

    expect(beacon).not.toHaveBeenCalled();

    logger.dispose();
  });
});

describe("visibilitychange", () => {
  it("flushes when the page goes hidden", async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchImpl);
    const logger = makeLogger();

    logger.info("form.submitted");
    setVisibility("hidden");
    document.dispatchEvent(new Event("visibilitychange"));

    await vi.waitFor(() => {
      expect(fetchImpl).toHaveBeenCalledTimes(1);
    });

    logger.dispose();
  });

  it("stays quiet when the page becomes visible again", () => {
    const fetchImpl = vi.fn<typeof fetch>();
    vi.stubGlobal("fetch", fetchImpl);
    const logger = makeLogger();

    logger.info("form.submitted");
    setVisibility("visible");
    document.dispatchEvent(new Event("visibilitychange"));

    expect(fetchImpl).not.toHaveBeenCalled();

    logger.dispose();
  });
});

describe("dispose", () => {
  it("unregisters both lifecycle listeners", () => {
    const beacon = vi.spyOn(navigator, "sendBeacon").mockReturnValue(true);
    const logger = makeLogger();

    logger.info("form.submitted");
    logger.dispose();
    globalThis.dispatchEvent(new PageTransitionEvent("pagehide"));

    expect(beacon).not.toHaveBeenCalled();
  });
});
