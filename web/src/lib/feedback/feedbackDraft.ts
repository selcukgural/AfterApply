import type { FeedbackCategory, FeedbackMood, SubmitFeedbackRequest } from "@/types/api";

/** Matches SubmitFeedbackRequestValidator.MaxMessageLength and the FeedbackEntries column. */
export const FEEDBACK_MESSAGE_MAX_LENGTH = 1000;

/** Matches the PagePath column; a locale-prefixed app path is nowhere near this. */
const MAX_PAGE_PATH_LENGTH = 200;

export const FEEDBACK_CATEGORIES: readonly FeedbackCategory[] = ["Bug", "Idea", "Question"];
export const FEEDBACK_MOODS: readonly FeedbackMood[] = ["Struggling", "Okay", "Good"];

export interface FeedbackDraft {
  category: FeedbackCategory;
  mood: FeedbackMood | null;
  message: string;
  replyEmail: string;
}

/** Translation keys under `feedback.problems`, so a failure names itself in the user's language. */
export type FeedbackDraftProblem = "messageRequired" | "messageTooLong" | "replyEmailInvalid";

export interface FeedbackPageContext {
  pathname: string;
  locale: string;
  theme: string;
}

// Deliberately loose: the server's EmailAddress() rule is the one that decides. This exists to
// catch the obvious typo before a round trip, not to re-implement RFC 5322 in the browser.
const EMAIL_SHAPE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function validateFeedbackDraft(draft: FeedbackDraft): FeedbackDraftProblem | null {
  const message = draft.message.trim();
  if (message.length === 0) {
    return "messageRequired";
  }
  if (message.length > FEEDBACK_MESSAGE_MAX_LENGTH) {
    return "messageTooLong";
  }

  const replyEmail = draft.replyEmail.trim();
  if (replyEmail.length > 0 && !EMAIL_SHAPE.test(replyEmail)) {
    return "replyEmailInvalid";
  }

  return null;
}

/**
 * The path the panel was opened from, cleaned the same way the server cleans it: no query string
 * and no fragment, because those can carry application ids the user never meant to attach to a
 * sentence about a button being hard to find.
 */
export function normalizePagePath(pathname: string): string | null {
  const withoutQuery = pathname.split(/[?#]/)[0]?.trim() ?? "";
  if (!withoutQuery.startsWith("/")) {
    return null;
  }
  return withoutQuery.slice(0, MAX_PAGE_PATH_LENGTH);
}

/** Empty optional fields are sent as null rather than "" — the column means "not given". */
export function buildFeedbackRequest(draft: FeedbackDraft, context: FeedbackPageContext): SubmitFeedbackRequest {
  const replyEmail = draft.replyEmail.trim();

  return {
    category: draft.category,
    message: draft.message.trim(),
    mood: draft.mood,
    replyEmail: replyEmail.length > 0 ? replyEmail : null,
    pagePath: normalizePagePath(context.pathname),
    locale: context.locale,
    theme: context.theme,
  };
}
