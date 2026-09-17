"use client";

import { useLocale, useTranslations } from "next-intl";
import type { CandidateExperiencePublic } from "@/types/api";
import { EXPERIENCE_CATALOGUE } from "@/lib/candidateExperiences/statementCatalogue";
import { formatQuarter } from "@/lib/candidateExperiences/experienceDraft";
import { categoryMessageKey } from "@/lib/statements/catalogue";
import { StarRating } from "@/components/companyReviews/StarRating";
import { StatementTag } from "@/components/companyReviews/StatementChip";

/**
 * One published candidate experience: the overall stars, a line of short facts (outcome, length,
 * rounds, quarter), only the areas the author rated, and the statements they picked as short
 * labels (the full sentence is the tooltip). Nothing here is text the author typed, and nothing
 * points at a person — no title, no month.
 */
export function ExperienceCard({ experience }: { experience: CandidateExperiencePublic }) {
  const t = useTranslations("candidateExperiences.card");
  const tCategories = useTranslations("candidateExperiences.categories");
  const tStatements = useTranslations("candidateExperiences.statements");
  const tOutcome = useTranslations("hiringOutcome");
  const tDuration = useTranslations("processDuration");
  const tTypes = useTranslations("interviewType");
  const locale = useLocale();

  const STAGE_NUMBER = { One: 1, Two: 2, Three: 3, Four: 4 } as const;
  const stages =
    experience.stages === null ? null : experience.stages === "FivePlus" ? t("stagesFivePlus") : t("stages", { count: STAGE_NUMBER[experience.stages] });
  const facts = [
    experience.outcome ? tOutcome(experience.outcome) : null,
    experience.duration ? tDuration(experience.duration) : null,
    stages,
    formatQuarter(experience.submittedQuarter, locale),
  ].filter((f): f is string => f !== null);
  const hasPicks = experience.likedStatements.length > 0 || experience.improvableStatements.length > 0;

  const tags = (keys: string[]) =>
    keys.flatMap((key) => {
      const statement = EXPERIENCE_CATALOGUE.find(key);
      // A key the catalogue no longer knows renders as nothing rather than as its key.
      if (!statement) return [];
      return [
        <li key={key}>
          <StatementTag label={tStatements(`${key}.label`)} sentence={tStatements(`${key}.sentence`)} kind={statement.kind} />
        </li>,
      ];
    });

  return (
    <article className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <header className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex flex-col gap-1">
          <p className="text-sm text-gray-700 dark:text-gray-300">{facts.join(" · ")}</p>
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {experience.categoryRatings.length > 0 ? t("ratedCategories", { count: experience.categoryRatings.length }) : null}
            {experience.interviewTypes.length > 0
              ? `${experience.categoryRatings.length > 0 ? " · " : ""}${experience.interviewTypes.map((type) => tTypes(type)).join(", ")}`
              : null}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <StarRating value={experience.overallRating} label={t("overallLabel", { value: experience.overallRating })} size="lg" />
          <span className="text-sm font-semibold text-gray-900 dark:text-gray-100">{experience.overallRating}</span>
        </div>
      </header>

      {experience.categoryRatings.length > 0 ? (
        <dl className="grid gap-x-4 gap-y-1 text-xs text-gray-600 sm:grid-cols-2 dark:text-gray-400">
          {experience.categoryRatings.map(({ category, rating }) => (
            <div key={category} className="flex items-center justify-between gap-2">
              <dt>{tCategories(categoryMessageKey(category))}</dt>
              <dd>
                <StarRating value={rating} label={`${tCategories(categoryMessageKey(category))}: ${rating}`} />
              </dd>
            </div>
          ))}
        </dl>
      ) : null}

      {hasPicks ? (
        <div className="grid gap-3 sm:grid-cols-2">
          {experience.likedStatements.length > 0 ? (
            <section>
              <h4 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-good-ink">{t("liked")}</h4>
              <ul className="flex flex-wrap gap-1.5">{tags(experience.likedStatements)}</ul>
            </section>
          ) : null}
          {experience.improvableStatements.length > 0 ? (
            <section>
              <h4 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-warn-ink">{t("improvable")}</h4>
              <ul className="flex flex-wrap gap-1.5">{tags(experience.improvableStatements)}</ul>
            </section>
          ) : null}
        </div>
      ) : null}
    </article>
  );
}
