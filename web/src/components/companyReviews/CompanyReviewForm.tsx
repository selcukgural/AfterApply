"use client";

import { useMemo, useState, type FormEvent } from "react";
import { useTranslations } from "next-intl";
import type { CompanyReviewRequest, ReviewCategory } from "@/types/api";
import {
  EMPLOYMENT_STATUSES,
  buildReviewRequest,
  isCapReached,
  suggestionsFor,
  togglePick,
  validateReviewDraft,
  type ReviewDraft,
  type ReviewDraftField,
  type ReviewDraftProblem,
} from "@/lib/companyReviews/reviewDraft";
import { MAX_PICKS_PER_KIND, OPTIONAL_CATEGORIES, categoryMessageKey, statementsFor } from "@/lib/companyReviews/statementCatalogue";
import { createCompanyReviewSchema } from "@/lib/validation/companyReviewSchema";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";
import { Select } from "@/components/ui/Select";
import { CategoryRatingRow } from "@/components/companyReviews/CategoryRatingRow";

interface CompanyReviewFormProps {
  companyName: string;
  initialDraft: ReviewDraft;
  submitLabel: string;
  onSubmit: (request: CompanyReviewRequest) => Promise<void>;
  /** A message from the server (quota, duplicate) — shown under the form. */
  serverError: string | null;
  /** Shown above the form when the review being edited is in the earlier free-text format. */
  legacyNotice?: string;
}

/**
 * The structured review form (design canvas "Yön C", 2026-09-16): a relationship, one required
 * overall rating, ten optional category ratings, and — per rated category — statements from the
 * catalogue. No free text anywhere, on purpose. The sticky footer keeps the two pick counters and
 * the submit button in view on a form that is eleven rows tall.
 */
export function CompanyReviewForm({ companyName, initialDraft, submitLabel, onSubmit, serverError, legacyNotice }: CompanyReviewFormProps) {
  const t = useTranslations("companyReviews.form");
  const tStatus = useTranslations("employmentStatus");
  const tValidation = useTranslations("validation");
  const tCategories = useTranslations("companyReviews.categories");
  const tStatements = useTranslations("companyReviews.statements");
  const statementText = (key: string) => ({ label: tStatements(`${key}.label`), sentence: tStatements(`${key}.sentence`) });

  const [draft, setDraft] = useState<ReviewDraft>(initialDraft);
  const [problems, setProblems] = useState<Partial<Record<ReviewDraftField, ReviewDraftProblem>>>({});
  const [submitting, setSubmitting] = useState(false);

  const problemText = (field: ReviewDraftField) => {
    const problem = problems[field];
    return problem ? t(`problems.${problem}`) : undefined;
  };

  const picked = useMemo(() => new Set([...draft.liked, ...draft.improvable]), [draft.liked, draft.improvable]);
  const capReached = { Liked: isCapReached(draft, "Liked"), Improve: isCapReached(draft, "Improve") };

  const rate = (category: ReviewCategory) => (value: number) =>
    setDraft((prev) =>
      category === "Overall" ? { ...prev, overall: value } : { ...prev, ratings: { ...prev.ratings, [category]: value } },
    );
  const clear = (category: ReviewCategory) => () =>
    setDraft((prev) => {
      const ratings = { ...prev.ratings };
      delete ratings[category];
      return { ...prev, ratings };
    });
  const toggle = (key: string) => setDraft((prev) => togglePick(prev, key));

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

  const listProblem = problemText("liked") ?? problemText("improvable");

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

      {legacyNotice ? (
        <p className="rounded-lg border border-warn/40 bg-warn-wash px-3 py-2 text-sm text-warn-ink">{legacyNotice}</p>
      ) : null}

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

      <div className="flex flex-col">
        <CategoryRatingRow
          label={tCategories(categoryMessageKey("Overall"))}
          liked={statementsFor("Overall", "Liked")}
          improvable={statementsFor("Overall", "Improve")}
          statementText={statementText}
          required
          rating={draft.overall}
          suggestions={suggestionsFor("Overall", draft.overall)}
          picked={picked}
          capReached={capReached}
          onRate={rate("Overall")}
          onToggle={toggle}
          error={problemText("overall")}
        />
        <p className="border-t border-gray-100 pt-3 text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">{t("optionalNote")}</p>
        {OPTIONAL_CATEGORIES.map((category) => (
          <CategoryRatingRow
            key={category}
            label={tCategories(categoryMessageKey(category))}
            liked={statementsFor(category, "Liked")}
            improvable={statementsFor(category, "Improve")}
            statementText={statementText}
            rating={draft.ratings[category] ?? 0}
            suggestions={suggestionsFor(category, draft.ratings[category] ?? 0)}
            picked={picked}
            capReached={capReached}
            onRate={rate(category)}
            onClear={clear(category)}
            onToggle={toggle}
            error={problemText(category)}
          />
        ))}
      </div>

      <div className="sticky bottom-0 -mb-5 flex flex-wrap items-center justify-between gap-3 border-t border-gray-200 bg-white py-4 dark:border-gray-800 dark:bg-gray-900">
        <div className="flex flex-col gap-1">
          <p aria-live="polite" className="text-xs text-gray-600 dark:text-gray-400">
            {t("counters", { liked: draft.liked.length, improvable: draft.improvable.length, max: MAX_PICKS_PER_KIND })}
            {capReached.Liked || capReached.Improve ? <span className="ml-1 text-gray-500">— {t("capReached")}</span> : null}
          </p>
          {listProblem ? (
            <p className="text-sm text-red-600 dark:text-red-400">{listProblem}</p>
          ) : (
            <p className="text-xs text-gray-500 dark:text-gray-400">{t("publishNote")}</p>
          )}
          {serverError && (
            <p role="alert" className="text-sm text-red-600 dark:text-red-400">
              {serverError}
            </p>
          )}
        </div>
        <Button type="submit" disabled={submitting}>
          {submitting ? t("submitting") : submitLabel}
        </Button>
      </div>
    </form>
  );
}
