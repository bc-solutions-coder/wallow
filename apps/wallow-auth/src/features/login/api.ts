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
 *  - `routes/login.tsx` → `clientBrandingGetBrandingOptions`
 *
 * The route's branding read is here, and not in a seam of its own, because the
 * login route reads fork branding for the screen it HOSTS: the data belongs to the
 * login feature even though the file sits outside `features/`.
 *
 * THE MAGIC-LINK ASYMMETRY, so nobody tidies it away: `send` is a POST and gets an
 * `{op}Mutation()`; `verify` is a GET, so the generator emits an `{op}Options()`
 * factory and no mutation factory at all, and the redemption runs through
 * `queryClient.fetchQuery`. `src/generated-mutations.test.ts` pins the absence by
 * scanning this app's source for the name the generator does not emit — so do not
 * spell that name here either.
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
  clientBrandingGetBrandingOptions,
} from "@bc-solutions-coder/sdk/query";
