import { isSafeReturnUrl, type WallowUser } from "@bc-solutions-coder/sdk";
import { changelogGetChangelogQueryKey } from "@bc-solutions-coder/sdk/query";
import { createFileRoute } from "@tanstack/react-router";
import type { ReactElement } from "react";

/**
 * The app's only rendered route (`/`).
 *
 * It exists to pull the SDK's `./query` subpath — the generated TanStack Query
 * surface — into the CLIENT bundle, which is where a broken `exports` map or a
 * `.d.ts` that references a type the tarball forgot to ship would show up. The
 * root entry is exercised too: `isSafeReturnUrl` as a runtime value export and
 * `WallowUser` as a type export. Everything here is pure — nothing is fetched,
 * so the smoke needs no backend.
 */
function Home(): ReactElement {
  const queryKey: unknown = changelogGetChangelogQueryKey();
  const anonymous: WallowUser | null = null;

  return (
    <main className="bg-background text-foreground p-8">
      <h1 data-testid="fork-smoke-heading">Fork smoke</h1>
      <p data-testid="fork-smoke-query-key">{JSON.stringify(queryKey)}</p>
      <p data-testid="fork-smoke-safe-return-url">{String(isSafeReturnUrl("/dashboard"))}</p>
      <p data-testid="fork-smoke-user">{JSON.stringify(anonymous)}</p>
    </main>
  );
}

export const Route = createFileRoute("/")({
  component: Home,
});
