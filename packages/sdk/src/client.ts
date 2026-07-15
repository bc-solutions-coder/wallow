import { client } from "./generated/client.gen";

/**
 * Options for configuring the Wallow browser client against the BFF.
 */
export interface BffClientOptions {
  /**
   * Base URL the client points at. Defaults to the same-origin BFF path `/api`.
   */
  baseUrl?: string;
}

export type { BffClientOptions as WallowClientOptions };

/**
 * The client instance every generated SDK operation calls through.
 */
export { client };

/**
 * Configure the Wallow browser client for same-origin BFF use.
 *
 * Points the shared client at the BFF path (defaulting to `/api`) and includes
 * credentials so the browser sends the session cookie on every request.
 */
export function configureBffClient(options: BffClientOptions = {}): void {
  client.setConfig({
    baseUrl: options.baseUrl ?? "/api",
    credentials: "include",
  });
}

/**
 * Configure the Wallow browser client for same-origin BFF use.
 *
 * @deprecated Use {@link configureBffClient} instead. This alias is kept for back-compat and will be removed in a future major release.
 */
export const configureWallowClient = configureBffClient;
