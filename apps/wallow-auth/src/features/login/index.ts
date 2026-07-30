/**
 * The login feature's public contract. See `features/accept-terms/index.ts`.
 *
 * `clientBrandingGetBrandingOptions` is here as category two: `routes/login.tsx`
 * prefetches it in its loader. The rest of `api.ts` — every account mutation —
 * stays internal to the screens that call it.
 */
export { clientBrandingGetBrandingOptions } from "./api";
export { isPasswordResetMessage, PASSWORD_RESET_MESSAGE } from "./auth-result";
export { LoginScreen } from "./components/LoginScreen";
