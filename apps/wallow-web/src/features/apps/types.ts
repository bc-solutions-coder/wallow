/**
 * Apps feature view types (Wallow-8w1h.5.2) — copies the CANONICAL Organizations
 * `types.ts` role (Wallow-8w1h.4.2): a small view-model the list/detail
 * components render. Mirrors the API `DeveloperAppResponse` (`packages/sdk`
 * generated types) and the Blazor `AppModel.cs` oracle 1:1; the facade returns
 * these as `unknown`, so the feature's components narrow to `App` at the render
 * boundary.
 */
export interface App {
  clientId: string;
  displayName: string;
  clientType: string;
  redirectUris: string[];
  createdAt: string | null;
}
