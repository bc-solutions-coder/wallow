import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

// Acceptance-criteria guard for Wallow-m5aq.1.5 (Storybook 10 +
// @storybook/addon-vitest).
//
// Storybook here is NOT just a component explorer bolted onto the side: it is
// the third leg of this package's test suite. `@storybook/addon-vitest` turns
// every story into a Vitest test case that renders in the same headless
// Chromium the `browser` project already uses, so `pnpm --filter
// @bc-solutions-coder/ui test` runs node | browser | storybook. That makes the
// Storybook wiring part of the package contract rather than developer tooling,
// and it is what the 37-component catalog in the later phases will be tested
// through — every component task from Wallow-m5aq.2.1 onward assumes this loop
// exists.
//
// These specs read the wiring off disk (the house style for this package's
// scaffold guards — see package-scaffold.test.ts and dist-structure.test.ts)
// with one exception: the three-project assertion IMPORTS vitest.config.ts, so
// it describes the projects Vitest actually receives rather than the source
// text that happens to produce them.
//
// Two acceptance criteria are deliberately NOT expressed here, because neither
// has a durable on-disk subject: that the temporary Card smoke story proving
// the loop was deleted before commit, and that `pnpm --filter
// @bc-solutions-coder/ui storybook` renders with the fork's real brand tokens.
// Both are one-shot observations for the verifier, not standing invariants.

const packageDir = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const repoRoot = join(packageDir, "..", "..");
const storybookDir = join(packageDir, ".storybook");

function readPackageJson(): Record<string, unknown> {
  return JSON.parse(readFileSync(join(packageDir, "package.json"), "utf8")) as Record<
    string,
    unknown
  >;
}

/** Read a file under `.storybook/`, or `""` when it does not exist yet. */
function readStorybookFile(name: string): string {
  const path = join(storybookDir, name);

  return existsSync(path) ? readFileSync(path, "utf8") : "";
}

/** Every project name in `vitest.config.ts`, as Vitest itself would see them. */
async function vitestProjectNames(): Promise<(string | undefined)[]> {
  const configModule = (await import("../../vitest.config")) as unknown as {
    default: { test?: { projects?: { test?: { name?: string } }[] } };
  };

  return (configModule.default.test?.projects ?? []).map((project) => project.test?.name);
}

/** The Storybook packages this package must carry, all as dev dependencies. */
const STORYBOOK_DEV_DEPENDENCIES = [
  "storybook",
  "@storybook/react-vite",
  "@storybook/addon-vitest",
];

describe("packages/ui storybook dependencies", () => {
  it("declares the storybook packages as dev dependencies", () => {
    const pkg = readPackageJson();
    const devDeps = pkg.devDependencies as Record<string, string> | undefined;

    expect(devDeps).toBeDefined();
    for (const name of STORYBOOK_DEV_DEPENDENCIES) {
      expect(devDeps, name).toHaveProperty(name);
    }
  });

  it("keeps storybook out of the shipped dependency surface", () => {
    const pkg = readPackageJson();
    const deps = pkg.dependencies as Record<string, string>;
    const peers = pkg.peerDependencies as Record<string, string>;

    // Stories and their runner are authoring-time only. Anything that reaches
    // `dependencies` here is installed by every consuming app, and Storybook is
    // an order of magnitude larger than the library it documents.
    for (const name of STORYBOOK_DEV_DEPENDENCIES) {
      expect(deps, name).not.toHaveProperty(name);
      expect(peers, name).not.toHaveProperty(name);
    }
  });

  it("resolves an installed Storybook on the 10 line", () => {
    // Guards the install, not just the declaration — mirroring the runtime-deps
    // guard in package-scaffold.test.ts. Storybook 10 is the version whose
    // `@storybook/addon-vitest` exposes the `vitest-plugin` subpath the config
    // below imports; on 8.x that addon does not exist under this name.
    const installedManifest = join(packageDir, "node_modules", "storybook", "package.json");
    expect(existsSync(installedManifest)).toBe(true);

    const installed = JSON.parse(readFileSync(installedManifest, "utf8")) as { version: string };
    expect(installed.version).toMatch(/^10\./u);
  });

  it("resolves installed copies of the react-vite framework and the vitest addon", () => {
    for (const name of ["@storybook/react-vite", "@storybook/addon-vitest"]) {
      expect(existsSync(join(packageDir, "node_modules", name, "package.json")), name).toBe(true);
    }
  });

  it("exposes the storybook dev and build scripts", () => {
    const pkg = readPackageJson();
    const scripts = pkg.scripts as Record<string, string>;

    expect(scripts.storybook).toBe("storybook dev -p 6006");
    expect(scripts["build-storybook"]).toBe("storybook build");
  });
});

