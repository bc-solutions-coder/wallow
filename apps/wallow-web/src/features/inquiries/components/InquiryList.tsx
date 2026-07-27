/**
 * Inquiries list component (Wallow-8w1h.7.2) — copies the CANONICAL
 * OrganizationList shape. It drives `useQuery(inquiriesQueries.list())` and
 * renders three states: loading, empty, and a list of `inquiry-item` rows, each
 * showing the inquiry's status via `inquiry-item-status`.
 */
import { MutedText } from "@bc-solutions-coder/ui";
import { useQuery } from "@tanstack/react-query";

import { inquiriesQueries } from "../api";
import type { Inquiry } from "../types";

/**
 * The row's stacked identity block — name over an optional company line. Its own
 * component because `li > div > span` would otherwise exceed the repo's JSX
 * nesting budget.
 */
function InquiryIdentity({ inquiry }: { inquiry: Inquiry }) {
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

/** A single inquiry row (extracted to keep the list's JSX nesting shallow). */
function InquiryRow({ inquiry }: { inquiry: Inquiry }) {
  return (
    <li
      data-testid="inquiry-item"
      className="flex items-center justify-between px-6 py-4 hover:bg-background/50"
    >
      <InquiryIdentity inquiry={inquiry} />
      <span
        data-testid="inquiry-item-status"
        className="inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full"
      >
        {inquiry.status}
      </span>
    </li>
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
  const { data, isPending } = useQuery(inquiriesQueries.list());

  if (isPending) {
    return (
      <MutedText data-testid="inquiries-loading" className="text-center py-12">
        Loading inquiries…
      </MutedText>
    );
  }

  // The facade returns the list as `unknown`; narrow to the feature view-model
  // at the render boundary (the sanctioned pattern OrganizationList established).
  const inquiries = (data ?? []) as Inquiry[];

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
