import { Card, CardTitle, MutedText } from "@bc-solutions-coder/ui";
import type { ReactNode } from "react";
import { toAppHref } from "../../../lib/base-path";

/**
 * The Not Found screen (Wallow-ffpq.2.7).
 *
 * There is no Blazor oracle for this screen: `Wallow.Auth`'s `Routes.razor`
 * defined no `<NotFound>` fragment, so an unmatched auth URL used to answer 404
 * with whatever the framework printed — a bare "Not Found" on an otherwise
 * branded document. The shape here is therefore `ErrorPage`'s, the sibling
 * terminal screen: a card with a heading, one explanatory line, and a way out.
 *
 * The way out is `/login` rather than `/` because this is the AUTH app — `/` only
 * redirects to `/login` anyway (`routes/index.tsx`), and a user who mistyped an
 * auth URL wants the sign-in page, not another bounce.
 *
 * It takes NO props, deliberately: the requested path is attacker-constructible
 * (anyone can send a victim `/<whatever-they-want-on-screen>`), so this screen
 * never receives it and can never echo attacker-chosen text onto a Wallow-branded
 * page. Same reasoning as `ErrorPage` mapping `reason` through a `ReadonlyMap`
 * instead of printing it.
 *
 * The wiring lives in `routes/__root.tsx` as the root route's
 * `notFoundComponent`, which renders in place of the shell's `<Outlet/>` so the
 * head, theme, and `<ReadyIndicator/>` still render on a 404.
 */

/**
 * The heading. A `<CardTitle>` (an `<h2>`) rather than a `<div>`: the shell's
 * `<FocusOnNavigate/>` moves focus to the page heading on every navigation, and
 * `AuthLayout` owns the `<h1>` above this card.
 */
function NotFoundHeading() {
  return <CardTitle data-testid="not-found-heading">Page not found</CardTitle>;
}

/** The explanatory line — a page, rather than a bare status string. */
function NotFoundMessage() {
  return (
    <MutedText data-testid="not-found-message">
      That address does not match any page here. The link may be out of date, or the address may
      have been mistyped.
    </MutedText>
  );
}

/** The one action this page can usefully offer in an auth app. */
function NotFoundFooter() {
  return (
    <div className="w-full text-center">
      <a
        href={toAppHref("/login")}
        data-testid="not-found-login-link"
        className="text-sm font-medium text-primary hover:text-primary/80"
      >
        Go to sign in
      </a>
    </div>
  );
}

export function NotFoundPage(): ReactNode {
  return (
    <Card>
      <NotFoundHeading />
      <NotFoundMessage />
      <NotFoundFooter />
    </Card>
  );
}