describe("packages/ui .storybook config", () => {
  it("configures the react-vite framework against the component story glob", () => {
    expect(existsSync(join(storybookDir, "main.ts"))).toBe(true);

    const main = readStorybookFile("main.ts");

    expect(main).toMatch(/framework\s*:\s*["']@storybook\/react-vite["']/u);
    // Stories are co-located with their component, exactly like the *.test.tsx
    // specs are — so the glob points into src/components, not a separate
    // stories/ tree. Tailwind's @source rules in source.css already assume this
    // layout (they exclude ./src/components/**/*.stories.tsx from the scan).
    expect(main).toMatch(/["']\.\.\/src\/components\/\*\*\/\*\.stories\.tsx["']/u);
  });

  it("registers the vitest addon so stories become test cases", () => {
    const main = readStorybookFile("main.ts");

    expect(main).toMatch(/addons\s*:\s*\[[^\]]*["']@storybook\/addon-vitest["']/u);
  });

  it("imports the shared Tailwind entry and scans components from preview.css", () => {
    expect(existsSync(join(storybookDir, "preview.css"))).toBe(true);

    const previewCss = readStorybookFile("preview.css");

    // Storybook gets its utilities the same way an app does: import the shared
    // stylesheet, then declare a @source scan for the sources it renders.
    // packages/ui's own source.css cannot serve here — Tailwind resolves
    // @source relative to the declaring file, and this one lives a directory
    // deeper.
    expect(previewCss).toMatch(/@import\s+["']@bc-solutions-coder\/styles\/styles\.css["']/u);
    expect(previewCss).toMatch(/@source\s+["']\.\.\/src\/components/u);
  });

  it("decorates every story with the fork's real branding tokens", () => {
    expect(existsSync(join(storybookDir, "preview.tsx"))).toBe(true);

    const preview = readStorybookFile("preview.tsx");

    expect(preview).toMatch(/import\s+["']\.\/preview\.css["']/u);
    // The whole point of the decorator: stories render against the SAME token
    // values api/branding.json produces for the apps, so a component that looks
    // right in Storybook looks right in wallow-web. A hand-written palette here
    // would make Storybook lie.
    expect(preview).toMatch(/forkResolvedBranding/u);
    expect(preview).toMatch(/renderThemeStyle/u);
    expect(preview).toMatch(/from\s+["']@bc-solutions-coder\/styles["']/u);
    expect(preview).toMatch(/export\s+const\s+decorators/u);
  });
});

describe("packages/ui vitest projects", () => {
  it("runs node, browser and storybook as three projects", async () => {
    const names = await vitestProjectNames();

    // The acceptance criterion in one line: `pnpm --filter
    // @bc-solutions-coder/ui test` drives all three. Asserted on the imported
    // config rather than its source text so a project that is declared but
    // never pushed onto `projects` still fails.
    expect(names.toSorted()).toEqual(["browser", "node", "storybook"]);
  });

  it("builds the storybook project from the addon's vitest plugin", () => {
    const vitestConfig = readFileSync(join(packageDir, "vitest.config.ts"), "utf8");

    // `storybookTest` is what reads .storybook/main.ts, expands the story glob
    // and hands each story to Vitest as a test case — without it a third
    // project would exist but collect nothing.
    expect(vitestConfig).toMatch(/@storybook\/addon-vitest\/vitest-plugin/u);
    expect(vitestConfig).toMatch(/storybookTest\s*\(/u);
  });

  it("renders stories in the same headless Chromium as the browser project", async () => {
    const configModule = (await import("../../vitest.config")) as unknown as {
      default: {
        test?: {
          projects?: {
            test?: { name?: string; browser?: { enabled?: boolean; headless?: boolean } };
          }[];
        };
      };
    };
    const storybookProject = (configModule.default.test?.projects ?? []).find(
      (project) => project.test?.name === "storybook",
    );

    expect(storybookProject).toBeDefined();
    // A story test that ran in node would assert nothing about rendering, which
    // is the only reason to run stories as tests at all. Real Chromium is the
    // repo-wide rule for anything touching the DOM (.claude/rules/TESTING.md).
    expect(storybookProject?.test?.browser?.enabled).toBe(true);
    expect(storybookProject?.test?.browser?.headless).toBe(true);
  });
});

describe("packages/ui storybook build output", () => {
  it("gitignores the static storybook build", () => {
    const patterns = readFileSync(join(repoRoot, ".gitignore"), "utf8")
      .split("\n")
      .map((line) => line.trim());

    // `storybook build` writes packages/ui/storybook-static/ — a build artifact
    // like dist/, and one large enough that committing it once is painful to
    // undo.
    expect(
      patterns.some((pattern) => /^\/?(packages\/ui\/)?storybook-static\/?$/u.test(pattern)),
    ).toBe(true);
  });
});
