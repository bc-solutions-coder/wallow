/**
 * The verify-email feature's public contract. See `features/accept-terms/index.ts`.
 *
 * `sign-in-href.ts` is deliberately absent: it is an internal helper both screens
 * below share, not something a route composes with.
 */
export { VerifyEmailConfirm } from "./components/VerifyEmailConfirm";
export { VerifyEmailNotice } from "./components/VerifyEmailNotice";
