/**
 * Inquiry detail (Wallow-8w1h.7.4). Drives
 * `useQuery(inquiriesGetByIdOptions(...))` +
 * `useQuery(inquiriesGetCommentsOptions(...))` and renders the inquiry heading +
 * fields, current status, a status-change control, and the comment thread +
 * add-comment form. Mirrors the canonical Organizations `OrganizationDetail` +
 * `MemberList` shape.
 *
 * The back link is a plain anchor (not a router `Link`) so it needs no matched
 * route of its own; the SDK still comes off the router context.
 *
 * Testids ({page}-{element} kebab-case, invented per the scout's 7.4
 * reconciliation — the C# `InquiryPage` page object only covers the public
 * submit form, so there is no oracle for the detail/comments/status flow):
 * `inquiry-detail-heading`, `inquiry-detail-back-link`, `inquiry-detail-not-found`,
 * `inquiry-detail-error`, `inquiry-detail-status`, `inquiry-status-select` +
 * `inquiry-status-submit` + `inquiry-status-error`,
 * `inquiry-comments-table` + `inquiry-comment-row`,
 * `inquiry-comments-loading` / `inquiry-comments-empty`, `inquiry-comment-content` +
 * `inquiry-comment-internal` + `inquiry-comment-submit`, `inquiry-comment-error`.
 */
