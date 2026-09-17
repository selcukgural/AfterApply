"use client";

import { useMemo, useState, type FormEvent } from "react";
import { useTranslations } from "next-intl";
import type { CandidateExperienceRequest, ExperienceCategory } from "@/types/api";
import {
  buildExperienceRequest,
  isCapReached,
  suggestionsFor,
  toggleInterviewType,
  togglePick,
  validateExperienceDraft,
  type ExperienceDraft,
  type ExperienceDraftField,
  type ExperienceDraftProblem,
} from "@/lib/candidateExperiences/experienceDraft";
import {
  EXPERIENCE_CATALOGUE,
  HIRING_OUTCOMES,
  INTERVIEW_TYPES,
  OPTIONAL_EXPERIENCE_CATEGORIES,
  PROCESS_DURATIONS,
  STAGE_COUNTS,
} from "@/lib/candidateExperiences/statementCatalogue";
import { MAX_PICKS_PER_KIND, categoryMessageKey } from "@/lib/statements/catalogue";
import { Button } from "@/components/ui/Button";
import { CategoryRatingRow } from "@/components/companyReviews/CategoryRatingRow";
import { FactCheckPills, FactPills } from "@/components/candidateExperiences/FactPills";

interface CandidateExperienceFormProps {
  companyName: string;
  initialDraft: ExperienceDraft;
  submitLabel: string;
  onSubmit: (request: CandidateExperienceRequest) => Promise<void>;
  /** A message from the server (quota, duplicate) — shown under the form. */
  serverError: string | null;
}

/**
 * The candidate experience form (design canvas "Yön A", 2026-09-17): the review form's twin. A
 * box of optional closed-list facts about the process first, then one required overall rating
 * and eight optional category ratings, each opening its catalogue statements once rated. No
 * free text anywhere, on purpose — and no question whose answer could point at a person.
 */
export function CandidateExperienceForm({ companyName, initialDraft, submitLabel, onSubmit, serverError }: CandidateExperienceFormProps) {
  const t = useTranslations("candidateExperiences.form");
  const tShared = useTranslations("companyReviews.form");
  const tCategories = useTranslations("candidateExperiences.categories");
  const tStatements = useTranslations("candidateExperiences.statements");
  const tOutcome = useTranslations("hiringOutcome");
  const tDuration = useTranslations("processDuration");
  const tStages = useTranslations("stageCount");
  const tTypes = useTranslations("interviewType");
  const statementText = (key: string) => ({ label: tStatements(`${key}.label`), sentence: tStatements(`${key}.sentence`) });

  const [draft, setDraft] = useState<ExperienceDraft>(initialDraft);
  const [problems, setProblems] = useState<Partial<Record<ExperienceDraftField, ExperienceDraftProblem>>>({});
  const [submitting, setSubmitting] = useState(false);

  const problemText = (field: ExperienceDraftField) => {
    const problem = problems[field];
    return problem ? t(`problems.${problem}`) : undefined;
  };

  const picked = useMemo(() => new Set([...draft.liked, ...draft.improvable]), [draft.liked, draft.improvable]);
  const capReached = { Liked: isCapReached(draft, "Liked"), Improve: isCapReached(draft, "Improve") };

  const rate = (category: ExperienceCategory) => (value: number) =>
    setDraft((prev) =>
      category === "Overall" ? { ...prev, overall: value } : { ...prev, ratings: { ...prev.ratings, [category]: value } },
    );
  const clear = (category: ExperienceCategory) => () =>
    setDraft((prev) => {
      const ratings = { ...prev.ratings };
      delete ratings[category];
      return { ...prev, ratings };
    });
  const toggle = (key: string) => setDraft((prev) => togglePick(prev, key));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const found = validateExperienceDraft(draft);
    setProblems(found);
    if (Object.keys(found).length > 0) {
      return;
    }

    setSubmitting(true);
    try {
      await onSubmit(buildExperienceRequest(draft));
    } finally {
      setSubmitting(false);
    }
  };

  const listProblem = problemText("liked") ?? problemText("improvable");
  const row = (category: ExperienceCategory) => ({
    label: tCategories(categoryMessageKey(category)),
    liked: EXPERIENCE_CATALOGUE.for(category, "Liked"),
    improvable: EXPERIENCE_CATALOGUE.for(category, "Improve"),
    statementText,
    picked,
    capReached,
    onToggle: toggle,
  });

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

      <div className="flex flex-col gap-4 rounded-lg border border-gray-100 bg-gray-50 p-4 dark:border-gray-800 dark:bg-gray-950">
        <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
          <h3 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("factsHeading")}</h3>
          <p className="text-xs text-gray-500 dark:text-gray-400">{t("factsNote")}</p>
        </div>
        <FactPills
          label={t("outcome")}
          options={HIRING_OUTCOMES}
          value={draft.outcome}
          optionLabel={(o) => tOutcome(o)}
          notSaidLabel={t("notSaid")}
          optionalLabel={t("optional")}
          onChange={(outcome) => setDraft((prev) => ({ ...prev, outcome }))}
        />
        <div className="grid gap-4 md:grid-cols-2">
          <FactPills
            label={t("duration")}
            options={PROCESS_DURATIONS}
            value={draft.duration}
            optionLabel={(o) => tDuration(o)}
            notSaidLabel={t("notSaid")}
            optionalLabel={t("optional")}
            onChange={(duration) => setDraft((prev) => ({ ...prev, duration }))}
          />
          <FactPills
            label={t("stages")}
            options={STAGE_COUNTS}
            value={draft.stages}
            optionLabel={(o) => tStages(o)}
            notSaidLabel={t("notSaid")}
            optionalLabel={t("optional")}
            onChange={(stages) => setDraft((prev) => ({ ...prev, stages }))}
          />
        </div>
        <FactCheckPills
          label={t("interviewTypes")}
          hint={t("interviewTypesHint")}
          options={INTERVIEW_TYPES}
          values={draft.interviewTypes}
          optionLabel={(o) => tTypes(o)}
          optionalLabel={t("optional")}
          onToggle={(type) => setDraft((prev) => toggleInterviewType(prev, type, INTERVIEW_TYPES))}
        />
      </div>

      <div className="flex flex-col">
        <CategoryRatingRow
          {...row("Overall")}
          required
          rating={draft.overall}
          suggestions={suggestionsFor("Overall", draft.overall)}
          onRate={rate("Overall")}
          error={problemText("overall")}
        />
        <p className="border-t border-gray-100 pt-3 text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">{t("optionalNote")}</p>
        {OPTIONAL_EXPERIENCE_CATEGORIES.map((category) => (
          <CategoryRatingRow
            key={category}
            {...row(category)}
            rating={draft.ratings[category] ?? 0}
            suggestions={suggestionsFor(category, draft.ratings[category] ?? 0)}
            onRate={rate(category)}
            onClear={clear(category)}
            error={problemText(category)}
          />
        ))}
      </div>

      <div className="sticky bottom-0 -mb-5 flex flex-wrap items-center justify-between gap-3 border-t border-gray-200 bg-white py-4 dark:border-gray-800 dark:bg-gray-900">
        <div className="flex flex-col gap-1">
          <p aria-live="polite" className="text-xs text-gray-600 dark:text-gray-400">
            {tShared("counters", { liked: draft.liked.length, improvable: draft.improvable.length, max: MAX_PICKS_PER_KIND })}
            {capReached.Liked || capReached.Improve ? <span className="ml-1 text-gray-500">— {tShared("capReached")}</span> : null}
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
