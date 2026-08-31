/**
 * Login feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`), added by Wallow-x4qn.9.4 to bring wallow-auth
 * in line with wallow-web. Every artifact below is GENERATED from
 * `packages/sdk/openapi/v1.json` and takes `{ client }` — read the request-scoped
 * instance off the router context (`useRouteContext({ from: "__root__" }).sdk`);
 * there is nothing to configure or bootstrap first.
 *
 * This is the app's widest seam because `login` is four panels and a route:
 *
 *  - `PasswordLoginForm` → `accountLoginMutation`
 *  - `OtpLoginForm` → `accountSendOtpMutation`, `accountVerifyOtpMutation`
 *  - `MagicLinkLoginForm` → `accountSendMagicLinkMutation`,
 *    `accountVerifyMagicLinkOptions`
 *  - `ExternalProviders` → `accountGetExternalProvidersOptions`
 *
 * (The route's old per-client branding read, `clientBrandingGetBrandingOptions`,
 * is GONE with its anonymous endpoint: branding now arrives through the root
 * loader's transaction-scoped context — `shared/lib/authorize-context.ts`.)
 *
 * THE MAGIC-LINK ASYMMETRY, so nobody tidies it away: `send` is a POST and gets an
 * `{op}Mutation()`; `verify` is a GET, so the generator emits an `{op}Options()`
 * factory and no mutation factory at all, and the redemption runs through
 * `queryClient.fetchQuery`. `src/app-wiring.test.ts` pins the shape the
 * generator DOES emit for it (a `queryFn` and a key); the mutation factory's absence
 * needs no guard of its own, since naming one the generator never emitted is a type
 * error at the import.
 *
 * The two `{op}QueryKey` factories are the specs' half of the surface:
 * `ExternalProviders.test.tsx` seeds the provider cache and
 * `MagicLinkLoginForm.verify.test.tsx` reads the entry the redemption leaves
 * behind. They resolve their keys through this seam for the same reason the screens
 * do — a key built anywhere else is a key the screen never wrote. Generated keys
 * are flat (`[{ _id, baseUrl, tags, ...args }]`) with no prefix to sweep by.
 *
 * `isSafeReturnUrl` and `buildExchangeTicketUrl` stay direct imports from the raw
 * barrel in `LoginScreen`: a predicate and a URL builder issue no request, and the
 * seam lists this feature's ENDPOINTS.
 */
export {
  accountGetExternalProvidersOptions,
  accountGetExternalProvidersQueryKey,
  accountLoginMutation,
  accountSendMagicLinkMutation,
  accountSendOtpMutation,
  accountVerifyMagicLinkOptions,
  accountVerifyMagicLinkQueryKey,
  accountVerifyOtpMutation,
} from "@bc-solutions-coder/sdk/query";
