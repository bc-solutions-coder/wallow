import { createRouter as createTanStackRouter, type AnyRouter } from "@tanstack/react-router";

import { createQueryClient } from "@bc-solutions-coder/web-shell";
import { Route as acceptTermsRoute } from "./routes/accept-terms";
import { Route as consentRoute } from "./routes/consent";
import { Route as errorRoute } from "./routes/error";
import { Route as forgotPasswordRoute } from "./routes/forgot-password";
import { Route as indexRoute } from "./routes/index";
import { Route as invitationRoute } from "./routes/invitation";
import { Route as loginRoute } from "./routes/login";
import { Route as logoutRoute } from "./routes/logout";
import { Route as mfaChallengeRoute } from "./routes/mfa/challenge";
import { Route as mfaEnrollRoute } from "./routes/mfa/enroll";
import { Route as privacyRoute } from "./routes/privacy";
import { Route as registerRoute } from "./routes/register";
import { Route as resetPasswordRoute } from "./routes/reset-password";
import { Route as rootRoute } from "./routes/__root";
import { Route as termsRoute } from "./routes/terms";
import { Route as verifyEmailConfirmRoute } from "./routes/verify-email/confirm";
import { Route as verifyEmailRoute } from "./routes/verify-email/index";

/**
 * Assembles the route tree and constructs the TanStack router that boots the
 * wallow-auth Start app (Wallow-vec7.1.4).
 *
 * The routes are authored file-route style (`createFileRoute('/')(options)`),
 * which leaves their `id`/`path`/parent unset — the file-based codegen would
 * normally fill those in. The installed Start version ships no route-tree
 * codegen (see bd memory wallow-web-tanstack-manual-route-tree), so each route
 * is bound to the root explicitly below.
 *
 * **This file is closed to the Phase 2 screen tasks (Wallow-vec7.3.1-.3.15).**
 * Wallow-vec7.3.16 pre-registered all sixteen screen routes up front precisely
 * so those fifteen tasks would stop colliding here: each would otherwise have to
 * add an import + `addChildren` entry to this one file, making them unsafe to
 * run in parallel. Every screen now owns its own file under `src/routes/` (named
 * in that file's header comment) and implements itself by replacing the
 * placeholder component there. Registering a screen route is no longer part of
 * any screen task.
 *
 * The paths below are the migration's external contract: they are copied
 * verbatim from the `@page` directives of the Blazor oracles in
 * `api/src/Wallow.Auth/Components/Pages/`, so any URL that worked against the
 * Blazor auth app lands on the same screen here. They are not free to change.
 *
 * Every route is a flat child of the root: the auth app has no layout route,
 * because `AuthLayout` is applied by each screen rather than through the tree.
 * That includes the two nested paths (`/verify-email/confirm`, `/mfa/*`), which
 * are bound at their full path rather than under a parent segment.
 */
export function createRouter(): AnyRouter {
  const indexRouteWithParent = indexRoute.update({
    id: "/",
    path: "/",
    getParentRoute: () => rootRoute,
  });

  const loginRouteWithParent = loginRoute.update({
    id: "/login",
    path: "/login",
    getParentRoute: () => rootRoute,
  });

  const registerRouteWithParent = registerRoute.update({
    id: "/register",
    path: "/register",
    getParentRoute: () => rootRoute,
  });

  const forgotPasswordRouteWithParent = forgotPasswordRoute.update({
    id: "/forgot-password",
    path: "/forgot-password",
    getParentRoute: () => rootRoute,
  });

  const resetPasswordRouteWithParent = resetPasswordRoute.update({
    id: "/reset-password",
    path: "/reset-password",
    getParentRoute: () => rootRoute,
  });

  const verifyEmailRouteWithParent = verifyEmailRoute.update({
    id: "/verify-email",
    path: "/verify-email",
    getParentRoute: () => rootRoute,
  });

  const verifyEmailConfirmRouteWithParent = verifyEmailConfirmRoute.update({
    id: "/verify-email/confirm",
    path: "/verify-email/confirm",
    getParentRoute: () => rootRoute,
  });

  const mfaChallengeRouteWithParent = mfaChallengeRoute.update({
    id: "/mfa/challenge",
    path: "/mfa/challenge",
    getParentRoute: () => rootRoute,
  });

  const mfaEnrollRouteWithParent = mfaEnrollRoute.update({
    id: "/mfa/enroll",
    path: "/mfa/enroll",
    getParentRoute: () => rootRoute,
  });

  const consentRouteWithParent = consentRoute.update({
    id: "/consent",
    path: "/consent",
    getParentRoute: () => rootRoute,
  });

  const logoutRouteWithParent = logoutRoute.update({
    id: "/logout",
    path: "/logout",
    getParentRoute: () => rootRoute,
  });

  const invitationRouteWithParent = invitationRoute.update({
    id: "/invitation",
    path: "/invitation",
    getParentRoute: () => rootRoute,
  });

  const acceptTermsRouteWithParent = acceptTermsRoute.update({
    id: "/accept-terms",
    path: "/accept-terms",
    getParentRoute: () => rootRoute,
  });

  const privacyRouteWithParent = privacyRoute.update({
    id: "/privacy",
    path: "/privacy",
    getParentRoute: () => rootRoute,
  });

  const termsRouteWithParent = termsRoute.update({
    id: "/terms",
    path: "/terms",
    getParentRoute: () => rootRoute,
  });

  const errorRouteWithParent = errorRoute.update({
    id: "/error",
    path: "/error",
    getParentRoute: () => rootRoute,
  });

  const routeTree = rootRoute.addChildren([
    indexRouteWithParent,
    loginRouteWithParent,
    registerRouteWithParent,
    forgotPasswordRouteWithParent,
    resetPasswordRouteWithParent,
    verifyEmailRouteWithParent,
    verifyEmailConfirmRouteWithParent,
    mfaChallengeRouteWithParent,
    mfaEnrollRouteWithParent,
    consentRouteWithParent,
    logoutRouteWithParent,
    invitationRouteWithParent,
    acceptTermsRouteWithParent,
    privacyRouteWithParent,
    termsRouteWithParent,
    errorRouteWithParent,
  ]);

  const queryClient = createQueryClient();

  return createTanStackRouter({ routeTree, context: { queryClient } });
}
