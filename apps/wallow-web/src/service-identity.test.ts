import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The seeded OIDC identity this app logs in as. `api/seed.json` is fork config
 * (`.gitattributes` marks it `merge=ours`), and it is the API side of a contract
 * whose other half is this app's BFF routes: the callback path the client is
 * allowed to return to, and the scopes the dashboard's calls require. A mismatch
 * fails at the authorize endpoint, not at build or typecheck time — hence a
 * spec, read from the same file the seeder consumes.
 */

// apps/wallow-web/src -> repo root (src -> wallow-web -> apps -> repo).
const repoRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");

interface SeededClient {
  clientId: string;
  redirectUris?: string[];
  postLogoutRedirectUris?: string[];
  scopes?: string[];
}

interface SeedFile {
  clients: SeededClient[];
}

function seededClient(clientId: string): SeededClient {
  const seed: SeedFile = JSON.parse(
    readFileSync(resolve(repoRoot, "api/seed.json"), "utf8"),
  ) as SeedFile;
  const client: SeededClient | undefined = seed.clients.find((c) => c.clientId === clientId);
  if (client === undefined) {
    throw new Error(`seed.json has no client '${clientId}'`);
  }
  return client;
}

describe("seeded wallow-web-client identity (api/seed.json)", () => {
  it("uses the BFF callback path /bff/callback, not the legacy /signin-oidc", () => {
    const client: SeededClient = seededClient("wallow-web-client");
    const redirects: string[] = client.redirectUris ?? [];
    expect(redirects.some((uri) => uri.endsWith("/bff/callback"))).toBe(true);
    expect(redirects.some((uri) => uri.includes("/signin-oidc"))).toBe(false);
  });

  it("drops the legacy /signout-callback-oidc post-logout path", () => {
    const client: SeededClient = seededClient("wallow-web-client");
    const postLogout: string[] = client.postLogoutRedirectUris ?? [];
    expect(postLogout.some((uri) => uri.includes("/signout-callback-oidc"))).toBe(false);
  });

  it("grants the inquiries.*/notifications.* scopes the React dashboard calls", () => {
    const client: SeededClient = seededClient("wallow-web-client");
    const scopes: string[] = client.scopes ?? [];
    expect(scopes).toEqual(
      expect.arrayContaining([
        "inquiries.read",
        "inquiries.write",
        "notifications.read",
        "notifications.write",
      ]),
    );
  });

  it("retains the base OIDC scopes it already had", () => {
    const client: SeededClient = seededClient("wallow-web-client");
    const scopes: string[] = client.scopes ?? [];
    expect(scopes).toEqual(
      expect.arrayContaining(["openid", "email", "profile", "roles", "offline_access"]),
    );
  });
});
