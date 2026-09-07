# @bc-solutions-coder/api-errors

Wallow's shared API failure model, with no runtime dependencies or framework binding.
It is published to GitHub Packages under the `@bc-solutions-coder` scope. Registry
consumers need access to that registry; workspace consumers use a `workspace:` dependency.
All public imports come from the package root.

Convert a decoded problem into a failure and choose display text:

```ts
import {
  ErrorCode,
  resolveFailureMessage,
  splitFieldErrors,
  toApiFailure,
} from "@bc-solutions-coder/api-errors";

const failure = toApiFailure({
  status: 400,
  code: ErrorCode.VALIDATION_FAILED,
  title: "Validation failed",
  errors: { DisplayName: ["Enter a display name."] },
});
const split = splitFieldErrors(failure, ["displayName"]);
// split.fieldErrors.displayName is ["Enter a display name."]
const message = resolveFailureMessage(failure, { unmatched: split.unmatched });
```

For a failed fetch response, call
`failureFromResponse(response, await response.text())`. The response status overrides
any status in the body. A top-level string `code` identifies a problem; OAuth error
bodies become codes such as `OAuth.InvalidGrant`. Other bodies become
`Client.UnrecognizedResponse`, with the original body on `cause` rather than display
text. The parser reads `x-request-id` and `Retry-After` headers.

`toApiFailure(error)` passes existing failures through. Without a supplied status, a
thrown `Error` becomes a network failure, timeout, or abort. `isSilentFailure(error)`
identifies aborts. Use `isApiFailure` rather than `instanceof` when failures can cross
bundled copies of the package. `ApiFailure.message` is diagnostic text; display messages
come from `resolveFailureMessage`.

| Export                                        | Result                                                                                                                                                        |
| --------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ApiFailure`, `ApiFailureInit`                | Status, code, title, optional detail, correlation IDs, field errors, retry delay, and native cause.                                                           |
| `ErrorCode`, `ClientErrorCode`, `FailureCode` | Generated API codes, client/BFF codes, and their named union. Unknown string codes are also accepted by failures.                                             |
| `defineFailureMessages`                       | A typed code-to-callback registry, unchanged at runtime.                                                                                                      |
| `resolveFailureMessage`                       | Display text using call-site messages, registry, unmatched validation messages, shipped code text, eligible detail, status text, fallback, then generic text. |
| `splitFieldErrors`                            | Field matches by exact, camelCase, or folded dotted name, plus unmatched messages. Later colliding keys replace earlier messages.                             |
| `parseRetryAfter`                             | Whole seconds from a delta-seconds or HTTP-date header, or `undefined`.                                                                                       |
| `failureReference`                            | Trace and request IDs for transport or 5xx failures, or `undefined` when none should be shown.                                                                |

Default message resolution does not expose client-code or 5xx details. Custom registry
callbacks choose their own text and can throw. Correlation IDs and field errors are
optional; callers should not assume every failure carries them.

`ErrorCode` is generated from the SDK's committed OpenAPI snapshot. Run
`pnpm --filter @bc-solutions-coder/api-errors generate` to refresh it. The workspace
`pnpm check:generated` command verifies generated output; do not edit `src/generated`.
