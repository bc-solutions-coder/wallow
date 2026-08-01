import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The SSR origin derivations have ONE definition in this workspace, and it is not
 * in any app: it is `@bc-solutions-coder/env`.
 *
 * All three Start apps carried a byte-identical `request-origin.ts` and a
 * hand-rolled `resolveInternalOrigin`, kept in step by a drift guard rather than
 * by the module system. Both values travel into the SDK's `baseUrl` and from
 * there into every generated query key, so a copy that drifts costs an SSR cache
 * hit on one app and not the others.
 *
 * This spec reaches across all three apps because the duplication did. It pins
 * the deletion as well as the adoption: an app that keeps both the package and
 * its own copy has not migrated.
 */

const here: string = dirname(fileURLToPath(import.meta.url));
const appsDir: string = resolve(here, "..", "..");

const ENV = "@bc-solutions-coder/env";

interface StartApp {
  /** Directory under `apps/`. */
  readonly dir: string;
  /** The app's `start.ts`, relative to that directory. */
  readonly start: string;
  /** Where its copy of the helper used to sit. */
  readonly retiredCopy: string;
  /** How this app composes the SDK's `baseUrl` off the resolved origin. */
  readonly baseUrl: RegExp;
}

const APPS: readonly StartApp[] = [
  {
    dir: "wallow-web",
    start: "src/app/start.ts",
    retiredCopy: "src/shared/lib/request-origin.ts",
    // The BFF's `/api` mount, which strips the prefix and forwards upstream.
    baseUrl: /baseUrl:\s*`\$\{requestOrigin\}\$\{API_MOUNT\}`/u,
  },
  {
    dir: "wallow-auth",
    start: "src/app/start.ts",
    retiredCopy: "src/shared/lib/request-origin.ts",
    // Under a based build the bare origin is a different app.
    baseUrl: /baseUrl:\s*withBasePath\(requestOrigin,\s*BASE_PATH\)/u,
  },
  {
    dir: "examples/minimal-app",
    start: "src/start.ts",
    retiredCopy: "src/lib/request-origin.ts",
    // No mount prefix: the passthrough answers `/v1/**` at the root.
    baseUrl: /baseUrl:\s*requestOrigin/u,
  },
];

const read = (relativePath: string): string => readFileSync(resolve(appsDir, relativePath), "utf8");

describe.each(APPS)("$dir routes through the shared env package", (app: StartApp) => {
  const source: string = read(`${app.dir}/${app.start}`);

  it("looks in a directory that is really there", () => {
    // The absence cases below are satisfied for free by a stale directory name.
    const copyDir: string = resolve(appsDir, app.dir, dirname(app.retiredCopy));

    expect(existsSync(copyDir)).toBe(true);
    expect(readdirSync(copyDir).length).toBeGreaterThan(0);
  });

  it.each(["", ".test"])("leaves no local copy%s behind", (suffix: string) => {
    const copy: string = app.retiredCopy.replace(/\.ts$/u, `${suffix}.ts`);

    expect(existsSync(resolve(appsDir, app.dir, copy))).toBe(false);
  });

  it("imports both origin helpers from the package", () => {
    expect(source).toContain(`from "${ENV}/request-origin"`);
    expect(source).toContain(`from "${ENV}/internal-origin"`);
  });

  it("composes its own baseUrl off the resolved origin", () => {
    // The one thing that is genuinely per-app: where each app's API surface
    // answers relative to the origin serving the page.
    expect(source).toMatch(app.baseUrl);
  });

  it("does the process.env read itself, at the call site", () => {
    // The package takes an env record precisely so this file — which Start
    // aliases into the CLIENT bundle too — keeps the read inside the server-only
    // callback rather than at a module's top level.
    expect(source).toMatch(/resolveInternalOrigin\(process\.env\)/u);
  });

  it("declares the package, so pnpm links it rather than hoisting it", () => {
    const manifest = JSON.parse(read(`${app.dir}/package.json`)) as {
      dependencies?: Record<string, string>;
    };

    expect(manifest.dependencies?.[ENV]).toBe("workspace:*");
    expect(existsSync(resolve(appsDir, app.dir, "node_modules", ENV))).toBe(true);
  });
});

describe("the retired helper is not re-declared under another name", () => {
  it.each(APPS)("$dir defines neither resolver again", (app: StartApp) => {
    for (const [entry, source] of appSources(app.dir)) {
      expect(source, `${app.dir}/${entry} redeclares resolveRequestOrigin`).not.toMatch(
        /(?:export\s+)?function\s+resolveRequestOrigin\b/u,
      );
      expect(source, `${app.dir}/${entry} redeclares resolveInternalOrigin`).not.toMatch(
        /(?:export\s+)?function\s+resolveInternalOrigin\b/u,
      );
    }
  });
});

/**
 * Every hand-written TypeScript module under an app's `src`. Specs are excluded
 * (they name the retired symbols in order to forbid them) and so is
 * `routeTree.gen.ts`, which is codegen.
 */
function appSources(dir: string): readonly (readonly [string, string])[] {
  return readdirSync(resolve(appsDir, dir, "src"), { recursive: true })
    .map(String)
    .filter((entry: string) => /\.tsx?$/u.test(entry))
    .filter((entry: string) => !/\.test\.tsx?$/u.test(entry) && !entry.endsWith("routeTree.gen.ts"))
    .map((entry: string) => [entry, read(`${dir}/src/${entry}`)] as readonly [string, string]);
}
