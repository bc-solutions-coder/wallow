/**
 * wallow-web's failure-message registry: the one table of app-specific
 * sentences `resolveFailureMessage` consults after a call site's own
 * `messages` and before the package's shipped copy. It is mounted once through
 * `FailureMessagesProvider` in the root route and handed to the query client's
 * unhandled-failure callback, so a banner, a form, and a toast all say the
 * same thing for the same code.
 *
 * The entries are the former MFA error map. The MFA endpoints
 * (`api/src/Modules/Identity/Wallow.Identity.Api/Controllers/MfaController.cs`)
 * still answer business failures with a raw `{ succeeded: false, error:
 * "<token>" }` body rather than an RFC 7807 problem, and the parser normalises
 * that body under the OAuth grammar: `no_auth_session` becomes
 * `OAuth.NoAuthSession`, with the raw token kept as the title. The keys here
 * are those normalised codes. When the controller moves onto the catalogue
 * (`Mfa.SessionMissing`, `Mfa.CodeInvalid`, …) the problem's own `detail`
 * carries the sentence and these entries go.
 */
import { defineFailureMessages, type FailureMessageRegistry } from "@bc-solutions-coder/api-errors";

export const failureMessages: FailureMessageRegistry = defineFailureMessages({
  "OAuth.NoAuthSession": () => "Your session has expired. Please sign in again.",
  "OAuth.InvalidPassword": () => "That password is incorrect.",
  "OAuth.InvalidCode": () => "That verification code is not valid.",
});
