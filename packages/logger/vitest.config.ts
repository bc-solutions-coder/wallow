import { playwright } from "@vitest/browser-playwright";
import { configDefaults, defineConfig } from "vitest/config";

/**
 * The node + headless-Chromium split, keyed on the same file extension the rest
 * of the workspace uses: `*.test.ts` runs on node, `*.test.tsx` in a real
 * browser.
 *
 * The pair is spelled out here rather than taken from
 * `createVitestProjects()` — the shared preset's `optimizeDeps` baseline
 * pre-bundles `vitest-browser-react` and the React JSX runtimes, and this
 * package renders no components and depends on no React. Adopting the preset
 * would mean adding React to a package that has none purely to satisfy a
 * pre-bundle list. What the preset actually contributes to a package like this
 * one — the two project shapes — is four lines.
 *
 * Only what genuinely needs a browser is in the browser project: the page
 * lifecycle (`visibilitychange`, `pagehide`) and `navigator.sendBeacon`, which
 * is the path a terminal flush takes and the one place "logs vanish on tab
 * close" hides. Everything else — buffering, level filtering, redaction, the
 * ingest guards, the OTLP payload — is `fetch` and plain values, and runs on
 * node.
 */
export default defineConfig({
  test: {
    projects: [
      {
        test: {
          name: "node",
          environment: "node",
          include: ["src/**/*.test.ts"],
          exclude: [...configDefaults.exclude],
        },
      },
      {
        test: {
          name: "browser",
          include: ["src/**/*.test.tsx"],
          exclude: [...configDefaults.exclude],
          browser: {
            enabled: true,
            // Vitest 4 factory provider, NOT the v3 `"playwright"` string (throws).
            provider: playwright(),
            headless: true,
            instances: [{ browser: "chromium" }],
          },
        },
      },
    ],
  },
});
