import { afterEach, describe, expect, it, vi } from "vitest";

import { getUser, login, logout, type WallowUser } from "./auth";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("login", () => {
  it("redirects to the BFF login endpoint with an encoded returnTo", () => {
    const location: { href: string } = { href: "" };
    vi.stubGlobal("location", location);

    login("/dashboard");

    expect(location.href).toBe("/bff/login?returnTo=%2Fdashboard");
  });
});

describe("logout", () => {
  it("redirects to the BFF logout endpoint", () => {
    const location: { href: string } = { href: "" };
    vi.stubGlobal("location", location);

    logout();

    expect(location.href).toBe("/bff/logout");
  });
});

describe("getUser", () => {
  it("returns the parsed JSON body on a 200 response", async () => {
    const user: WallowUser = { sub: "user-123", email: "user@example.com" };
    const fetchMock: ReturnType<typeof vi.fn> = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async (): Promise<WallowUser> => user,
    });
    vi.stubGlobal("fetch", fetchMock);

    const result: WallowUser | null = await getUser();

    expect(result).toEqual(user);
    expect(fetchMock).toHaveBeenCalledWith("/bff/user", {
      credentials: "include",
    });
  });

  it("returns null on a 401 response", async () => {
    const fetchMock: ReturnType<typeof vi.fn> = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      json: async (): Promise<unknown> => ({}),
    });
    vi.stubGlobal("fetch", fetchMock);

    const result: WallowUser | null = await getUser();

    expect(result).toBeNull();
  });
});