import type { InquiryCommentResponse, WallowSdk } from "@bc-solutions-coder/sdk";
import { Button, Card, Checkbox, ErrorBanner, MutedText } from "@bc-solutions-coder/ui";
import { useMutation, useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";
import { useRouteContext } from "@tanstack/react-router";
import { useState } from "react";

import { SelectControl, type SelectControlOption } from "../../../components/SelectControl";
import { errorText } from "../../../lib/error-text";
import {
  inquiriesAddCommentMutation,
  inquiriesGetByIdOptions,
  inquiriesGetByIdQueryKey,
  inquiriesGetCommentsOptions,
  inquiriesGetCommentsQueryKey,
  inquiriesUpdateStatusMutation,
  queriesForOperation,
} from "../api";
import { INQUIRY_STATUSES } from "../statuses";

/** The status/marker pill shared with the inquiries list rows. */
const CHIP =
  "inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full";

/** The `ui` `Input` recipe, applied to the bare `textarea` control. */
const CONTROL =
  "w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring";

/**
 * The domain's statuses as catalog-`Select` options. Value and label are the
 * same string here — the status IS the display text — so no mapping is lost.
 */
const STATUS_OPTIONS: readonly SelectControlOption[] = INQUIRY_STATUSES.map((status: string) => ({
  value: status,
  label: status,
}));

export function InquiryDetail(props: { inquiryId: string }) {
  const { inquiryId } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const detailQuery = useQuery(
    inquiriesGetByIdOptions({ client: sdk.client, path: { id: inquiryId } }),
  );

  if (detailQuery.isPending) {
    return <MutedText data-testid="inquiry-detail-loading">Loading inquiry…</MutedText>;
  }

  // React Query retains the last resolved data across a failed background
  // refetch, so a genuine error is only meaningful when there is NO data to fall
  // back to — which is what distinguishes errored from resolved-null.
  const inquiry = detailQuery.data ?? null;

  if (inquiry === null) {
    if (detailQuery.isError) {
      return (
        <ErrorBanner data-testid="inquiry-detail-error">
          {errorText(detailQuery.error, "Could not load the inquiry.")}
        </ErrorBanner>
      );
    }

    return (
      <Card spacing="p-8 space-y-6" className="shadow-sm">
        <a
          href="/dashboard/inquiries"
          data-testid="inquiry-detail-back-link"
          className="inline-block text-sm text-foreground/60 hover:text-foreground no-underline mb-4"
        >
          Back to inquiries
        </a>
        <MutedText data-testid="inquiry-detail-not-found">Inquiry not found.</MutedText>
      </Card>
    );
  }

  return (
    <Card data-testid="inquiry-detail-card" spacing="p-8 space-y-6" className="shadow-sm">
      <a
        href="/dashboard/inquiries"
        data-testid="inquiry-detail-back-link"
        className="inline-block text-sm text-foreground/60 hover:text-foreground no-underline mb-4"
      >
        Back to inquiries
      </a>
      <h1 data-testid="inquiry-detail-heading" className="text-3xl font-bold text-foreground">
        {inquiry.name}
      </h1>
      <div data-testid="inquiry-detail-email" className="text-sm text-foreground/60">
        {inquiry.email}
      </div>
      <div data-testid="inquiry-detail-status" className={CHIP}>
        {inquiry.status}
      </div>

      <StatusControl
        client={sdk.client}
        queryClient={queryClient}
        inquiryId={inquiryId}
        currentStatus={inquiry.status}
      />
      <CommentThread inquiryId={inquiryId} />
      <AddCommentForm client={sdk.client} queryClient={queryClient} inquiryId={inquiryId} />
    </Card>
  );
}

/** Status-change control: pick a status and PUT it on the inquiry. */
function StatusControl(props: {
  client: WallowSdk["client"];
  queryClient: QueryClient;
  inquiryId: string;
  currentStatus: string;
}) {
  const { client, queryClient, inquiryId, currentStatus } = props;
  const mutation = useMutation({
    ...inquiriesUpdateStatusMutation({ client }),
    onSuccess: (): void => {
      void queryClient.invalidateQueries(
        queriesForOperation(inquiriesGetByIdQueryKey({ client, path: { id: inquiryId } })),
      );
    },
  });
  const [status, setStatus] = useState<string>(currentStatus);

  return (
    <>
      <SelectControl
        testId="inquiry-status-select"
        value={status}
        options={STATUS_OPTIONS}
        onChange={setStatus}
      />
      <Button
        type="button"
        data-testid="inquiry-status-submit"
        onClick={() => {
          mutation.mutate({ path: { id: inquiryId }, body: { newStatus: status } });
        }}
      >
        Update status
      </Button>
      {mutation.isError ? (
        <ErrorBanner data-testid="inquiry-status-error">
          {errorText(mutation.error, "Could not update the status.")}
        </ErrorBanner>
      ) : null}
    </>
  );
}

/** A single comment row (author + content, flagged when internal). */
function CommentRow(props: { comment: InquiryCommentResponse }) {
  const { comment } = props;
  return (
    <li
      data-testid="inquiry-comment-row"
      className="flex items-center gap-3 px-4 py-3 hover:bg-background/50"
    >
      <span data-testid="inquiry-comment-author" className="text-xs font-medium text-foreground/60">
        {comment.authorName}
      </span>
      <span data-testid="inquiry-comment-body" className="text-sm text-card-foreground">
        {comment.content}
      </span>
      {comment.isInternal ? (
        <span data-testid="inquiry-comment-internal-flag" className={CHIP}>
          (internal)
        </span>
      ) : null}
    </li>
  );
}

/** The comment thread: loading / empty / row-list states. */
function CommentThread(props: { inquiryId: string }) {
  const { inquiryId } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const { data, isPending } = useQuery(
    inquiriesGetCommentsOptions({ client: sdk.client, path: { id: inquiryId } }),
  );

  if (isPending) {
    return <MutedText data-testid="inquiry-comments-loading">Loading comments…</MutedText>;
  }

  const comments: readonly InquiryCommentResponse[] = data ?? [];

  if (comments.length === 0) {
    return (
      <MutedText data-testid="inquiry-comments-empty" className="text-center py-6">
        No comments yet.
      </MutedText>
    );
  }

  return (
    <ul
      data-testid="inquiry-comments-table"
      className="divide-y divide-border rounded-md border border-border overflow-hidden"
    >
      {comments.map((comment) => (
        <CommentRow key={comment.id} comment={comment} />
      ))}
    </ul>
  );
}

/**
 * The add-comment form's public/internal flag — the catalog `Checkbox`, keeping
 * the `inquiry-comment-internal` testid on the box a user actually clicks.
 *
 * The tick is `keepMounted` and hidden with `invisible` (which still reserves
 * its space) rather than left to Base UI's default unmount, so ticking the box
 * cannot reflow the row it sits in.
 */
function InternalFlag(props: { checked: boolean; onChange: (checked: boolean) => void }) {
  const { checked, onChange } = props;
  return (
    <Checkbox.Root
      data-testid="inquiry-comment-internal"
      aria-label="Internal note"
      checked={checked}
      onCheckedChange={onChange}
    >
      <Checkbox.Indicator keepMounted className="data-[unchecked]:invisible">
        ✓
      </Checkbox.Indicator>
    </Checkbox.Root>
  );
}

/** Add-comment form with a public/internal toggle. */
function AddCommentForm(props: {
  client: WallowSdk["client"];
  queryClient: QueryClient;
  inquiryId: string;
}) {
  const { client, queryClient, inquiryId } = props;
  const mutation = useMutation({
    ...inquiriesAddCommentMutation({ client }),
    onSuccess: (): void => {
      void queryClient.invalidateQueries(
        queriesForOperation(inquiriesGetCommentsQueryKey({ client, path: { id: inquiryId } })),
      );
    },
  });
  const [content, setContent] = useState("");
  const [isInternal, setIsInternal] = useState(false);

  return (
    <form
      data-testid="inquiry-comment-form"
      className="space-y-3"
      onSubmit={(e) => {
        e.preventDefault();
        e.stopPropagation();
        mutation.mutate(
          { path: { id: inquiryId }, body: { content, isInternal } },
          {
            onSuccess: () => {
              setContent("");
              setIsInternal(false);
            },
          },
        );
      }}
    >
      <textarea
        data-testid="inquiry-comment-content"
        className={CONTROL}
        value={content}
        onChange={(e) => {
          setContent(e.target.value);
        }}
      />
      <InternalFlag
        checked={isInternal}
        onChange={(next: boolean) => {
          setIsInternal(next);
        }}
      />
      {mutation.isError ? (
        <ErrorBanner data-testid="inquiry-comment-error">
          {errorText(mutation.error, "Could not add the comment.")}
        </ErrorBanner>
      ) : null}
      <Button type="submit" data-testid="inquiry-comment-submit">
        Add comment
      </Button>
    </form>
  );
}
