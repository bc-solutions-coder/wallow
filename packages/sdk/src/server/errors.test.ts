import { ApiFailure, ClientErrorCode, isApiFailure } from "@bc-solutions-coder/api-errors";

import { describe, expect, it } from "vitest";

import { redact, REDACTED, RefreshFailedError } from "./errors";

describe("RefreshFailedError", () => {
  it("is the package's ApiFailure, answered as a 401 Bff.SessionRefreshFailed", () => {
    const error: RefreshFailedError = new RefreshFailedError("The grant was revoked.");

    expect(error).toBeInstanceOf(Error);
    expect(error).toBeInstanceOf(ApiFailure);
    expect(isApiFailure(error)).toBe(true);
    expect(error.name).toBe("RefreshFailedError");
    expect(error.status).toBe(401);
    expect(error.code).toBe(ClientErrorCode.BFF_SESSION_REFRESH_FAILED);
    expect(error.code).toBe("Bff.SessionRefreshFailed");
    expect(error.detail).toBe("The grant was revoked.");
  });

  it("carries no detail when the refresh named none", () => {
    expect(new RefreshFailedError().detail).toBeUndefined();
  });
});

describe("redact", () => {
  it("strips authorization and cookie headers", () => {
    const redacted: Record<string, unknown> = redact({
      authorization: "Bearer abc.def.ghi",
      Cookie: "wallow_session=super-secret-value",
      "set-cookie": "wallow_session=super-secret-value; HttpOnly",
      "content-type": "application/json",
    }) as Record<string, unknown>;

    expect(redacted["authorization"]).toBe(REDACTED);
    expect(redacted["Cookie"]).toBe(REDACTED);
    expect(redacted["set-cookie"]).toBe(REDACTED);
    expect(redacted["content-type"]).toBe("application/json");
  });

  it("strips token-shaped and credential-named members", () => {
    const redacted: Record<string, unknown> = redact({
      accessToken: "a.b.c",
      refresh_token: "rt-123",
      idToken: "x.y.z",
      clientSecret: "shhh",
      password: "hunter2",
      email: "user@example.com",
    }) as Record<string, unknown>;

    expect(redacted["accessToken"]).toBe(REDACTED);
    expect(redacted["refresh_token"]).toBe(REDACTED);
    expect(redacted["idToken"]).toBe(REDACTED);
    expect(redacted["clientSecret"]).toBe(REDACTED);
    expect(redacted["password"]).toBe(REDACTED);
    expect(redacted["email"]).toBe("user@example.com");
  });

  it("redacts nested objects and arrays without mutating the input", () => {
    const input: Record<string, unknown> = {
      request: {
        url: "https://api.example.com/inquiries",
        headers: { authorization: "Bearer abc.def.ghi" },
      },
      events: [{ cookie: "sid=1" }, { status: 500 }],
    };

    const redacted: Record<string, unknown> = redact(input) as Record<string, unknown>;
    const request: Record<string, unknown> = redacted["request"] as Record<string, unknown>;
    const headers: Record<string, unknown> = request["headers"] as Record<string, unknown>;
    const events: Record<string, unknown>[] = redacted["events"] as Record<string, unknown>[];

    expect(headers["authorization"]).toBe(REDACTED);
    expect(request["url"]).toBe("https://api.example.com/inquiries");
    expect(events[0]?.["cookie"]).toBe(REDACTED);
    expect(events[1]?.["status"]).toBe(500);
    expect(
      ((input["request"] as Record<string, unknown>)["headers"] as Record<string, unknown>)[
        "authorization"
      ],
    ).toBe("Bearer abc.def.ghi");
  });

  it("redacts token-shaped string values regardless of member name", () => {
    const redacted: Record<string, unknown> = redact({
      note: "Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.signature",
      jwt: "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.signature",
      title: "Not Found",
    }) as Record<string, unknown>;

    expect(redacted["note"]).toBe(REDACTED);
    expect(redacted["jwt"]).toBe(REDACTED);
    expect(redacted["title"]).toBe("Not Found");
  });

  it("passes primitives through untouched", () => {
    expect(redact(42)).toBe(42);
    expect(redact("plain text")).toBe("plain text");
    expect(redact(null)).toBeNull();
    expect(redact(undefined)).toBeUndefined();
  });
});
