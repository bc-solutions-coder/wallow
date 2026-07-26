/**
 * Inquiries list component (Wallow-8w1h.7.2) — copies the CANONICAL
 * OrganizationList shape. It drives `useQuery(inquiriesQueries.list())` and
 * renders three states: loading, empty, and a list of `inquiry-item` rows, each
 * showing the inquiry's status via `inquiry-item-status`.
 */
import { Card, MutedText } from "@bc-solutions-coder/ui";
import { useQuery } from "@tanstack/react-query";

import { inquiriesQueries } from "../api";
import type { Inquiry } from "../types";

/** A single inquiry row (extracted to keep the list's JSX nesting shallow). */
function InquiryRow({ inquiry }: { inquiry: Inquiry }) {
  return (
    <li data-testid="inquiry-item">
      <span>{inquiry.name}</span>
      {inquiry.company === null ? null : <span>{inquiry.company}</span>}
      <span data-testid="inquiry-item-status">{inquiry.status}</span>
    </li>
  );
}

export function InquiryList() {
  const { data, isPending } = useQuery(inquiriesQueries.list());

  if (isPending) {
    return <MutedText data-testid="inquiries-loading">Loading inquiries…</MutedText>;
  }

  // The facade returns the list as `unknown`; narrow to the feature view-model
  // at the render boundary (the sanctioned pattern OrganizationList established).
  const inquiries = (data ?? []) as Inquiry[];

  if (inquiries.length === 0) {
    return <MutedText data-testid="inquiries-empty-state">No inquiries yet.</MutedText>;
  }

  return (
    <Card>
      <ul data-testid="inquiries-table">
        {inquiries.map((inquiry) => (
          <InquiryRow key={inquiry.id} inquiry={inquiry} />
        ))}
      </ul>
    </Card>
  );
}
