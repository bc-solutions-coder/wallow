import { beforeEach, describe, expect, it, vi } from "vitest";
import type { QueryClient } from "@tanstack/react-query";

/**
 * Inquiries feature query layer (Wallow-8w1h.7.1) — copies the CANONICAL
 * Organizations `api.test.ts`. The `getWallowSdk()` facade is mocked: these tests
 * assert the query/mutation layer's KEY STABILITY and its DELEGATION to the
 * `inquiries` facade slice, not the wire.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  list: vi.fn(),
  create: vi.fn(),
  get: vi.fn(),
  comments: vi.fn(),
  addComment: vi.fn(),
  setStatus: vi.fn(),
}));

// Route/component files import only from this feature's api.ts; api.ts in turn
// imports getWallowSdk. We mock the facade module so the slice methods are spies.
vi.mock("../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    inquiries: {
      list: mocks.list,
      create: mocks.create,
      get: mocks.get,
      comments: mocks.comments,
      addComment: mocks.addComment,
      setStatus: mocks.setStatus,
    },
  }),
}));

import {
  addCommentMutation,
  createInquiryMutation,
  inquiriesQueries,
  setStatusMutation,
} from "./api";

/** Invoke a queryOptions `queryFn` while ignoring its QueryFunctionContext arg. */
async function callQueryFn(queryFn: unknown): Promise<unknown> {
  return (queryFn as () => Promise<unknown>)();
}

function fakeQueryClient(): QueryClient {
  return { invalidateQueries: vi.fn() } as unknown as QueryClient;
}

describe("inquiriesQueries", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe("list", () => {
    it("keys the list query as ['inquiries']", () => {
      expect(inquiriesQueries.list().queryKey).toEqual(["inquiries"]);
    });

    it("keeps the list queryKey stable across calls", () => {
      expect(inquiriesQueries.list().queryKey).toEqual(inquiriesQueries.list().queryKey);
    });

    it("queryFn delegates to the facade inquiries.list and returns its data", async () => {
      const inquiries = [{ id: "i1", name: "Ada" }];
      mocks.list.mockResolvedValue(inquiries);

      const result = await callQueryFn(inquiriesQueries.list().queryFn);

      expect(mocks.list).toHaveBeenCalledTimes(1);
      expect(result).toBe(inquiries);
    });
  });

  describe("detail", () => {
    it("keys the detail query as ['inquiries', id]", () => {
      expect(inquiriesQueries.detail("i1").queryKey).toEqual(["inquiries", "i1"]);
    });

    it("queryFn delegates to the facade inquiries.get with the id", async () => {
      const inquiry = { id: "i1", name: "Ada" };
      mocks.get.mockResolvedValue(inquiry);

      const result = await callQueryFn(inquiriesQueries.detail("i1").queryFn);

      expect(mocks.get).toHaveBeenCalledWith("i1");
      expect(result).toBe(inquiry);
    });
  });

  describe("comments", () => {
    it("keys the comments query as ['inquiries', id, 'comments']", () => {
      expect(inquiriesQueries.comments("i1").queryKey).toEqual(["inquiries", "i1", "comments"]);
    });

    it("queryFn delegates to the facade inquiries.comments with the id", async () => {
      const comments = [{ id: "c1", content: "hi", isInternal: false }];
      mocks.comments.mockResolvedValue(comments);

      const result = await callQueryFn(inquiriesQueries.comments("i1").queryFn);

      expect(mocks.comments).toHaveBeenCalledWith("i1");
      expect(result).toBe(comments);
    });
  });
});

describe("createInquiryMutation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  const body = {
    name: "Grace",
    email: "grace@example.com",
    phone: "555-0100",
    company: null,
    projectType: "web-app",
    budgetRange: "5k-15k",
    timeline: "asap",
    message: "Hello",
  };

  it("mutationFn delegates to inquiries.create with the body", async () => {
    const created = { id: "i2", name: "Grace" };
    mocks.create.mockResolvedValue(created);

    const mutation = createInquiryMutation(fakeQueryClient());
    const result = await mutation.mutationFn(body);

    expect(mocks.create).toHaveBeenCalledWith(body);
    expect(result).toBe(created);
  });

  it("invalidates the ['inquiries'] list query on success", () => {
    const queryClient = fakeQueryClient();

    createInquiryMutation(queryClient).onSuccess();

    expect(queryClient.invalidateQueries).toHaveBeenCalledWith({ queryKey: ["inquiries"] });
  });
});

describe("addCommentMutation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("mutationFn delegates to inquiries.addComment with the inquiry id and body", async () => {
    mocks.addComment.mockResolvedValue(undefined);
    const body = { content: "Following up", isInternal: true };

    const mutation = addCommentMutation(fakeQueryClient(), "i1");
    await mutation.mutationFn(body);

    expect(mocks.addComment).toHaveBeenCalledWith("i1", body);
  });

  it("invalidates the ['inquiries', id, 'comments'] query on success", () => {
    const queryClient = fakeQueryClient();

    addCommentMutation(queryClient, "i1").onSuccess();

    expect(queryClient.invalidateQueries).toHaveBeenCalledWith({
      queryKey: ["inquiries", "i1", "comments"],
    });
  });
});

describe("setStatusMutation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("mutationFn delegates to inquiries.setStatus with the inquiry id and new status", async () => {
    const updated = { id: "i1", status: "Reviewed" };
    mocks.setStatus.mockResolvedValue(updated);

    const mutation = setStatusMutation(fakeQueryClient(), "i1");
    const result = await mutation.mutationFn("Reviewed");

    expect(mocks.setStatus).toHaveBeenCalledWith("i1", "Reviewed");
    expect(result).toBe(updated);
  });

  it("invalidates the ['inquiries', id] detail query on success", () => {
    const queryClient = fakeQueryClient();

    setStatusMutation(queryClient, "i1").onSuccess();

    expect(queryClient.invalidateQueries).toHaveBeenCalledWith({ queryKey: ["inquiries", "i1"] });
  });
});
