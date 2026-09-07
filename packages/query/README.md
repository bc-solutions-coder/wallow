# @bc-solutions-coder/query

TanStack React Query exports plus Wallow cache and failure-reporting defaults.
This private workspace package re-exports the upstream bindings by reference.
Import providers and hooks from the same package to share one query context.

## Setup

```tsx
import { createQueryClient, QueryClientProvider } from "@bc-solutions-coder/query";
import type { ReactNode } from "react";

const queryClient = createQueryClient();

export function BrowserQueries({ children }: { children: ReactNode }) {
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
```

The module-level client above is for a browser app. During SSR, create a new
client for each incoming request so users do not share cached data.

## Exports and failure policy

`createQueryClient(options?)` creates an independent `QueryClient` with automatic
retries disabled for both queries and mutations. Other query behavior follows
TanStack defaults and can be overridden on individual operations.

`CreateQueryClientOptions.onUnhandledFailure` receives `{ kind, error }` through
the `UnhandledFailure` type. The error is passed through unchanged; this package
does not normalize it or render a toast.

- Mutations report every failure unless their metadata has `failureHandled: true`.
- Queries report only when their metadata has `toastFailure: true`, once until
  the query next succeeds.
- `handledFailure(meta?)` adds the mutation flag. `toastedFailure(meta?)` adds the
  query flag. Both return new metadata and preserve existing keys.

```ts
import { handledFailure, toastedFailure } from "@bc-solutions-coder/query";

const mutationMeta = handledFailure({ feature: "profile" });
const queryMeta = toastedFailure({ feature: "status" });
```

Pass these values as the corresponding operation's `meta`. Use handled metadata
only when your component or form displays the failure itself. All public
`@tanstack/react-query` exports, including hooks, query option builders,
hydration utilities, and their types, are also available from this entry.
