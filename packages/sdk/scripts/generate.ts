import { execFileSync } from "node:child_process";
import { writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

/**
 * Regenerates the typed API client under `src/generated/`.
 *
 * By default the committed `openapi/v1.json` snapshot is used. Set the
 * `WALLOW_OPENAPI_URL` environment variable to refresh that snapshot from a
 * running API before generating (for example
 * `WALLOW_OPENAPI_URL=http://localhost:5001/openapi/v1.json`).
 */
const scriptDir: string = dirname(fileURLToPath(import.meta.url));
const packageRoot: string = resolve(scriptDir, "..");
const specPath: string = resolve(packageRoot, "openapi/v1.json");

const specUrl: string | undefined = process.env.WALLOW_OPENAPI_URL;
if (specUrl) {
  const response: Response = await fetch(specUrl);
  if (!response.ok) {
    throw new Error(
      `Failed to fetch OpenAPI spec from ${specUrl}: ${response.status} ${response.statusText}`,
    );
  }

  const spec: string = await response.text();
  writeFileSync(specPath, spec);
}

// Use execFileSync (never a shell string) so arguments cannot be interpreted
// by a shell.
execFileSync("npx", ["@hey-api/openapi-ts"], {
  cwd: packageRoot,
  stdio: "inherit",
});
