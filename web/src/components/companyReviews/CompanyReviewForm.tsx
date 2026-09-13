"use client";

import { useState, type FormEvent } from "react";
import { useTranslations } from "next-intl";
import type { CompanyReviewRequest } from "@/types/api";
import {
  EMPLOYMENT_STATUSES,
  RATING_KEYS,
  REVIEW_TEXT_MAX_LENGTH,
  REVIEW_TITLE_MAX_LENGTH,
  buildReviewRequest,
  validateReviewDraft,
  type RatingKey,
  type ReviewDraft,
  type ReviewDraftField,
  type ReviewDraftProblem,
} from "@/lib/companyReviews/reviewDraft";
import { createCompanyReviewSchema } from "@/lib/validation/companyReviewSchema";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { Textarea } from "@/components/ui/Textarea";
import { RatingInput } from "@/components/companyReviews/RatingInput";

interface CompanyReviewFormProps {
  companyName: string;
  initialDraft: ReviewDraft;
  submitLabel: string;
  onSubmit: (request: CompanyReviewRequest) => Promise<void>;
  /** A message from the server (quota, duplicate) — shown under the form. */
  serverError: string | null;
}

export function CompanyReviewForm({ companyName, initialDraft, submitLabel, onSubmit, serverError }: CompanyReviewFormProps) {
  const t = useTranslations("companyReviews.form");
  const tRatings = useTranslations("companyReviews.ratings");
  const tStatus = useTranslations("employmentStatus");
  const tValidation = useTranslations("validation");

  const [draft, setDraft] = useState<ReviewDraft>(initialDraft);
  const [problems, setProblems] = useState<Partial<Record<ReviewDraftField, ReviewDraftProblem>>>({});
  const [submitting, setSubmitting] = useState(false);

  const problemText = (field: ReviewDraftField) => {
    const problem = problems[field];
    return problem ? t(`problems.${problem}`) : undefined;
  };

  const setRating = (key: RatingKey) => (value: number) =>
    setDraft((prev) => ({ ...prev, ratings: { ...prev.ratings, [key]: value } }));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const found = validateReviewDraft(draft);
    setProblems(found);
    if (Object.keys(found).length > 0) {
      return;
    }

    const request = buildReviewRequest(draft);
    // Belt to the braces above: the same rules as the server's, on the request that leaves.
    if (!createCompanyReviewSchema(tValidation).safeParse(request).success) {
      return;
    }

    setSubmitting(true);
    try {
      await onSubmit(request);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <form
      onSubmit={handleSubmit}
      noValidate
      className="flex flex-col gap-5 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-gray-900"
    >
      <div>
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("heading", { company: companyName })}</h2>
        <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("anonymityNote")}</p>
      </div>

      <FormField label={t("employmentStatus")} htmlFor="review-employment" error={problemText("employmentStatus")}>
        <Select
          id="review-employment"
          value={draft.employmentStatus}
          onChange={(e) => setDraft((prev) => ({ ...prev, employmentStatus: e.target.value as ReviewDraft["employmentStatus"] }))}
        >
          <option value="">{t("employmentStatusPlaceholder")}</option>
          {EMPLOYMENT_STATUSES.map((status) => (
            <option key={status} value={status}>
              {tStatus(status)}
            </option>
          ))}
        </Select>
      </FormField>

      <div className="grid gap-4 sm:grid-cols-2">
        {RATING_KEYS.map((key) => (
          <RatingInput key={key} label={tRatings(key)} value={draft.ratings[key]} onChange={setRating(key)} error={problemText(key)} />
        ))}
      </div>

      <FormField label={t("title")} htmlFor="review-title" error={problemText("title")}>
        <Input
          id="review-title"
          value={draft.title}
          maxLength={REVIEW_TITLE_MAX_LENGTH}
          placeholder={t("titlePlaceholder")}
          onChange={(e) => setDraft((prev) => ({ ...prev, title: e.target.value }))}
        />
      </FormField>

      <FormField label={t("pros")} htmlFor="review-pros" error={problemText("pros")}>
        <Textarea
          id="review-pros"
          rows={4}
          value={draft.pros}
          maxLength={REVIEW_TEXT_MAX_LENGTH}
          placeholder={t("prosPlaceholder")}
          onChange={(e) => setDraft((prev) => ({ ...prev, pros: e.target.value }))}
        />
        <p className="text-right text-xs text-gray-400">
          {draft.pros.length} / {REVIEW_TEXT_MAX_LENGTH}
        </p>
      </FormField>

      <FormField label={t("cons")} htmlFor="review-cons" error={problemText("cons")}>
        <Textarea
          id="review-cons"
          rows={4}
          value={draft.cons}
          maxLength={REVIEW_TEXT_MAX_LENGTH}
          placeholder={t("consPlaceholder")}
          onChange={(e) => setDraft((prev) => ({ ...prev, cons: e.target.value }))}
        />
        <p className="text-right text-xs text-gray-400">
          {draft.cons.length} / {REVIEW_TEXT_MAX_LENGTH}
        </p>
      </FormField>

      {serverError && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {serverError}
        </p>
      )}

      <p className="text-xs text-gray-500 dark:text-gray-400">{t("moderationNote")}</p>

      <Button type="submit" disabled={submitting} className="self-start">
        {submitting ? t("submitting") : submitLabel}
      </Button>
    </form>
  );
}
