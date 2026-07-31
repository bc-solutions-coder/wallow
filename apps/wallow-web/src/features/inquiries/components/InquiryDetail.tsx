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
 * `inquiry-comments-table` + `inquiry-comment-item`,
 * `inquiry-comments-loading` / `inquiry-comments-empty` / `inquiry-comments-error`,
 * `inquiry-comment-content` + `inquiry-comment-internal` + `inquiry-comment-submit`,
 * `inquiry-comment-error`.
 *
 * `inquiry-comments-error` (the comment thread's READ failing) is deliberately
 * distinct from `inquiry-comment-error` (the add-comment MUTATION failing) —
 * they can co-render, so a spec asserting one must be able to say which failure
 * it saw.
 */
import { AppForm, FormError, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import { useMutation, useQuery, useQueryClient, type QueryClient } from "@bc-solutions-coder/query";
import type { InquiryCommentResponse, WallowSdk } from "@bc-solutions-coder/sdk";
import {
  Badge,
  Button,
  Card,
  ErrorBanner,
  ListCard,
  ListRow,
  MutedText,
  Text,
} from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import { useState } from "react";
import { z } from "zod";

import { SelectControl, type SelectControlOption } from "@shared/components/SelectControl";
import { errorText } from "@shared/lib/error-text";
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

/**
 * The comment thread is a sub-list INSIDE the detail card, not a page-level list
 * card, so it overrides the catalog recipe down to a flat, tighter frame with a
 * tighter, left-aligned cell. Both are caller `className`s that tailwind-merge
 * collapses against the recipe — the alternative would be a second catalog
 * recipe for a shape one screen uses.
 */
const SUB_LIST_SURFACE = "rounded-md shadow-none";

/** The sub-list's row cell — packed and left-aligned, not spread apart. */
const SUB_LIST_ROW = "justify-start gap-3 px-4 py-3";

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
          className="inline-block text-sm text-muted-foreground hover:text-foreground no-underline mb-4"
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
        className="inline-block text-sm text-muted-foreground hover:text-foreground no-underline mb-4"
      >
        Back to inquiries
      </a>
      <Text as="h1" variant="title" data-testid="inquiry-detail-heading">
        {inquiry.name}
      </Text>
      <MutedText data-testid="inquiry-detail-email">{inquiry.email}</MutedText>
      <Badge data-testid="inquiry-detail-status">{inquiry.status}</Badge>

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
        label="Status"
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

/**
 * A single comment row (author + content, flagged when internal). `ListRow`
 * derives its test id from `name` as `inquiry-comment-item`.
 */
function CommentRow(props: { comment: InquiryCommentResponse }) {
  const { comment } = props;
  return (
    <ListRow name="inquiry-comment" className={SUB_LIST_ROW}>
      <Text
        as="span"
        variant="caption"
        color="muted"
        weight="medium"
        data-testid="inquiry-comment-author"
      >
        {comment.authorName}
      </Text>
      <Text as="span" variant="bodySm" color="onCard" data-testid="inquiry-comment-body">
        {comment.content}
      </Text>
      {comment.isInternal ? (
        <Badge data-testid="inquiry-comment-internal-flag">(internal)</Badge>
      ) : null}
    </ListRow>
  );
}

/** The comment thread: loading / errored / empty / row-list states. */
function CommentThread(props: { inquiryId: string }) {
  const { inquiryId } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const { data, isPending, isError, error } = useQuery(
    inquiriesGetCommentsOptions({ client: sdk.client, path: { id: inquiryId } }),
  );

  if (isPending) {
    return <MutedText data-testid="inquiry-comments-loading">Loading comments…</MutedText>;
  }

  // A failed read is not an empty thread: without this the `data ?? []` below
  // would render "No comments yet." over a server error. Cached comments still
  // win over a failed background refetch.
  if (isError && data === undefined) {
    return (
      <ErrorBanner data-testid="inquiry-comments-error">
        {errorText(error, "Could not load the comments.")}
      </ErrorBanner>
    );
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
    <ListCard name="inquiry-comments" className={SUB_LIST_SURFACE}>
      {comments.map((comment) => (
        <CommentRow key={comment.id} comment={comment} />
      ))}
    </ListCard>
  );
}

/**
 * The required-comment rule. Before the migration this form had NO validation at
 * all: an empty submit POSTed `{ content: "" }` and paid a round trip to learn
 * what the form already knew.
 *
 * `.trim()` is what makes `"   "` fail the `min(1)`. It does NOT trim the
 * submitted value: TanStack's standard-schema adapter reads only the issue list
 * off a validation result and discards the parsed output, so `form.state.values`
 * stays raw — which is also what the pre-migration form posted, so the two
 * bodies `InquiryDetail.test.tsx` pins are unchanged.
 */
const addCommentSchema = z.object({
  content: z.string().trim().min(1, "Comment is required"),
  isInternal: z.boolean(),
});

/**
 * Add-comment form (migrated to `@bc-solutions-coder/forms` in Wallow-lrlm.5.5) —
 * one `useAppForm` call holding the zod schema, the GENERATED
 * `inquiriesAddCommentMutation({ client })` and the success work, rendered
 * through the shared `AppForm` shell (see `CreateOrganizationForm`, the canonical
 * template).
 *
 * `inquiry-comment-form`, `inquiry-comment-content`, `inquiry-comment-error` and
 * `inquiry-comment-submit` all DERIVE from the shell's `testIdPrefix`. The
 * internal flag does not: `isInternal` would derive `inquiry-comment-is-internal`,
 * but `InquiryDetail.test.tsx` clicks — and `InquiryDetail.catalog.test.tsx`
 * inspects — `inquiry-comment-internal`, so it carries an explicit `testId`.
 *
 * The flag also gains a VISIBLE label: `CheckboxField` requires one and renders
 * it beside the box. It reuses the box's existing `aria-label` copy verbatim,
 * "Internal note", so the accessible name is unchanged — a migration adds
 * chrome, it never rewrites copy.
 *
 * ERROR SURFACE. The sibling `ErrorBanner` that named nothing becomes the RFC
 * 7807 split: per-property `errors` render through the field's own message
 * (`aria-describedby`'d and `aria-invalid`'d onto the control), and only what is
 * left over reaches the banner — which keeps the id `inquiry-comment-error` and,
 * for a failure carrying no message of its own, the sentence it always had.
 */
function AddCommentForm(props: {
  client: WallowSdk["client"];
  queryClient: QueryClient;
  inquiryId: string;
}) {
  const { client, queryClient, inquiryId } = props;

  const form = useAppForm({
    schema: addCommentSchema,
    defaultValues: { content: "", isInternal: false },
    // The generated factory goes over WHOLE — `useAppForm` infers its `TError`,
    // so nothing here has to be destructured or cast.
    mutation: inquiriesAddCommentMutation({ client }),
    // The operation carries a path parameter, so the default `{ body: values }`
    // would post to the wrong URL. The body itself is the values verbatim.
    toVariables: (values) => ({
      path: { id: inquiryId },
      body: { content: values.content, isInternal: values.isInternal },
    }),
    onSuccess: () => {
      // Generated keys are flat, so there is no `['comments']` prefix to
      // invalidate by — the comments OPERATION predicate is the sweep.
      void queryClient.invalidateQueries(
        queriesForOperation(inquiriesGetCommentsQueryKey({ client, path: { id: inquiryId } })),
      );
      // TanStack's own `reset` (the form's values), NOT `form.wallow.reset` (the
      // mutation's result state). Leaving the flag stuck on is how the NEXT
      // comment gets posted internal by accident. Closing over `form` is safe —
      // `onSuccess` only ever runs after a render.
      form.reset();
    },
    fallbackError: "Could not add the comment.",
  });

  return (
    // `space-y-3`, not the shell's default `space-y-5` — this form's rhythm is
    // pinned by `InquiryDetail.restyle.test.tsx`.
    <AppForm form={form} testIdPrefix="inquiry-comment" className="space-y-3">
      <form.AppField name="content">
        {(field) => <field.TextareaField label="Comment" />}
      </form.AppField>

      <form.AppField name="isInternal">
        {(field) => <field.CheckboxField label="Internal note" testId="inquiry-comment-internal" />}
      </form.AppField>

      <FormError />

      <SubmitButton>Add comment</SubmitButton>
    </AppForm>
  );
}
