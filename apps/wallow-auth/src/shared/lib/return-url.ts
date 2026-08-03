/**
 * Where a refused `returnUrl` goes.
 *
 * REFUSE, don't sanitize (bd memory `returnurl-guard-refuse-dont-sanitize`): an
 * unsafe value routes here rather than silently falling back to "/", which would
 * swallow the open-redirect attempt and leave the user on a screen that looks as
 * though nothing was wrong.
 *
 * A bare constant rather than something the hook owns, because two of its four
 * consumers are pure functions with no React in them — `verifyEmailTarget` in
 * `RegisterForm` and `authOutcome` in the login feature's result layer — and a
 * module named `use-*` is the wrong place for them to import from.
 */
export const ERROR_HREF = "/error?reason=invalid_redirect_uri";
