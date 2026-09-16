"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { companySalariesApi } from "@/lib/api/companySalaries";
import { ApiError } from "@/lib/api/httpClient";
import { formatAmount, occupationName } from "@/lib/companySalaries/salaryDraft";
import { Button, buttonClassName } from "@/components/ui/Button";
import { Modal } from "@/components/ui/Modal";

/** The author's salary entries — the twin of "My reviews": the quota line, one card per entry
 *  with the exact figures the author gave, edit and delete. Deleting frees a quota slot. */
export default function MySalariesPage() {
  const t = useTranslations("companySalaries.mine");
  const tType = useTranslations("employmentType");
  const tStatus = useTranslations("salaryEmploymentStatus");
  const locale = useLocale();
  const queryClient = useQueryClient();
  const [deleting, setDeleting] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const { data, isLoading } = useQuery({ queryKey: ["companySalaries", "mine"], queryFn: companySalariesApi.listMine });

  const remove = useMutation({
    mutationFn: (id: string) => companySalariesApi.remove(id),
    onSuccess: async () => {
      setDeleting(null);
      await queryClient.invalidateQueries({ queryKey: ["companySalaries", "mine"] });
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
          <Link href="/contribute?tab=salary" className={buttonClassName("primary")}>
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
          <Link href="/contribute?tab=salary" className="mt-3 inline-block text-accent-ink underline-offset-2 hover:underline">
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
                  <Link href={`/companies/${entry.companySlug}`} className="text-sm text-gray-500 underline-offset-2 hover:underline dark:text-gray-400">
                    {entry.companyName}
                  </Link>
                  <span className="font-semibold text-gray-900 dark:text-gray-100">{occupationName(entry.occupation, locale)}</span>
                  <span className="text-xs text-gray-500 dark:text-gray-400">
                    {[
                      t("years", { count: entry.yearsOfExperience }),
                      tType(entry.employmentType),
                      tStatus(entry.employmentStatus),
                      new Intl.DateTimeFormat(locale, { dateStyle: "medium" }).format(new Date(entry.submittedAt)),
                    ].join(" · ")}
                  </span>
                </div>
                <div className="flex flex-col items-end gap-0.5">
                  <span className="text-base font-semibold text-gray-900 dark:text-gray-100">
                    {formatAmount(locale, entry.monthlyNetAmount, entry.currency)}{" "}
                    <span className="text-xs font-normal text-gray-500 dark:text-gray-400">{t("perMonthNet")}</span>
                  </span>
                  <span className="text-xs text-gray-500 dark:text-gray-400">
                    {entry.annualBonusAmount !== null
                      ? t("bonus", { amount: formatAmount(locale, entry.annualBonusAmount, entry.currency) })
                      : t("noBonus")}
                  </span>
                </div>
              </div>

              <div className="flex gap-2">
                <Link href={`/my-salaries/${entry.id}/edit`} className={buttonClassName("secondary")}>
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
