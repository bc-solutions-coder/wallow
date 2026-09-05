import { ApiFailure, ErrorCode } from "@bc-solutions-coder/api-errors";
import { isNotFound } from "@tanstack/react-router";
import { describe, expect, it } from "vitest";

import { notFoundOn404 } from "./not-found-on-404";

const NOT_FOUND = new ApiFailure({
  status: 404,
  code: ErrorCode.HTTP_NOT_FOUND,
  title: "Not Found",
});

describe("notFoundOn404", () => {
  it("passes a resolved read through", async () => {
    await expect(notFoundOn404(Promise.resolve({ id: "org-1" }))).resolves.toEqual({ id: "org-1" });
  });

  it("rethrows an API 404 as the router's not-found signal", async () => {
    await expect(notFoundOn404(Promise.reject(NOT_FOUND))).rejects.toSatisfy(isNotFound);
  });

  it("leaves every other failure untouched", async () => {
    const outage = new ApiFailure({
      status: 503,
      code: "Transport.NetworkError",
      title: "Unavailable",
    });
    await expect(notFoundOn404(Promise.reject(outage))).rejects.toBe(outage);
    const bug = new Error("render");
    await expect(notFoundOn404(Promise.reject(bug))).rejects.toBe(bug);
  });
});
