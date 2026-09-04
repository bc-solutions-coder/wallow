# @bc-solutions-coder/api-errors

The Wallow API failure model, with no runtime dependency and no React: one `ApiFailure`
type for a problem the API answered with, a transport fault on the way, or a response the
client could not make sense of; the parsers that build one; and the resolver that turns one
into a sentence.

```ts
import {
  failureFromResponse,
  isSilentFailure,
  resolveFailureMessage,
  toApiFailure,
} from "@bc-solutions-coder/api-errors";

const response = await fetch("/api/v1/organizations/42");
if (!response.ok) {
  throw failureFromResponse(response, await response.text());
}

try {
  await save();
} catch (error) {
  if (isSilentFailure(error)) return; // the caller aborted
  showBanner(resolveFailureMessage(error, { fallback: "Could not save." }));
}
```

| Export                                        | What it does                                                                                                                                                                                             |
| --------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ApiFailure`, `isApiFailure`                  | The failure type (`status`, `code`, `title`, `detail?`, `traceId?`, `requestId?`, `fieldErrors?`, `retryAfter?`, native `cause`) and its cross-bundle brand check.                                       |
| `failureFromResponse(response, body)`         | Problem+json (top-level `code`), an OAuth `{ error, error_description }` body (`OAuth.InvalidGrant`), or `Client.UnrecognizedResponse` with the body on `cause`. Reads `x-request-id` and `Retry-After`. |
| `toApiFailure(input, context?)`               | Passes a failure through; classifies a thrown error as `Transport.NetworkError` 503, `Transport.Timeout` 504 or `Transport.Aborted` 499; reads a plain object as a body.                                 |
| `resolveFailureMessage(error, options?)`      | Call-site `messages` → app `registry` → shipped copy per code → `detail` (4xx, API codes only) → shipped copy per status → `fallback` → a generic sentence. Always a string.                             |
| `defineFailureMessages(entries)`              | Declares an app's registry: `{ [code]: (failure) => string }`.                                                                                                                                           |
| `splitFieldErrors(failure, knownFields)`      | Distributes `fieldErrors` across a form's fields, folding `Branding.DisplayName` onto `brandingDisplayName`; what matches nothing comes back as `unmatched` messages.                                    |
| `isSilentFailure(error)`                      | `true` for `Transport.Aborted` only.                                                                                                                                                                     |
| `ErrorCode`, `ClientErrorCode`, `FailureCode` | The API's published catalogue (generated from the OpenAPI document), the codes the client mints itself, and their union.                                                                                 |

`ErrorCode` is regenerated with `pnpm generate` from the SDK's committed OpenAPI snapshot;
`pnpm check:generated` at the workspace root fails when the committed output is stale.
