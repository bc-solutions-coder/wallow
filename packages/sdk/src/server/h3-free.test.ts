/**
 * The SDK ships no h3 (Wallow-pu6a.3.4).
 *
 * `server/handlers.ts` and `server/proxy.ts` were ported to web-standard
 * `Request`/`Response` in Wallow-pu6a.3.2/3.3, which left `h3` in the package
 * manifest as a dependency nothing imports. This spec is the deliverable's
 * assertion shape: the thing being shipped is the ABSENCE of a dependency, so
 * the test reads the dependency graph (manifest + source imports) rather than
 * exercising behaviour.
 *
 * The behavioural half is the other direction of the same claim — with h3 gone
 * the server entry must still be a working web-standard handler factory, and
 * the handler TYPE must be nameable by consumers, since `h3`'s `EventHandler`
 * (which the hosts annotated their handlers with before the port) is no longer
 * available to them.
 */
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import {
  createApiProxy,
  createBffHandlers,
  CookieSessionStore,
  type ApiProxyHandler,
  type BffConfig,
  type BffHandler,
  type BffHandlers,
  type SessionStore,
} from "./index";

// packages/sdk/src/server -> packages/sdk
const packageRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
// packages/sdk/src/server -> repo root
const repoRoot: string = resolve(packageRoot, "..", "..");
const srcDir: string = resolve(packageRoot, "src");
const appsDir: string = resolve(repoRoot, "apps");

/**
 * Directories the source sweep never descends into: installed packages and
 * build output are not authored modules, and a stale `dist/` would otherwise
 * report an import the current source no longer has.
 */
const SKIPPED_DIRECTORIES: ReadonlySet<string> = new Set([
  "node_modules",
  "dist",
  "build",
  "coverage",
  "test-results",
  "playwright-report",
]);

/** The four dependency groups npm/pnpm install from. */
const DEPENDENCY_GROUPS: readonly string[] = [
  "dependencies",
  "devDependencies",
  "peerDependencies",
  "optionalDependencies",
];

interface PackageManifest {
  dependencies?: Record<string, string>;
  devDependencies?: Record<string, string>;
  peerDependencies?: Record<string, string>;
  optionalDependencies?: Record<string, string>;
}

function readManifest(path: string): PackageManifest {
  return JSON.parse(readFileSync(path, "utf8")) as PackageManifest;
}

/** Directory entries a sweep descends into: authored source, not tooling output. */
function scannableEntries(dir: string): string[] {
  return readdirSync(dir).filter(
    (entry: string): boolean => !entry.startsWith(".") && !SKIPPED_DIRECTORIES.has(entry),
  );
}

/** Every `.ts` file under the given directory, recursively. */
function typescriptFiles(dir: string): string[] {
  const found: string[] = [];
  for (const entry of scannableEntries(dir)) {
    const full: string = join(dir, entry);
    if (statSync(full).isDirectory()) {
      found.push(...typescriptFiles(full));
    } else if (full.endsWith(".ts") || full.endsWith(".tsx")) {
      found.push(full);
    }
  }
  return found;
}

/**
 * Every workspace app under `apps/`, as a repo-relative path — an app being any
 * directory with its own `package.json` (the nesting of `apps/examples/*` is
 * discovered, not spelled out). Derived rather than listed so that moving,
 * renaming, or adding an app cannot make this sweep read a path that no longer
 * exists; a hard-coded list did exactly that when the Start migration deleted
 * the hand-rolled proxy hosts.
 */
function workspaceApps(dir: string): string[] {
  if (existsSync(join(dir, "package.json"))) {
    return [relative(repoRoot, dir)];
  }
  const found: string[] = [];
  for (const entry of scannableEntries(dir)) {
    const full: string = join(dir, entry);
    if (statSync(full).isDirectory()) {
      found.push(...workspaceApps(full));
    }
  }
  return found;
}

/**
 * Matches a real module reference to h3 — an `import ... from "h3"` (or an h3
 * subpath), a bare side-effect `import "h3"`, or a `require("h3")`. Comments
 * that merely NAME h3 (several survive as history in the ported files) are not
 * dependency edges and must not trip this.
 */
