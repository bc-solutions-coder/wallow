/**
 * The CONTRACT between the login shell (`LoginScreen`) and its tab panels
 * (Wallow-vec7.3.11 / 2.8a).
 *
 * The oracle (`api/src/Wallow.Auth/Components/Pages/Login.razor`) is one 600-line
 * component holding three tabs' fields, three submit handlers and three DIFFERENT
 * error switches, over a single shared error banner and a single
 * `HandleSuccessfulAuth`. Ported verbatim that would be one file five beads deep
 * (`.3.12` magic-link, `.3.13` OTP, `.3.14` external providers, `.3.15` MFA
 * hand-off) all editing the same function bodies.
 *
 * The split instead: the SHELL owns what the oracle SHARES across tabs
 * (`_activeTab`, `_errorMessage`, `_signedIn`, the enrollment banner, and
 * `HandleSuccessfulAuth`); each PANEL owns only what the oracle keeps per-tab
 * (its own fields, its own mutation, its own error switch).
 *
 * A panel therefore never navigates. `magic-link/verify` and `otp/verify` hand
 * back the same `AuthResponse` shape as `login`, so a panel reports its result UP
 * and the shell's one `authDispositionOf` (`../auth-result`) decides where the
 * user goes. Three panels re-deriving that branch table would be three chances to
 * disagree about where a half-authenticated user lands.
 */

/** The oracle's `LoginTab` enum, and the shell's `_activeTab`. */
export type LoginTab = "password" | "magic-link" | "otp";

/** What every tab panel is handed. Panels take these and add nothing to them. */
export interface LoginPanelProps {
  /**
   * Report an auth response — the RAW, untyped body from `auth.login` /
   * `auth.verifyMagicLink` / `auth.verifyOtp`. `unknown` deliberately: the
   * narrowing is the shell's, so no panel can invent a laxer one.
   *
   * The shell owns everything after this: the MFA branches, the grace banner, the
   * open-redirect guard and the ticket exchange.
   */
  readonly onAuthResult: (body: unknown) => void;
  /**
   * Set (or clear, with `null`) the ONE error banner all three tabs share — the
   * oracle's `_errorMessage`. Copy is the panel's, because the oracle has a
   * different error switch per tab; the banner is the shell's, because there is
   * only one of it.
   */
  readonly onError: (message: string | null) => void;
}
