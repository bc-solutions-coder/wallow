import {
  createApiPassthrough,
  type ApiPassthrough,
} from "@bc-solutions-coder/sdk/server/passthrough";
import { createFileRoute } from "@tanstack/react-router";

/**
 * `/v1/**` — the same-origin API proxy, built from the SDK's
 * `./server/passthrough` preset. Present so the smoke covers the fourth and last
 * export subpath the tarball declares.
 *
 * Built lazily and memoised at module scope, like the workspace apps do: it
 * reads `WALLOW_API_INTERNAL_URL`, which this app never sets, so constructing it
 * at module load would throw inside the server bundle's evaluation.
 */
let passthrough: ApiPassthrough | undefined;

export const Route = createFileRoute("/v1/$")({
  server: {
    handlers: {
      ANY: ({ request }): Promise<Response> => {
        passthrough ??= createApiPassthrough();
        return passthrough.handle(request);
      },
    },
  },
});
