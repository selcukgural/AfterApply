"use client";

import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { MyCandidateExperience } from "@/types/api";
import { candidateExperiencesApi } from "@/lib/api/candidateExperiences";
import { ApiError } from "@/lib/api/httpClient";
import { Button, buttonClassName } from "@/components/ui/Button";
import { Modal } from "@/components/ui/Modal";
import { StarRating } from "@/components/companyReviews/StarRating";
import { ContributionKindBadge } from "@/components/contributions/ContributionKindBadge";
import { MyProofLine } from "@/components/contributions/ProofLabel";

interface MyExperienceCardProps {
  entry: MyCandidateExperience;
  /** Called once the row is gone on the server; the list decides what to refetch. */
  onDeleted: () => Promise<void>;
  /** Whether the company page shows the "tracked application" label on this row. */
  backed: boolean;
}

/** The author's own candidate experience on the merged contributions list, with the exact date
 *  they gave (readers see a quarter), and the edit/delete pair. */
export function MyExperienceCard({ entry, backed, onDeleted }: MyExperienceCardProps) {
  const t = useTranslations("candidateExperiences.mine");
  const tCard = useTranslations("candidateExperiences.card");
  const tOutcome = useTranslations("hiringOutcome");
  const tDuration = useTranslations("processDuration");
  const locale = useLocale();
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const remove = useMutation({
    mutationFn: () => candidateExperiencesApi.remove(entry.id),
    onSuccess: async () => {
      setConfirming(false);
      await onDeleted();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t("error")),
  });

  return (
    <li className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex flex-col gap-1">
          <span className="flex flex-wrap items-center gap-2">
            <ContributionKindBadge kind="Experience" />
            <Link href={`/companies/${entry.companySlug}?tab=experiences`} className="text-sm text-gray-500 underline-offset-2 hover:underline dark:text-gray-400">
              {entry.companyName}
            </Link>
          </span>
          <span className="text-xs text-gray-500 dark:text-gray-400">
            {[
              entry.outcome ? tOutcome(entry.outcome) : null,
              entry.duration ? tDuration(entry.duration) : null,
              new Intl.DateTimeFormat(locale, { dateStyle: "medium" }).format(new Date(entry.submittedAt)),
            ]
              .filter(Boolean)
              .join(" · ")}
          </span>
          <span className="text-xs text-gray-500 dark:text-gray-400">
            {t("summary", { count: entry.likedStatements.length + entry.improvableStatements.length })}
            {entry.categoryRatings.length > 0 ? ` · ${tCard("ratedCategories", { count: entry.categoryRatings.length })}` : null}
          </span>
        </div>
        <div className="flex items-center gap-2">
          <StarRating value={entry.overallRating} label={tCard("overallLabel", { value: entry.overallRating })} size="lg" />
          <span className="text-sm font-semibold text-gray-900 dark:text-gray-100">{entry.overallRating}</span>
        </div>
      </div>

      <MyProofLine kind="tracked" backed={backed} />

      {error && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      )}

      <div className="flex gap-2">
        <Link href={`/my-experiences/${entry.id}/edit`} className={buttonClassName("secondary")}>
          {t("edit")}
        </Link>
        <Button variant="danger" onClick={() => setConfirming(true)}>
          {t("delete")}
        </Button>
      </div>

      {confirming && (
        <Modal
          title={t("deleteTitle")}
          onClose={() => setConfirming(false)}
          busy={remove.isPending}
          footer={
            <>
              <Button variant="secondary" onClick={() => setConfirming(false)} disabled={remove.isPending}>
                {t("cancel")}
              </Button>
              <Button variant="danger" onClick={() => remove.mutate()} disabled={remove.isPending}>
                {t("confirmDelete")}
              </Button>
            </>
          }
        >
          <h2 className="text-lg font-semibold">{t("deleteTitle")}</h2>
          <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">{t("deleteBody")}</p>
        </Modal>
      )}
    </li>
  );
}
