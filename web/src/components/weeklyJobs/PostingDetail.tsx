"use client";

import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useRouter } from "@/i18n/navigation";
import type { JobSourcePostingDetailResponse } from "@/types/api";
import { jobSourcesApi } from "@/lib/api/jobSources";
import { ApiError } from "@/lib/api/httpClient";
import { SCORE_TEXT_CLASSES, scoreTone, sourceLabel } from "@/lib/weeklyJobs/score";
import { Button, buttonClassName } from "@/components/ui/Button";

interface PostingDetailProps {
  posting: JobSourcePostingDetailResponse;
  /** The detail page shows the title as its h1; the split view's right column as an h2. */
  headingLevel?: "h1" | "h2";
}

function Chips({ items, className }: { items: string[]; className: string }) {
  return (
    <ul className="flex flex-wrap gap-1.5">
      {items.map((item) => (
        <li key={item} className={`rounded-full px-2.5 py-0.5 text-xs ${className}`}>
          {item}
        </li>
      ))}
    </ul>
  );
}

/**
 * One delivered posting in full: the score and the model's reasons at the top, the actions, the
 * source's text last. Everything textual here came from LinkedIn or from the model and is
 * rendered as text — never as HTML.
 */
export function PostingDetail({ posting, headingLevel = "h2" }: PostingDetailProps) {
  const t = useTranslations("weeklyJobs.detail");
  const router = useRouter();
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const Heading = headingLevel;
  const tone = scoreTone(posting.score);

  const apply = useMutation({
    mutationFn: () => jobSourcesApi.markApplied(posting.id),
    onSuccess: async (application) => {
      await queryClient.invalidateQueries({ queryKey: ["applications"] });
      router.push(`/applications/${application.id}`);
    },
    onError: (mutationError) => setError(mutationError instanceof ApiError ? mutationError.message : t("applyError")),
  });

  const subtitle = [posting.companyName, posting.location, posting.employmentType, posting.seniority, sourceLabel(posting.source)]
    .filter((part): part is string => Boolean(part))
    .join(" · ");

  return (
    <article className="flex flex-col gap-4 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-gray-900">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between sm:gap-4">
        <div className="flex flex-col gap-1">
          <Heading className="text-base font-semibold text-gray-900 dark:text-gray-100">{posting.title}</Heading>
          <p className="text-sm text-gray-700 dark:text-gray-300">{subtitle}</p>
        </div>
        <div className="flex items-baseline gap-1.5">
          {posting.score === null ? (
            <span className="text-sm text-gray-500 dark:text-gray-400">{t("unscored")}</span>
          ) : (
            <>
              <span className={`text-3xl font-semibold tracking-tight ${SCORE_TEXT_CLASSES[tone]}`}>%{posting.score}</span>
              <span className="text-xs text-gray-500 dark:text-gray-400">{t("fit")}</span>
            </>
          )}
        </div>
      </header>

      {posting.scoreSummary && <p className="text-sm text-gray-700 dark:text-gray-300">{posting.scoreSummary}</p>}

      {(posting.matchedCriteria.length > 0 || posting.missingCriteria.length > 0) && (
        <div className="grid gap-3 sm:grid-cols-2">
          <section className="flex flex-col gap-1.5">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-good-ink">{t("matched")}</h3>
            {posting.matchedCriteria.length > 0 ? (
              <Chips items={posting.matchedCriteria} className="bg-good-wash text-good-ink" />
            ) : (
              <p className="text-xs text-gray-500 dark:text-gray-400">{t("none")}</p>
            )}
          </section>
          <section className="flex flex-col gap-1.5">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-crit-ink">{t("missing")}</h3>
            {posting.missingCriteria.length > 0 ? (
              <Chips items={posting.missingCriteria} className="bg-crit-wash text-crit-ink" />
            ) : (
              <p className="text-xs text-gray-500 dark:text-gray-400">{t("none")}</p>
            )}
          </section>
        </div>
      )}

      {posting.requiredSkills.length > 0 && (
        <section className="flex flex-col gap-1.5">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{t("requiredSkills")}</h3>
          <p className="text-sm text-gray-700 dark:text-gray-300">{posting.requiredSkills.join(", ")}</p>
        </section>
      )}

      <div className="flex flex-col gap-2 pt-1 sm:flex-row sm:items-center">
        <Button type="button" onClick={() => apply.mutate()} disabled={apply.isPending}>
          {apply.isPending ? t("applying") : t("applied")}
        </Button>
        <a href={posting.url} target="_blank" rel="noopener noreferrer" className={buttonClassName("outline", "text-center")}>
          {t("openOnSource", { source: sourceLabel(posting.source) })}
        </a>
      </div>
      {error && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      )}

      <section className="flex flex-col gap-2 border-t border-gray-100 pt-4 dark:border-gray-800">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{t("description")}</h3>
        {posting.description ? (
          <p className="whitespace-pre-line text-sm text-gray-700 dark:text-gray-300">{posting.description}</p>
        ) : (
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("noDescription")}</p>
        )}
      </section>
    </article>
  );
}
