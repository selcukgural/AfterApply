"use client";

import { useId, useState } from "react";
import { useTranslations } from "next-intl";
import { ApiError } from "@/lib/api/httpClient";
import { COMMENT_MAX_LENGTH, COMMENT_MIN_LENGTH, validateCommentDraft } from "@/lib/blog/commentDraft";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Textarea";

interface CommentFormProps {
  /** The accessible name of the textarea; the placeholder is what shows. */
  label: string;
  placeholder: string;
  submitLabel: string;
  initialValue?: string;
  autoFocus?: boolean;
  /** Shown under the box — the community rule on the root form, nothing on a reply. */
  note?: React.ReactNode;
  /** Resolves when the API accepted the text; throws to keep the form open with an error. */
  onSubmit: (content: string) => Promise<void>;
  onCancel?: () => void;
}

/**
 * One form for a new comment, a reply and an edit (2026-09-20). It checks the text before sending
 * (the API checks again), disables the button while the request is out so a double click is
 * one comment, and turns the API's answer into a sentence: 429 into "too often", 409 into "no
 * longer editable", anything else into the message the API sent or the generic one.
 */
export function CommentForm({ label, placeholder, submitLabel, initialValue = "", autoFocus, note, onSubmit, onCancel }: CommentFormProps) {
  const t = useTranslations("blogComments");
  const id = useId();
  const [content, setContent] = useState(initialValue);
  const [problem, setProblem] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    const draftProblem = validateCommentDraft(content);
    if (draftProblem) {
      setProblem(t(`problems.${draftProblem}`, { min: COMMENT_MIN_LENGTH, max: COMMENT_MAX_LENGTH }));
      return;
    }
    setProblem(null);
    setBusy(true);
    try {
      await onSubmit(content.trim());
      setContent("");
    } catch (err) {
      if (err instanceof ApiError && err.status === 429) setProblem(t("errors.tooMany"));
      else if (err instanceof ApiError && err.status === 409) setProblem(t("errors.locked"));
      else if (err instanceof ApiError && err.status === 400 && err.message) setProblem(err.message);
      else setProblem(t("errors.generic"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form
      className="flex flex-col gap-2"
      onSubmit={(e) => {
        e.preventDefault();
        void submit();
      }}
    >
      <label htmlFor={id} className="sr-only">
        {label}
      </label>
      <Textarea
        id={id}
        value={content}
        onChange={(e) => setContent(e.target.value)}
        placeholder={placeholder}
        rows={4}
        maxLength={COMMENT_MAX_LENGTH}
        autoFocus={autoFocus}
        disabled={busy}
        aria-invalid={problem ? true : undefined}
        aria-describedby={problem ? `${id}-problem` : undefined}
      />
      {problem && (
        <p id={`${id}-problem`} role="alert" className="text-sm text-red-600 dark:text-red-400">
          {problem}
        </p>
      )}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="max-w-xl text-xs leading-5 text-gray-500 dark:text-gray-400">{note}</div>
        <div className="flex flex-col items-end gap-1.5">
          <div className="flex gap-2">
            {onCancel && (
              <Button type="button" variant="secondary" onClick={onCancel} disabled={busy}>
                {t("cancel")}
              </Button>
            )}
            <Button type="submit" disabled={busy}>
              {busy ? t("sending") : submitLabel}
            </Button>
          </div>
          <span className="text-[11px] tabular-nums text-gray-400 dark:text-gray-500" aria-live="polite">
            {t("counter", { count: content.trim().length, max: COMMENT_MAX_LENGTH })}
          </span>
        </div>
      </div>
    </form>
  );
}
