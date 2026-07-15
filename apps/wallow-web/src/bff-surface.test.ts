import { readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Regression guard (Wallow-8w1h.2.2 acceptance: "existing BFF/proxy surface
 * untouched"). Scaffolding the Start SSR shell must NOT disturb the current h3
 * BFF host in `server.ts` — the E2E `BffFlowTests`/`DockerComposeFixture`
 * contract still depends on it until task 1.3 ports it to Start server routes.
 *
 * NOTE: this guard is expected to PASS in the red phase (server.ts is untouched
 * today); it exists to fail loudly if the green phase edits the BFF host.
 */
const serverSource = await readFile(
  fileURLToPath(new URL("../server.ts", import.meta.url)),
  "utf8",
);

describe("BFF/proxy surface preserved during Start scaffold", () => {
  it("keeps the /health liveness route for the E2E fixture wait", () => {
    expect(serverSource).toContain('"/health"');
  });

  it.each([
    ["/bff/login", '"/bff/login"'],
    ["/bff/callback", '"/bff/callback"'],
    ["/bff/user", '"/bff/user"'],
    ["/bff/logout", '"/bff/logout"'],
    ["/api/**", '"/api/**"'],
  ])("keeps the %s route registered", (_label, needle) => {
    expect(serverSource).toContain(needle);
  });
});
