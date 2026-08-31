import type { ReactElement } from "react";

import { MutedText } from "../muted-text/muted-text";
import { Text } from "../text/text";

export interface BrandedHeaderProps {
  /** Heading text: the requesting client's display name, else the fork's app name. */
  readonly name: string;
  /** Sub-heading beneath the name; `null`/`undefined`/`""` renders nothing. */
  readonly tagline?: string | null;
  /** Logo `src` above the name; `null`/`undefined`/`""` renders nothing. */
  readonly logoUrl?: string | null;
  /**
   * The organization that owns the client, attributed as "by {organizationName}"
   * beneath the heading. Absent for the fork's own branding and for first-party
   * clients, which render no attribution line.
   */
  readonly organizationName?: string | null;
  /**
   * `page` (default) is the auth screens' header: a centred block with the
   * route-change focus target on an `h1`. `card` is an embeddable preview —
   * a fragment of parts with a `span` heading, whose wrapper (and spacing) the
   * caller owns.
   */
  readonly variant?: "page" | "card";
  /**
   * App-owned test id. The `page` variant stamps it on its own wrapper; both
   * variants derive `{id}-logo`, `{id}-name`, `{id}-tagline` and
   * `{id}-organization` for the parts — the `card` variant has no wrapper to
   * stamp, so the caller's wrapper id never collides.
   */
  readonly "data-testid"?: string;
}

/** Treat `null`, `undefined` and `""` alike: nothing to show. */
function orNull(value: string | null | undefined): string | null {
  return value === null || value === undefined || value === "" ? null : value;
}

/** A part's derived test id, or `undefined` when the header itself has none. */
function partTestId(testId: string | undefined, part: string): string | undefined {
  return testId === undefined ? undefined : `${testId}-${part}`;
}

/**
 * Logo, name, tagline and organization attribution for whoever the page is
 * branded as — the requesting OAuth client on the auth host's transaction
 * screens, the fork itself everywhere else. Branding arrives as props so this
 * package never resolves it (nor imports `@bc-solutions-coder/styles`).
 */
export function BrandedHeader({
  name,
  tagline,
  logoUrl,
  organizationName,
  variant = "page",
  "data-testid": testId,
}: BrandedHeaderProps): ReactElement {
  const page: boolean = variant === "page";
  const logo: string | null = orNull(logoUrl);
  const shownTagline: string | null = orNull(tagline);
  const organization: string | null = orNull(organizationName);

  const parts: ReactElement = (
    <>
      {logo !== null && (
        <img
          src={logo}
          alt={name}
          className={page ? "size-30 mx-auto block" : "mx-auto mb-3 block size-16 object-contain"}
          style={page ? { shapeRendering: "geometricPrecision" } : undefined}
          data-testid={partTestId(testId, "logo")}
        />
      )}
      {page ? (
        /*
         * `variant="heading"` + `weight="bold"`, not the `display` scale
         * `as="h1"` derives: a card-sized branded title, with the weight
         * override landing after the variant's `font-semibold` so
         * tailwind-merge keeps it. The heading is FocusOnNavigate's
         * route-change focus target.
         */
        <Text
          as="h1"
          variant="heading"
          weight="bold"
          data-focus-target
          tabIndex={-1}
          data-testid={partTestId(testId, "name")}
        >
          {name}
        </Text>
      ) : (
        <Text
          as="span"
          variant="heading"
          weight="bold"
          className="block"
          data-testid={partTestId(testId, "name")}
        >
          {name}
        </Text>
      )}
      {shownTagline !== null && (
        <MutedText className="mt-1" data-testid={partTestId(testId, "tagline")}>
          {shownTagline}
        </MutedText>
      )}
      {organization !== null && (
        <MutedText className="mt-1" data-testid={partTestId(testId, "organization")}>
          by {organization}
        </MutedText>
      )}
    </>
  );

  if (!page) {
    return parts;
  }

  return (
    <div className="text-center mb-8" data-testid={testId}>
      {parts}
    </div>
  );
}
