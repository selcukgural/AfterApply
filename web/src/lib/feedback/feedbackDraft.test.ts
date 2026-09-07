import { describe, expect, it } from "vitest";
import {
  buildFeedbackRequest,
  FEEDBACK_MESSAGE_MAX_LENGTH,
  normalizePagePath,
  validateFeedbackDraft,
  type FeedbackDraft,
} from "./feedbackDraft";

const draft = (overrides: Partial<FeedbackDraft> = {}): FeedbackDraft => ({
  category: "Bug",
  mood: null,
  message: "The status filter forgets my choice.",
  replyEmail: "",
  ...overrides,
});

describe("validateFeedbackDraft", () => {
  it("accepts a filled-in panel", () => {
    expect(validateFeedbackDraft(draft())).toBeNull();
  });

  it("rejects whitespace as a message", () => {
    expect(validateFeedbackDraft(draft({ message: "   \n  " }))).toBe("messageRequired");
  });

  it("measures the trimmed message against the limit", () => {
    const padded = `  ${"a".repeat(FEEDBACK_MESSAGE_MAX_LENGTH)}  `;

    expect(validateFeedbackDraft(draft({ message: padded }))).toBeNull();
    expect(validateFeedbackDraft(draft({ message: "a".repeat(FEEDBACK_MESSAGE_MAX_LENGTH + 1) }))).toBe("messageTooLong");
  });

  it("treats a blank reply address as not given rather than invalid", () => {
    expect(validateFeedbackDraft(draft({ replyEmail: "   " }))).toBeNull();
  });

  it("catches an obviously wrong reply address before the round trip", () => {
    expect(validateFeedbackDraft(draft({ replyEmail: "someone@example" }))).toBe("replyEmailInvalid");
    expect(validateFeedbackDraft(draft({ replyEmail: "someone@example.com" }))).toBeNull();
  });
});

describe("normalizePagePath", () => {
  it("keeps the path", () => {
    expect(normalizePagePath("/tr/applications")).toBe("/tr/applications");
  });

  // A query string on an application screen can carry ids the user never meant to attach to a
  // sentence about a button being hard to find.
  it("drops the query string and the fragment", () => {
    expect(normalizePagePath("/tr/applications?status=Applied&page=2")).toBe("/tr/applications");
    expect(normalizePagePath("/tr/applications#timeline")).toBe("/tr/applications");
  });

  it("refuses anything that is not a path", () => {
    expect(normalizePagePath("https://ekariyerim.com/tr/applications")).toBeNull();
    expect(normalizePagePath("")).toBeNull();
  });
});

describe("buildFeedbackRequest", () => {
  const context = { pathname: "/tr/applications?status=Applied", locale: "tr", theme: "dark" };

  it("sends omitted optional fields as null, not empty strings", () => {
    const request = buildFeedbackRequest(draft({ replyEmail: "  " }), context);

    expect(request.replyEmail).toBeNull();
    expect(request.mood).toBeNull();
  });

  it("trims the message and cleans the page path", () => {
    const request = buildFeedbackRequest(draft({ message: "  hello  ", mood: "Good", replyEmail: " me@example.com " }), context);

    expect(request).toEqual({
      category: "Bug",
      message: "hello",
      mood: "Good",
      replyEmail: "me@example.com",
      pagePath: "/tr/applications",
      locale: "tr",
      theme: "dark",
    });
  });
});
