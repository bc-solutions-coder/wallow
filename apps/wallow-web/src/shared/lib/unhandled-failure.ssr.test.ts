import { ApiFailure } from "@bc-solutions-coder/api-errors";
import { afterEach, describe, expect, it, vi } from "vitest";

import { log } from "./log";
import { reportUnhandledFailure } from "./unhandled-failure";

/*
 * The callback's server-side branch: a query with `toastFailure` meta can fail
 * inside an SSR loader, where nothing is mounted to show a toast and the browser
 * log transport does not exist. The node project has no `document`, so this is
 * the one place that branch can run. The browser cases are `unhandled-failure.test.tsx`.
 */

describe("reportUnhandledFailure without a document", () => {
  const warn = vi.spyOn(log, "warn");

  afterEach(() => {
    warn.mockReset();
  });

  it("says nothing", () => {
    reportUnhandledFailure({
      kind: "query",
      error: new ApiFailure({ status: 500, code: "Server.Error", title: "Internal Server Error" }),
    });

    expect(warn).not.toHaveBeenCalled();
  });
});
