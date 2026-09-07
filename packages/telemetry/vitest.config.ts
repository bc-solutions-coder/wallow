import { playwright } from "@vitest/browser-playwright";
import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    projects: [
      { test: { name: "node", environment: "node", include: ["src/**/*.test.ts"] } },
      {
        optimizeDeps: { include: ["@grafana/faro-web-sdk"] },
        test: {
          name: "browser",
          include: ["src/**/*.test.tsx"],
          setupFiles: ["./vitest.setup.ts"],
          browser: {
            enabled: true,
            provider: playwright(),
            headless: true,
            instances: [{ browser: "chromium" }],
          },
        },
      },
    ],
  },
});
