import { createClient } from "@hey-api/client-fetch";

/**
 * Options for configuring the Wallow browser client.
 */
export interface WallowClientOptions {
  /**
   * Base URL the client points at. Defaults to the same-origin BFF path `/api`.
   */
  baseUrl?: string;
}

/**
 * Module-level client singleton shared with the generated SDK functions.
 */
export const client = createClient();

/**
 * Configure the Wallow browser client for same-origin BFF use.
 *
 * Points the shared client at the BFF path (defaulting to `/api`) and includes
 * credentials so the browser sends the session cookie on every request.
 */
export function configureWallowClient(options: WallowClientOptions = {}): void {
  client.setConfig({
    baseUrl: options.baseUrl ?? "/api",
    credentials: "include",
  });
}
