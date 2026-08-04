import { Button, Card, Text } from "@bc-solutions-coder/ui";
import type { ReactNode } from "react";
import { toAppHref } from "@shared/lib/base-path";

/**
 * Where the authorize endpoint sends someone whose join request is awaiting review.
 *
 * This is not an error, so it does not render through the error screen: the request was
 * accepted and the pending membership recorded. What the person needs is the state of that
 * request and the two things they can do — wait, or come back as somebody who already has
 * access. The organization name is not shown because the authorize redirect carries no
 * attacker-proof way to supply one.
 */

function Heading() {
  return (
    <Text as="h2" variant="subheading" color="onCard" data-testid="access-request-heading">
      Request sent
    </Text>
  );
}

function Explanation() {
  return (
    <Text as="p" color="onCard" data-testid="access-request-message">
      Your request to join is waiting for an administrator to review it. You&apos;ll get an email
      once someone decides.
    </Text>
  );
}

/**
 * The same escape hatch the error screen's membership refusals offer: the visitor is signed in
 * as somebody without access here, so a home link alone would loop them back through authorize
 * and land them right here again.
 */
function Footer() {
  return (
    <div className="flex flex-col items-center gap-2 w-full">
      <Button
        render={<a href={toAppHref("/logout")} />}
        nativeButton={false}
        variant="link"
        data-testid="access-request-sign-out-link"
      >
        Sign out and try a different account
      </Button>
      <a
        href={toAppHref("/")}
        data-testid="access-request-back-link"
        className="text-sm text-muted-foreground hover:text-foreground"
      >
        Back to home
      </a>
    </div>
  );
}

export function AccessRequestPage(): ReactNode {
  return (
    <Card>
      <Heading />
      <Explanation />
      <Footer />
    </Card>
  );
}