const H3_IMPORT_PATTERN: RegExp = /(?:from|import|require\()\s*["']h3(?:\/[^"']*)?["']/u;

/**
 * This spec is itself under `src/`, and the pattern literals above contain the
 * very specifier they look for. Scanning the scanner would be a permanent false
 * positive, so it is the one file the source sweep skips.
 */
const SELF: string = fileURLToPath(import.meta.url);

/** A config that needs no environment and no live OP. */
function makeConfig(): BffConfig {
  return {
    issuer: "https://issuer.h3-free.example.com",
    clientId: "web-bff",
    clientSecret: "s3cret",
    redirectUri: "https://app.example.com/bff/callback",
    postLogoutRedirectUri: "https://app.example.com/",
    scopes: ["openid", "profile", "email", "offline_access"],
    apiBaseUrl: "https://api.example.com",
    cookieName: "wallow_bff",
    cookiePassword: "x".repeat(32),
    sessionTtlSeconds: 86_400,
    cookieSecure: true,
  };
}

function makeStore(): SessionStore {
  return new CookieSessionStore({ password: "x".repeat(32) });
}

describe("h3 is gone from the SDK dependency graph", () => {
  it.each(DEPENDENCY_GROUPS)("declares no h3 under %s", (group: string) => {
    const manifest: Record<string, Record<string, string> | undefined> = readManifest(
      resolve(packageRoot, "package.json"),
    ) as unknown as Record<string, Record<string, string> | undefined>;

    expect(Object.keys(manifest[group] ?? {})).not.toContain("h3");
  });

  it("keeps the runtime deps the port still needs", () => {
    const manifest: PackageManifest = readManifest(resolve(packageRoot, "package.json"));
    const runtime: string[] = Object.keys(manifest.dependencies ?? {});

    // Dropping h3 must not take the cookie/crypto/OIDC layers with it — the
    // ported handlers parse and serialize cookies through cookie-es, seal
    // sessions through iron-webcrypto, and delegate the OIDC grants.
    expect(runtime).toContain("cookie-es");
    expect(runtime).toContain("iron-webcrypto");
    expect(runtime).toContain("openid-client");
  });

  it("imports h3 from no module under src/", () => {
    const offenders: string[] = typescriptFiles(srcDir)
      .filter((file: string): boolean => file !== SELF)
      .filter((file: string): boolean => H3_IMPORT_PATTERN.test(readFileSync(file, "utf8")));

    expect(offenders).toEqual([]);
  });
});

describe("the server entry stays functional on the web-standard API", () => {
  it("exposes the four BFF handlers as (Request) => Promise<Response>", async () => {
    const handlers: BffHandlers = createBffHandlers(makeConfig(), makeStore());
    // Named as the exported handler type: with h3 gone, `EventHandler` is no
    // longer available to hosts, so the SDK must name this shape itself.
    const user: BffHandler = handlers.user;

    const response: Response = await user(new Request("https://app.example.com/bff/user"));

    expect(response).toBeInstanceOf(Response);
    expect(response.status).toBe(401);
  });

  it("exposes the api proxy as (Request) => Promise<Response>", async () => {
    const proxy: ApiProxyHandler = createApiProxy(makeConfig(), makeStore());

    const response: Response = await proxy(
      new Request("https://app.example.com/api/notifications"),
    );

    expect(response).toBeInstanceOf(Response);
    expect(response.status).toBe(401);
  });

  it("answers a request built with no framework event object at all", async () => {
    const handlers: BffHandlers = createBffHandlers(makeConfig(), makeStore());

    // Nothing h3-shaped is constructed anywhere in this call: a bare WHATWG
    // Request goes in and a bare WHATWG Response comes out. Under h3 this
    // needed createApp() + app.use() + toWebHandler().
    const response: Response = await handlers.logout(
      new Request("https://app.example.com/bff/logout", { method: "GET" }),
    );

    expect(response.status).toBe(405);
    expect(response.headers.get("allow")).toBe("POST");
  });
});

describe("no app host takes h3 back", () => {
  // The other end of the same claim. The apps that used to hand-roll an h3
  // proxy now consume the SDK's server entry (and its `/passthrough` subpath)
  // over web-standard Request/Response, so h3 is gone from the host layer too.
  // Dropping it from the SDK must not be paid for by an app reaching for h3
  // directly — that would put the framework back in the tree one layer up.
  const appHosts: readonly string[] = workspaceApps(appsDir);

  it("finds the app hosts to sweep", () => {
    // Without this the two cases below go vacuously green if `apps/` is ever
    // restructured: an empty derived list runs no cases and fails nothing.
    expect(appHosts.length).toBeGreaterThan(0);
  });

  it.each(appHosts)("%s declares no h3 dependency", (app: string) => {
    const manifest: Record<string, Record<string, string> | undefined> = readManifest(
      resolve(repoRoot, app, "package.json"),
    ) as unknown as Record<string, Record<string, string> | undefined>;

    for (const group of DEPENDENCY_GROUPS) {
      expect(Object.keys(manifest[group] ?? {})).not.toContain("h3");
    }
  });

  it.each(appHosts)("%s imports h3 from no module", (app: string) => {
    const offenders: string[] = typescriptFiles(resolve(repoRoot, app))
      .filter((file: string): boolean => H3_IMPORT_PATTERN.test(readFileSync(file, "utf8")))
      .map((file: string): string => relative(repoRoot, file));

    expect(offenders).toEqual([]);
  });
});
