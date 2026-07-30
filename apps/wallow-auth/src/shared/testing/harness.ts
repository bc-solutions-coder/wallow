/**
 * The shared SDK harness, configured for wallow-auth's API surface.
 *
 * This app does not sit behind a `/api` BFF prefix the way wallow-web does. Its
 * server routes are a passthrough reverse proxy mounting `/v1/**` and
 * `/connect/**` at the ROOT (`src/routes/v1/$.ts`, `src/routes/connect/$.ts`),
 * and `src/router.tsx` builds its SDK with `baseUrl: globalThis.location.origin`
 * to match. A harness on the shared default would record `/api/v1/...` paths
 * this app never issues, so the specs' endpoint constants stay bare `/v1/...`
 * and the harness origin is what moves.
 */
import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";

/** Origin the harness answers on — no path prefix, mirroring `location.origin`. */
export const AUTH_HARNESS_ORIGIN = "http://wallow.test";

/** Build a harness whose recorded `call.path` matches the real app's requests. */
export function createAuthHarness(): SdkHarness {
  return createSdkHarness({ baseUrl: AUTH_HARNESS_ORIGIN });
}
