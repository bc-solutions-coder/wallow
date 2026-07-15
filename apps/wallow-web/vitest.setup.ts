import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";

/**
 * Global test setup for wallow-web (Wallow-8w1h.4.2).
 *
 * React Testing Library normally registers `afterEach(cleanup)` automatically,
 * but only when a global `afterEach` exists. Vitest `globals` is off in this
 * app, so that auto-registration never happens and rendered DOM would leak
 * across tests in the same file. Register the teardown explicitly here. It is a
 * no-op for node-environment tests (nothing is ever mounted), so it is safe to
 * apply to every test file regardless of its `@vitest-environment` pragma.
 */
afterEach(() => {
  cleanup();
});
