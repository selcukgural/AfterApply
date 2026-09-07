"use client";

import { useEffect, useRef, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { usePathname } from "@/i18n/navigation";
import type { FeedbackCategory, FeedbackMood } from "@/types/api";
import { feedbackApi } from "@/lib/api/feedback";
import { ApiError } from "@/lib/api/httpClient";
import {
  buildFeedbackRequest,
  FEEDBACK_CATEGORIES,
  FEEDBACK_MESSAGE_MAX_LENGTH,
  FEEDBACK_MOODS,
  validateFeedbackDraft,
  type FeedbackDraft,
  type FeedbackDraftProblem,
} from "@/lib/feedback/feedbackDraft";

const EMPTY_DRAFT: FeedbackDraft = { category: "Bug", mood: null, message: "", replyEmail: "" };

/** How long the thank-you stays up before the panel closes itself. Long enough to still be there
 *  when someone's eyes come back from the button they just clicked. */
const SUCCESS_DISMISS_MS = 3500;

function ChatIcon({ className }: { className: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth={1.8} aria-hidden="true">
      <path strokeLinecap="round" strokeLinejoin="round" d="M20 14.5A2.5 2.5 0 0117.5 17H8l-4 3.5v-14A2.5 2.5 0 016.5 4h11A2.5 2.5 0 0120 6.5z" />
    </svg>
  );
}

function CategoryIcon({ category, className }: { category: FeedbackCategory; className: string }) {
  const paths: Record<FeedbackCategory, React.ReactNode> = {
    Bug: (
      <>
        <path strokeLinecap="round" d="M12 8.5v4M12 16v.4" />
        <path strokeLinecap="round" strokeLinejoin="round" d="M10.3 4.4L3 17.5A2 2 0 004.7 20.5h14.6a2 2 0 001.7-3L13.7 4.4a2 2 0 00-3.4 0z" />
      </>
    ),
    Idea: (
      <>
        <path strokeLinecap="round" d="M9.5 18h5M10 21h4" />
        <path strokeLinecap="round" strokeLinejoin="round" d="M12 3a6 6 0 013.6 10.8c-.6.5-.9 1.1-1 1.7H9.4c-.1-.6-.4-1.2-1-1.7A6 6 0 0112 3z" />
      </>
    ),
    Question: (
      <>
        <circle cx="12" cy="12" r="9" />
        <path strokeLinecap="round" d="M9.8 9.6a2.3 2.3 0 113 2.2v1.4M12.8 16.4v.4" />
      </>
    ),
  };

  return (
    <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth={1.7} aria-hidden="true">
      {paths[category]}
    </svg>
  );
}

function MoodIcon({ mood, className }: { mood: FeedbackMood; className: string }) {
  const mouths: Record<FeedbackMood, string> = {
    Struggling: "M8.5 15.5c1-1.4 5.5-1.4 7 0",
    Okay: "M8.5 15h7",
    Good: "M8 14c1.2 1.8 6.8 1.8 8 0",
  };

  return (
    <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth={1.6} aria-hidden="true">
      <circle cx="12" cy="12" r="9" />
      <path strokeLinecap="round" d={mouths[mood]} />
      <circle cx="9" cy="10" r="0.9" fill="currentColor" stroke="none" />
      <circle cx="15" cy="10" r="0.9" fill="currentColor" stroke="none" />
    </svg>
  );
}

const SELECTED_CLASSES = "border-accent bg-accent-wash text-accent-ink font-medium";
const UNSELECTED_CLASSES =
  "border-gray-300 text-gray-600 hover:border-gray-400 hover:text-gray-900 dark:border-gray-700 dark:text-gray-400 dark:hover:border-gray-600 dark:hover:text-gray-100";

/**
 * The floating feedback launcher, mounted once for the whole signed-in app (see the protected
 * layout). It lives on every screen on purpose: the panel reads the path it was opened from and
 * sends it along, so a user who is stuck somewhere never has to describe where "somewhere" was.
 *
 * Everything it attaches beyond the message — path, locale, theme — is named in the panel before
 * the user sends. Nothing about their applications, CVs or email is collected here.
 */
export function FeedbackWidget() {
  const t = useTranslations("feedback");
  const locale = useLocale();
  const pathname = usePathname();

  const [isOpen, setIsOpen] = useState(false);
  const [draft, setDraft] = useState<FeedbackDraft>(EMPTY_DRAFT);
  const [problem, setProblem] = useState<FeedbackDraftProblem | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isSent, setIsSent] = useState(false);

  const launcherRef = useRef<HTMLButtonElement>(null);
  const messageRef = useRef<HTMLTextAreaElement>(null);

  const submitMutation = useMutation({
    mutationFn: () =>
      feedbackApi.submit(
        buildFeedbackRequest(draft, {
          pathname: `/${locale}${pathname}`,
          locale,
          // Read at send time rather than tracked in state: the theme switcher is a plain DOM
          // side effect (see lib/theme), so the class on <html> is the only source of truth.
          theme: document.documentElement.classList.contains("dark") ? "dark" : "light",
        }),
      ),
    onSuccess: () => {
      setIsSent(true);
      setDraft(EMPTY_DRAFT);
    },
    onError: (error) => {
      // apiFetch already surfaces the backend's localized detail/errors; this is the
      // network-failure fallback.
      setErrorMessage(error instanceof ApiError ? error.message : t("errors.generic"));
    },
  });

  // Escape closes from anywhere inside the panel, and focus goes back to the button that opened
  // it — otherwise a keyboard user is dropped at the top of the document.
  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsOpen(false);
        launcherRef.current?.focus();
      }
    };

    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [isOpen]);

  useEffect(() => {
    if (isOpen && !isSent) {
      messageRef.current?.focus();
    }
  }, [isOpen, isSent]);

  useEffect(() => {
    if (!isSent) {
      return;
    }

    const timer = window.setTimeout(() => {
      setIsOpen(false);
      setIsSent(false);
    }, SUCCESS_DISMISS_MS);

    return () => window.clearTimeout(timer);
  }, [isSent]);

  const openPanel = () => {
    setProblem(null);
    setErrorMessage(null);
    setIsSent(false);
    setIsOpen(true);
  };

  const closePanel = () => {
    setIsOpen(false);
    launcherRef.current?.focus();
  };

  const handleSubmit = (event: React.FormEvent) => {
    event.preventDefault();
    setErrorMessage(null);

    const draftProblem = validateFeedbackDraft(draft);
    setProblem(draftProblem);
    if (draftProblem) {
      messageRef.current?.focus();
      return;
    }

    submitMutation.mutate();
  };

  const characterCount = draft.message.trim().length;

  return (
    <>
      {isOpen && (
        <div
          role="dialog"
          aria-label={t("title")}
          // Height is capped against the viewport, not left to the content: at 22rem wide the
          // filled-in panel runs past 550px, which overflows the top of a short window (a
          // laptop with the browser half-height, a phone in landscape). The header and the
          // send button stay put; only the fields scroll.
          className="fixed bottom-20 right-4 z-40 flex max-h-[calc(100dvh-7rem)] w-[min(22rem,calc(100vw-2rem))] flex-col overflow-hidden rounded-xl border border-gray-200 bg-white shadow-2xl dark:border-gray-800 dark:bg-gray-900"
        >
          <div className="flex items-start justify-between gap-3 border-b border-gray-100 px-4 py-3 dark:border-gray-800">
            <div>
              <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
              <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">{t("subtitle")}</p>
            </div>
            <button
              type="button"
              onClick={closePanel}
              aria-label={t("close")}
              className="-mr-1 rounded-md p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-700 dark:hover:bg-gray-800 dark:hover:text-gray-200"
            >
              <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
                <path strokeLinecap="round" d="M6 6l12 12M18 6L6 18" />
              </svg>
            </button>
          </div>

          {isSent ? (
            <div className="flex flex-col items-center gap-2 px-4 py-8 text-center">
              <span className="flex h-10 w-10 items-center justify-center rounded-full bg-good-wash text-good-ink">
                <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M5 12.5l4.5 4.5L19 7.5" />
                </svg>
              </span>
              <p className="text-sm font-medium text-gray-900 dark:text-gray-100">{t("sent.title")}</p>
              <p className="text-xs text-gray-500 dark:text-gray-400">{t("sent.body")}</p>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="flex min-h-0 flex-col gap-3 overflow-y-auto px-4 py-3">
              <fieldset className="flex flex-col gap-1.5">
                <legend className="text-xs font-medium text-gray-500 dark:text-gray-400">{t("mood.label")}</legend>
                <div className="flex gap-2">
                  {FEEDBACK_MOODS.map((mood) => (
                    <button
                      key={mood}
                      type="button"
                      // Toggles off: the mood is optional, so a mis-tap has to be undoable.
                      onClick={() => setDraft((current) => ({ ...current, mood: current.mood === mood ? null : mood }))}
                      aria-pressed={draft.mood === mood}
                      className={`flex flex-1 flex-col items-center gap-1 rounded-lg border px-1 py-2 text-[11px] transition-colors ${
                        draft.mood === mood ? SELECTED_CLASSES : UNSELECTED_CLASSES
                      }`}
                    >
                      <MoodIcon mood={mood} className="h-5 w-5" />
                      {t(`mood.${mood}`)}
                    </button>
                  ))}
                </div>
              </fieldset>

              <fieldset className="flex flex-col gap-1.5">
                <legend className="text-xs font-medium text-gray-500 dark:text-gray-400">{t("category.label")}</legend>
                <div className="flex flex-wrap gap-2">
                  {FEEDBACK_CATEGORIES.map((category) => (
                    <button
                      key={category}
                      type="button"
                      onClick={() => setDraft((current) => ({ ...current, category }))}
                      aria-pressed={draft.category === category}
                      className={`inline-flex items-center gap-1.5 rounded-lg border px-2.5 py-1.5 text-xs transition-colors ${
                        draft.category === category ? SELECTED_CLASSES : UNSELECTED_CLASSES
                      }`}
                    >
                      <CategoryIcon category={category} className="h-3.5 w-3.5" />
                      {t(`category.${category}`)}
                    </button>
                  ))}
                </div>
              </fieldset>

              <div className="flex flex-col gap-1">
                <label htmlFor="feedback-message" className="text-xs font-medium text-gray-500 dark:text-gray-400">
                  {t("message.label")}
                </label>
                <textarea
                  id="feedback-message"
                  ref={messageRef}
                  value={draft.message}
                  onChange={(event) => {
                    setDraft((current) => ({ ...current, message: event.target.value }));
                    setProblem(null);
                  }}
                  maxLength={FEEDBACK_MESSAGE_MAX_LENGTH}
                  rows={4}
                  placeholder={t("message.placeholder")}
                  className="w-full resize-y rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 placeholder:text-gray-400 focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100 dark:placeholder:text-gray-500"
                />
              </div>

              <div className="flex flex-col gap-1">
                <label htmlFor="feedback-reply-email" className="text-xs font-medium text-gray-500 dark:text-gray-400">
                  {t("replyEmail.label")}
                </label>
                <input
                  id="feedback-reply-email"
                  type="email"
                  value={draft.replyEmail}
                  onChange={(event) => {
                    setDraft((current) => ({ ...current, replyEmail: event.target.value }));
                    setProblem(null);
                  }}
                  placeholder={t("replyEmail.placeholder")}
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 placeholder:text-gray-400 focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100 dark:placeholder:text-gray-500"
                />
              </div>

              {/* Says what travels with the message. The panel collects context precisely because
                  it can; saying so is the difference between helpful and creepy. */}
              <p className="rounded-md bg-gray-50 px-3 py-2 text-[11px] leading-relaxed text-gray-500 dark:bg-gray-800/60 dark:text-gray-400">
                {t.rich("context", {
                  path: () => (
                    <code className="font-mono text-gray-700 dark:text-gray-200">{`/${locale}${pathname}`}</code>
                  ),
                })}
              </p>

              {problem && (
                <p role="alert" className="text-xs text-red-600 dark:text-red-400">
                  {t(`problems.${problem}`)}
                </p>
              )}
              {errorMessage && (
                <p role="alert" className="text-xs text-red-600 dark:text-red-400">
                  {errorMessage}
                </p>
              )}

              <div className="flex items-center justify-between gap-3">
                <span className="text-[11px] tabular-nums text-gray-400 dark:text-gray-500">
                  {characterCount} / {FEEDBACK_MESSAGE_MAX_LENGTH}
                </span>
                <button
                  type="submit"
                  disabled={submitMutation.isPending}
                  className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-accent-strong disabled:cursor-not-allowed disabled:bg-accent/40"
                >
                  {submitMutation.isPending ? t("sending") : t("submit")}
                </button>
              </div>
            </form>
          )}
        </div>
      )}

      <button
        ref={launcherRef}
        type="button"
        onClick={() => (isOpen ? closePanel() : openPanel())}
        aria-expanded={isOpen}
        className="fixed bottom-4 right-4 z-40 inline-flex items-center gap-2 rounded-full bg-accent px-4 py-2.5 text-sm font-medium text-white shadow-lg transition-colors hover:bg-accent-strong"
      >
        <ChatIcon className="h-4 w-4" />
        {t("launcher")}
      </button>
    </>
  );
}
