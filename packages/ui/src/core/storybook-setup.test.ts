import { describe, expect, it } from "vitest";

// Storybook here is NOT just a component explorer bolted onto the side: it is
// the third leg of this package's test suite. `@storybook/addon-vitest` turns
// every story into a Vitest test case that renders in the same headless
// Chromium the `browser` project already uses, so `pnpm --filter
// @bc-solutions-coder/ui test` runs node | browser | storybook. Stories are
// therefore this catalog's render coverage, and losing a project silently would
// drop hundreds of test cases while the run still reported green.
//
// That — and only that — is what these two specs pin. They IMPORT
// `vitest.config.ts` and assert the projects Vitest actually receives, so a
// project declared but never pushed onto `projects` still fails.

/** Every project name in `vitest.config.ts`, as Vitest itself would see them. */
async function vitestProjects(): Promise<
  { test?: { name?: string; browser?: { enabled?: boolean; headless?: boolean } } }[]
> {
  const configModule = (await import("../../vitest.config")) as unknown as {
    default: {
      test?: {
        projects?: {
          test?: { name?: string; browser?: { enabled?: boolean; headless?: boolean } };
        }[];
      };
    };
  };

  return configModule.default.test?.projects ?? [];
}

describe("packages/ui vitest projects", () => {
  it("runs node, browser and storybook as three projects", async () => {
    const projects = await vitestProjects();
    const names = projects.map((project) => project.test?.name);

    expect(names.toSorted()).toEqual(["browser", "node", "storybook"]);
  });

  it("renders stories in the same headless Chromium as the browser project", async () => {
    const projects = await vitestProjects();
    const storybookProject = projects.find((project) => project.test?.name === "storybook");

    expect(storybookProject).toBeDefined();
    // A story test that ran in node would assert nothing about rendering, which
    // is the only reason to run stories as tests at all. Real Chromium is the
    // repo-wide rule for anything touching the DOM (.claude/rules/TESTING.md).
    expect(storybookProject?.test?.browser?.enabled).toBe(true);
    expect(storybookProject?.test?.browser?.headless).toBe(true);
  });
});
