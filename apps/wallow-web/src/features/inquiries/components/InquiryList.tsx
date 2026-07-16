/**
 * Inquiries list component (Wallow-8w1h.7.2) — copies the CANONICAL
 * OrganizationList shape. It drives `useQuery(inquiriesQueries.list())` and
 * renders three states: loading, empty, and a list of `inquiry-item` rows, each
 * showing the inquiry's status via `inquiry-item-status`.
 */
import { useQuery } from "@tanstack/react-query";

import { inquiriesQueries } from "../api";
import type { Inquiry } from "../types";

export function InquiryList() {
  const { data, isPending } = useQuery(inquiriesQueries.list());

  if (isPending) {
    return <div data-testid="inquiries-loading">Loading inquiries…</div>;
  }

  // The facade returns the list as `unknown`; narrow to the feature view-model
  // at the render boundary (the sanctioned pattern OrganizationList established).
  const inquiries = (data ?? []) as Inquiry[];

  if (inquiries.length === 0) {
    return <div data-testid="inquiries-empty-state">No inquiries yet.</div>;
  }

  return (
    <ul data-testid="inquiries-table">
      {inquiries.map((inquiry) => (
        <li key={inquiry.id} data-testid="inquiry-item">
          <span>{inquiry.name}</span>
          {inquiry.company === null ? null : <span>{inquiry.company}</span>}
          <span data-testid="inquiry-item-status">{inquiry.status}</span>
        </li>
      ))}
    </ul>
  );
}
