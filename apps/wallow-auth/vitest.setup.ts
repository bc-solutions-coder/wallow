import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";

/**
 * Global test setup for wallow-auth (Wallow-vec7.1.5), mirroring wallow-web's.
 *
 * React Testing Library normally registers `afterEach(cleanup)` itself, but only
 * when a global `afterEach` exists. Vitest `globals` is off in this app, so that
 * auto-registration never happens and rendered DOM — including the
 * `document.body` attributes ReadyIndicator stamps — would leak across tests in
 * the same file. Register the teardown explicitly. It is a no-op for
 * node-environment tests (nothing is ever mounted), so it is safe to apply to
 * every test file regardless of its `@vitest-environment` pragma.
 */
afterEach(() => {
  cleanup();
});
