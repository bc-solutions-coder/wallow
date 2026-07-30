/**
 * Inquiries list component (Wallow-8w1h.7.2) — copies the CANONICAL
 * OrganizationList shape. It drives `useQuery(inquiriesGetAllOptions({ client }))`
 * and renders four states: loading, errored, empty, and a list of `inquiry-item`
 * rows, each showing the inquiry's status via `inquiry-item-status`.
 */
import { useQuery } from "@bc-solutions-coder/query";
import type { InquiryResponse } from "@bc-solutions-coder/sdk";
import { ErrorBanner, ListRow, MutedText } from "@bc-solutions-coder/ui";
import { Link, useRouteContext } from "@tanstack/react-router";

import { errorText } from "@shared/lib/error-text";
import { inquiriesGetAllOptions } from "../api";

/**
 * The row's stacked identity block — name over an optional company line. Its own
 * component because `li > div > span` would otherwise exceed the repo's JSX
 * nesting budget.
 */
function InquiryIdentity({ inquiry }: { inquiry: InquiryResponse }) {
  return (
    <div className="flex flex-col">
      <span data-testid="inquiry-item-name" className="text-sm font-medium text-card-foreground">
        {inquiry.name}
      </span>
      {inquiry.company === null ? null : (
        <span data-testid="inquiry-item-company" className="text-xs text-foreground/60">
          {inquiry.company}
        </span>
      )}
    </div>
  );
}

/**
 * A single inquiry row (extracted to keep the list's JSX nesting shallow).
 *
 * As in `OrganizationList`, `ListRow`'s `render` substitutes the `li`, so the
 * row itself is the `Link` to the inquiry's detail route.
 */
function InquiryRow({ inquiry }: { inquiry: InquiryResponse }) {
  return (
    <ListRow
      name="inquiry"
      render={<Link to="/dashboard/inquiries/$inquiryId" params={{ inquiryId: inquiry.id }} />}
    >
      <InquiryIdentity inquiry={inquiry} />
      <span
        data-testid="inquiry-item-status"
        className="inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full"
      >
        {inquiry.status}
      </span>
    </ListRow>
  );
}

/**
 * The no-inquiries card. It keeps the list's original sentence, "No inquiries
 * yet.", as the card heading rather than rewriting it — a restyle adds chrome,
 * it never drops a text node.
 */
function InquiriesEmptyState() {
  return (
    <div
      data-testid="inquiries-empty-state"
      className="bg-card rounded-lg shadow-sm border border-border p-12 text-center"
    >
      <div className="text-[80px] leading-none mb-4">🐷</div>
      <h2 className="text-xl font-semibold text-foreground mb-2">No inquiries yet.</h2>
      <p className="text-foreground/60">
        Nothing has arrived here. New inquiries show up as soon as one is submitted.
      </p>
    </div>
  );
}

export function InquiryList() {
  const { sdk } = useRouteContext({ from: "__root__" });
  const { data, isPending, isError, error } = useQuery(
    inquiriesGetAllOptions({ client: sdk.client }),
  );

  if (isPending) {
    return (
      <MutedText data-testid="inquiries-loading" className="text-center py-12">
        Loading inquiries…
      </MutedText>
    );
  }

  // The same branch the sibling `InquiryDetail` already ships: error only when
  // there is no cached list, so "we could not ask" never reads as "none yet".
  if (isError && data === undefined) {
    return (
      <ErrorBanner data-testid="inquiries-error">
        {errorText(error, "Could not load inquiries.")}
      </ErrorBanner>
    );
  }

  // No narrowing cast: under `responseStyle: "data"` the generated operation
  // resolves the body, so `data` is already `InquiryResponse[]`.
  const inquiries: readonly InquiryResponse[] = data ?? [];

  if (inquiries.length === 0) {
    return <InquiriesEmptyState />;
  }

  // A raw div, not the ui `Card`: Card's fixed `p-6 space-y-6` fights the
  // recipe's `px-6 py-4` row cells, which need to bleed to the card edge.
  return (
    <div className="bg-card rounded-lg shadow-sm border border-border overflow-hidden">
      <ul data-testid="inquiries-table" className="divide-y divide-border">
        {inquiries.map((inquiry) => (
          <InquiryRow key={inquiry.id} inquiry={inquiry} />
        ))}
      </ul>
    </div>
  );
}
