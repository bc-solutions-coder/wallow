/**
 * Inquiry detail (Wallow-8w1h.7.4). Drives
 * `useQuery(inquiriesQueries.detail(inquiryId))` +
 * `useQuery(inquiriesQueries.comments(inquiryId))` and renders the inquiry
 * heading + fields, current status, a status-change control
 * (`setStatusMutation`), and the comment thread + add-comment form
 * (`addCommentMutation`). Mirrors the canonical Organizations
 * `OrganizationDetail` + `MemberList` shape.
 *
 * The back link is a plain anchor (not a router `Link`) so the component renders
 * standalone under a `QueryClientProvider` without a router context.
 *
 * Testids ({page}-{element} kebab-case, invented per the scout's 7.4
 * reconciliation — the C# `InquiryPage` page object only covers the public
 * submit form, so there is no Blazor oracle for the detail/comments/status flow):
 * `inquiry-detail-heading`, `inquiry-detail-back-link`, `inquiry-detail-not-found`,
 * `inquiry-detail-error`, `inquiry-detail-status`, `inquiry-status-select` +
 * `inquiry-status-submit` + `inquiry-status-error`,
 * `inquiry-comments-table` + `inquiry-comment-row`,
 * `inquiry-comments-loading` / `inquiry-comments-empty`, `inquiry-comment-content` +
 * `inquiry-comment-internal` + `inquiry-comment-submit`, `inquiry-comment-error`.
 */
import { Button, Card, ErrorBanner, MutedText } from "@bc-solutions-coder/ui";
import { useMutation, useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";
import { useState } from "react";
import type { ProblemDetails } from "@bc-solutions-coder/sdk";

import { addCommentMutation, inquiriesQueries, setStatusMutation } from "../api";
import { INQUIRY_STATUSES, type Inquiry, type InquiryComment } from "../types";

export function InquiryDetail(props: { inquiryId: string }) {
  const { inquiryId } = props;
  const queryClient = useQueryClient();
  const detailQuery = useQuery(inquiriesQueries.detail(inquiryId));

  if (detailQuery.isPending) {
    return <MutedText data-testid="inquiry-detail-loading">Loading inquiry…</MutedText>;
  }

  // The facade returns the detail as `unknown`; narrow to the feature view-model
  // at the render boundary. A missing inquiry surfaces as `null`/`undefined`.
  // React Query retains the last resolved data across a failed background
  // refetch, so a genuine error (RFC 7807 ProblemDetails) is only meaningful when
  // there is NO data to fall back to — distinguishing errored from resolved-null.
  const inquiry = (detailQuery.data ?? null) as Inquiry | null;

  if (inquiry === null) {
    if (detailQuery.isError) {
      return (
        <ErrorBanner data-testid="inquiry-detail-error">
          {(detailQuery.error as ProblemDetails).detail}
        </ErrorBanner>
      );
    }

    return (
      <Card>
        <a href="/dashboard/inquiries" data-testid="inquiry-detail-back-link">
          Back to inquiries
        </a>
        <MutedText data-testid="inquiry-detail-not-found">Inquiry not found.</MutedText>
      </Card>
    );
  }

  return (
    <Card>
      <a href="/dashboard/inquiries" data-testid="inquiry-detail-back-link">
        Back to inquiries
      </a>
      <h1 data-testid="inquiry-detail-heading">{inquiry.name}</h1>
      <div>{inquiry.email}</div>
      <div data-testid="inquiry-detail-status">{inquiry.status}</div>

      <StatusControl
        queryClient={queryClient}
        inquiryId={inquiryId}
        currentStatus={inquiry.status}
      />
      <CommentThread inquiryId={inquiryId} />
      <AddCommentForm queryClient={queryClient} inquiryId={inquiryId} />
    </Card>
  );
}

/** Status-change control: pick a status and delegate to `setStatusMutation`. */
function StatusControl(props: {
  queryClient: QueryClient;
  inquiryId: string;
  currentStatus: string;
}) {
  const { queryClient, inquiryId, currentStatus } = props;
  const mutation = useMutation(setStatusMutation(queryClient, inquiryId));
  const [status, setStatus] = useState<string>(currentStatus);

  return (
    <>
      <select
        data-testid="inquiry-status-select"
        value={status}
        onChange={(e) => {
          setStatus(e.target.value);
        }}
      >
        {INQUIRY_STATUSES.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
      <Button
        type="button"
        data-testid="inquiry-status-submit"
        onClick={() => {
          mutation.mutate(status);
        }}
      >
        Update status
      </Button>
      {mutation.isError ? (
        <ErrorBanner data-testid="inquiry-status-error">
          {(mutation.error as ProblemDetails).detail}
        </ErrorBanner>
      ) : null}
    </>
  );
}

/** A single comment row (author + content, flagged when internal). */
function CommentRow(props: { comment: InquiryComment }) {
  const { comment } = props;
  return (
    <li data-testid="inquiry-comment-row">
      <span>{comment.authorName}</span>
      <span>{comment.content}</span>
      {comment.isInternal ? <span>(internal)</span> : null}
    </li>
  );
}

/** The comment thread: loading / empty / row-list states. */
function CommentThread(props: { inquiryId: string }) {
  const { inquiryId } = props;
  const { data, isPending } = useQuery(inquiriesQueries.comments(inquiryId));

  if (isPending) {
    return <MutedText data-testid="inquiry-comments-loading">Loading comments…</MutedText>;
  }

  const comments = (data ?? []) as InquiryComment[];

  if (comments.length === 0) {
    return <MutedText data-testid="inquiry-comments-empty">No comments yet.</MutedText>;
  }

  return (
    <ul data-testid="inquiry-comments-table">
      {comments.map((comment) => (
        <CommentRow key={comment.id} comment={comment} />
      ))}
    </ul>
  );
}

/** Add-comment form with a public/internal toggle, backed by `addCommentMutation`. */
function AddCommentForm(props: { queryClient: QueryClient; inquiryId: string }) {
  const { queryClient, inquiryId } = props;
  const mutation = useMutation(addCommentMutation(queryClient, inquiryId));
  const [content, setContent] = useState("");
  const [isInternal, setIsInternal] = useState(false);

  return (
    <form
      data-testid="inquiry-comment-form"
      onSubmit={(e) => {
        e.preventDefault();
        e.stopPropagation();
        mutation.mutate(
          { content, isInternal },
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
        value={content}
        onChange={(e) => {
          setContent(e.target.value);
        }}
      />
      <input
        type="checkbox"
        data-testid="inquiry-comment-internal"
        checked={isInternal}
        onChange={(e) => {
          setIsInternal(e.target.checked);
        }}
      />
      {mutation.isError ? (
        <ErrorBanner data-testid="inquiry-comment-error">
          {(mutation.error as ProblemDetails).detail}
        </ErrorBanner>
      ) : null}
      <Button type="submit" data-testid="inquiry-comment-submit">
        Add comment
      </Button>
    </form>
  );
}
