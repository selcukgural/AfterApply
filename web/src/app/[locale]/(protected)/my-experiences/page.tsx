"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { candidateExperiencesApi } from "@/lib/api/candidateExperiences";
import { ApiError } from "@/lib/api/httpClient";
import { Button, buttonClassName } from "@/components/ui/Button";
import { Modal } from "@/components/ui/Modal";
import { StarRating } from "@/components/companyReviews/StarRating";

/** The author's candidate experiences — the twin of "My salaries": the quota line, one card per
 *  entry with the author's own dates, edit and delete. Deleting frees a quota slot. */
export default function MyExperiencesPage() {
  const t = useTranslations("candidateExperiences.mine");
  const tCard = useTranslations("candidateExperiences.card");
  const tOutcome = useTranslations("hiringOutcome");
  const tDuration = useTranslations("processDuration");
  const locale = useLocale();
  const queryClient = useQueryClient();
  const [deleting, setDeleting] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const { data, isLoading } = useQuery({ queryKey: ["candidateExperiences", "mine"], queryFn: candidateExperiencesApi.listMine });

  const remove = useMutation({
    mutationFn: (id: string) => candidateExperiencesApi.remove(id),
    onSuccess: async () => {
      setDeleting(null);
      await queryClient.invalidateQueries({ queryKey: ["candidateExperiences", "mine"] });
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t("error")),
  });

  const quotaLeft = data ? Math.max(0, data.quota.limit - data.quota.used) : 0;

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
          <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
          {data && (
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              {t("quota", { used: data.quota.used, limit: data.quota.limit })}
            </p>
          )}
        </div>
        {quotaLeft > 0 && (
          <Link href="/contribute?tab=experience" className={buttonClassName("primary")}>
            {t("share")}
          </Link>
        )}
      </div>

      {isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
      {error && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      )}

      {data && data.items.length === 0 && (
        <div className="rounded-xl border border-dashed border-gray-300 p-8 text-center text-sm text-gray-600 dark:border-gray-700 dark:text-gray-400">
          <p>{t("empty")}</p>
          <p className="mt-1 text-xs">{t("emptyBody")}</p>
          <Link href="/contribute?tab=experience" className="mt-3 inline-block text-accent-ink underline-offset-2 hover:underline">
            {t("emptyCta")}
          </Link>
        </div>
      )}

      {data && data.items.length > 0 && (
        <ul className="flex flex-col gap-3">
          {data.items.map((entry) => (
            <li key={entry.id} className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div className="flex flex-col gap-1">
                  <Link href={`/companies/${entry.companySlug}?tab=experiences`} className="font-semibold text-gray-900 underline-offset-2 hover:underline dark:text-gray-100">
                    {entry.companyName}
                  </Link>
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

              <div className="flex gap-2">
                <Link href={`/my-experiences/${entry.id}/edit`} className={buttonClassName("secondary")}>
                  {t("edit")}
                </Link>
                <Button variant="danger" onClick={() => setDeleting(entry.id)}>
                  {t("delete")}
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}

      {data && <p className="text-xs text-gray-500 dark:text-gray-400">{t("footnote")}</p>}

      {deleting && (
        <Modal
          title={t("deleteTitle")}
          onClose={() => setDeleting(null)}
          busy={remove.isPending}
          footer={
            <>
              <Button variant="secondary" onClick={() => setDeleting(null)} disabled={remove.isPending}>
                {t("cancel")}
              </Button>
              <Button variant="danger" onClick={() => remove.mutate(deleting)} disabled={remove.isPending}>
                {t("confirmDelete")}
              </Button>
            </>
          }
        >
          <h2 className="text-lg font-semibold">{t("deleteTitle")}</h2>
          <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">{t("deleteBody")}</p>
        </Modal>
      )}
    </div>
  );
}
