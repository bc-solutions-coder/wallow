/**
 * The login feature's public contract. See `features/accept-terms/index.ts`.
 *
 * Everything in `api.ts` — every account mutation — stays internal to the
 * screens that call it; the route needs only the screen and the message
 * narrowing.
 */
export { isPasswordResetMessage, PASSWORD_RESET_MESSAGE } from "./auth-result";
export { LoginScreen } from "./components/LoginScreen";
