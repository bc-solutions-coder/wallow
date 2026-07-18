import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

// apps/wallow-web/src -> repo root (src -> wallow-web -> apps -> repo).
const repoRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");

function read(relativePath: string): string {
  return readFileSync(resolve(repoRoot, relativePath), "utf8");
}

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
  const seed: SeedFile = JSON.parse(read("api/seed.json")) as SeedFile;
  const client: SeededClient | undefined = seed.clients.find((c) => c.clientId === clientId);
  if (client === undefined) {
    throw new Error(`seed.json has no client '${clientId}'`);
  }
  return client;
}

// Extract a single top-level docker-compose service block (2-space indented key)
// so assertions target ONE service. This matters because the React BFF example
// service (`bff-example`) already references apps/wallow-web/Dockerfile, so a
// whole-file grep cannot distinguish it from the `wallow-web` service this task
// repoints. The block ends at the next 2-space-indented line (the next service
// key or its leading comment).
function composeServiceBlock(composeYaml: string, serviceName: string): string {
  const lines: string[] = composeYaml.split("\n");
  const startIdx: number = lines.indexOf(`  ${serviceName}:`);
  if (startIdx === -1) {
    throw new Error(`compose service '${serviceName}' not found`);
  }
  const body: string[] = [];
  for (let i = startIdx + 1; i < lines.length; i++) {
    if (/^ {2}\S/u.test(lines[i])) {
      break;
    }
    body.push(lines[i]);
  }
  return body.join("\n");
}

describe("seeded wallow-web-client identity (api/seed.json)", () => {
  it("uses the BFF callback path /bff/callback, not Blazor's /signin-oidc", () => {
    const client: SeededClient = seededClient("wallow-web-client");
    const redirects: string[] = client.redirectUris ?? [];
    expect(redirects.some((uri) => uri.endsWith("/bff/callback"))).toBe(true);
    expect(redirects.some((uri) => uri.includes("/signin-oidc"))).toBe(false);
  });

  it("drops the Blazor /signout-callback-oidc post-logout path", () => {
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

describe("docker-compose.test.yml wallow-web service", () => {
  it("builds the React image from apps/wallow-web/Dockerfile (repo-root context)", () => {
    const block: string = composeServiceBlock(read("docker/docker-compose.test.yml"), "wallow-web");
    expect(block).toMatch(/dockerfile:\s*apps\/wallow-web\/Dockerfile/u);
    expect(block).toMatch(/context:\s*\.\./u);
  });

  it("no longer pulls the prebuilt Blazor wallow-web:test image", () => {
    const block: string = composeServiceBlock(read("docker/docker-compose.test.yml"), "wallow-web");
    expect(block).not.toMatch(/image:\s*wallow-web:test/u);
  });

  it("runs the Node app, not the Blazor .NET host (no ASPNETCORE_* env)", () => {
    const block: string = composeServiceBlock(read("docker/docker-compose.test.yml"), "wallow-web");
    expect(block).not.toMatch(/ASPNETCORE_URLS/u);
    expect(block).not.toMatch(/Oidc__ClientId/u);
  });

  it("authenticates as the wallow-web-client seeded client", () => {
    const block: string = composeServiceBlock(read("docker/docker-compose.test.yml"), "wallow-web");
    expect(block).toMatch(/wallow-web-client/u);
  });

  it("leaves the separate bff-example service on the bcordes-bff client", () => {
    const block: string = composeServiceBlock(
      read("docker/docker-compose.test.yml"),
      "bff-example",
    );
    expect(block).toMatch(/OIDC_CLIENT_ID:\s*bcordes-bff/u);
  });
});

describe("docker-compose.production.yml wallow-web service", () => {
  it("keeps publishing under the ghcr wallow-web image name", () => {
    const block: string = composeServiceBlock(
      read("docker/docker-compose.production.yml"),
      "wallow-web",
    );
    expect(block).toMatch(/image:\s*ghcr\.io\/[^\s]*wallow-web/u);
  });

  it("runs the Node app, not the Blazor .NET host (no ASPNETCORE_*/Oidc__ env)", () => {
    const block: string = composeServiceBlock(
      read("docker/docker-compose.production.yml"),
      "wallow-web",
    );
    expect(block).not.toMatch(/ASPNETCORE_URLS/u);
    expect(block).not.toMatch(/Oidc__ClientId/u);
  });
});

describe("CI builds the wallow-web image from the Node Dockerfile", () => {
  it("ci.yml no longer builds wallow-web via Blazor dotnet publish", () => {
    const yaml: string = read(".github/workflows/ci.yml");
    expect(yaml).not.toMatch(/dotnet publish api\/src\/Wallow\.Web\/Wallow\.Web\.csproj/u);
  });

  it("ci.yml builds the wallow-web:test image from apps/wallow-web/Dockerfile", () => {
    const yaml: string = read(".github/workflows/ci.yml");
    expect(yaml).toMatch(/apps\/wallow-web\/Dockerfile/u);
  });

  it("deploy.yml no longer builds wallow-web via Blazor dotnet publish", () => {
    const yaml: string = read(".github/workflows/deploy.yml");
    expect(yaml).not.toMatch(/dotnet publish api\/src\/Wallow\.Web\/Wallow\.Web\.csproj/u);
  });

  it("deploy.yml builds the wallow-web image from apps/wallow-web/Dockerfile", () => {
    const yaml: string = read(".github/workflows/deploy.yml");
    expect(yaml).toMatch(/apps\/wallow-web\/Dockerfile/u);
  });

  it("still maps the local wallow-web image to the ghcr -web repository", () => {
    const yaml: string = read(".github/workflows/deploy.yml");
    expect(yaml).toMatch(/\["wallow-web"\]="\$\{IMAGE_BASE\}-web"/u);
  });
});

describe("the .NET-era e2e runner stays deleted", () => {
  // scripts/run-e2e.sh was removed with the xUnit E2E suite; e2e is now the
  // per-app Playwright suites (see .claude/rules/E2E.md — do not recreate it).
  it("scripts/run-e2e.sh does not exist", () => {
    expect(existsSync(resolve(repoRoot, "scripts/run-e2e.sh"))).toBe(false);
  });
});

describe("wallow-web e2e image tag is consistent across ci.yml and compose", () => {
  // The e2e job builds+caches+loads a tag then `docker compose up` reuses it only if the
  // tags match; a mismatch silently rebuilds the image at runtime, defeating the cache.
  it("docker-compose.test.yml wallow-web pins the canonical wallow-web-react:test tag", () => {
    const block: string = composeServiceBlock(read("docker/docker-compose.test.yml"), "wallow-web");
    expect(block).toMatch(/image:\s*wallow-web-react:test\b/u);
  });

  it("ci.yml builds and saves that same wallow-web-react:test tag", () => {
    const yaml: string = read(".github/workflows/ci.yml");
    expect(yaml).toMatch(/-t wallow-web-react:test\b/u);
    expect(yaml).toMatch(/wallow-web-react:test wallow-web-react:test-arm64/u);
  });

  it("ci.yml no longer references the dead bare wallow-web:test tag", () => {
    const yaml: string = read(".github/workflows/ci.yml");
    expect(yaml).not.toMatch(/\bwallow-web:test\b/u);
  });
});
