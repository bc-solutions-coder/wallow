/**
 * Inquiries list component (Wallow-8w1h.7.2) — copies the CANONICAL
 * OrganizationList shape. It drives `useQuery(inquiriesGetAllOptions({ client }))`
 * and renders four states: loading, errored, empty, and a list of `inquiry-item`
 * rows, each showing the inquiry's status via `inquiry-item-status`.
 */
import { useQuery } from "@bc-solutions-coder/query";
import type { InquiryResponse } from "@bc-solutions-coder/sdk";
import {
  Badge,
  EmptyState,
  FailureBanner,
  ListCard,
  ListRow,
  MutedText,
  Text,
} from "@bc-solutions-coder/ui";
import { Link, useRouteContext } from "@tanstack/react-router";

import { inquiriesGetAllOptions } from "../api";

/**
 * The row's stacked identity block — name over an optional company line. Its own
 * component because `li > div > span` would otherwise exceed the repo's JSX
 * nesting budget.
 */
function InquiryIdentity({ inquiry }: { inquiry: InquiryResponse }) {
  return (
    <div className="flex flex-col">
      <Text
        as="span"
        variant="bodySm"
        color="onCard"
        weight="medium"
        data-testid="inquiry-item-name"
      >
        {inquiry.name}
      </Text>
      {inquiry.company === null ? null : (
        <Text as="span" variant="caption" color="muted" data-testid="inquiry-item-company">
          {inquiry.company}
        </Text>
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
      <Badge data-testid="inquiry-item-status">{inquiry.status}</Badge>
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
    <EmptyState
      data-testid="inquiries-empty-state"
      icon="🐷"
      message="No inquiries yet."
      description="Nothing has arrived here. New inquiries show up as soon as one is submitted."
    />
  );
}

export function InquiryList() {
  const { sdk } = useRouteContext({ from: "__root__" });
  const { data, isPending, isError, error, refetch } = useQuery(
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
    return <FailureBanner data-testid="inquiries-error" error={error} onRetry={refetch} />;
  }

  // No narrowing cast: under `responseStyle: "data"` the generated operation
  // resolves the body, so `data` is already `InquiryResponse[]`.
  const inquiries: readonly InquiryResponse[] = data ?? [];

  if (inquiries.length === 0) {
    return <InquiriesEmptyState />;
  }

  // `ListCard`, not the ui `Card`: Card's fixed padding fights row cells that
  // have to bleed to the card edge. `inquiries-table` is derived from `name`.
  return (
    <ListCard name="inquiries">
      {inquiries.map((inquiry) => (
        <InquiryRow key={inquiry.id} inquiry={inquiry} />
      ))}
    </ListCard>
  );
}
