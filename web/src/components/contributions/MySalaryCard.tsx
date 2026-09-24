"use client";

import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { MyCompanySalary } from "@/types/api";
import { companySalariesApi } from "@/lib/api/companySalaries";
import { ApiError } from "@/lib/api/httpClient";
import { formatAmount, formatSalaryPeriod, occupationName } from "@/lib/companySalaries/salaryDraft";
import { Button, buttonClassName } from "@/components/ui/Button";
import { Modal } from "@/components/ui/Modal";
import { ContributionKindBadge } from "@/components/contributions/ContributionKindBadge";
import { MyProofLine } from "@/components/contributions/ProofLabel";
import { SalaryPositionPanel } from "@/components/companySalaries/SalaryPositionPanel";

interface MySalaryCardProps {
  entry: MyCompanySalary;
  /** Called once the row is gone on the server; the list decides what to refetch. */
  onDeleted: () => Promise<void>;
  /** Whether the company page shows the "tracked application" label on this row. */
  backed: boolean;
}

/** The author's own salary entry on the merged contributions list, with the exact figures they
 *  gave (readers see a band), and the edit/delete pair. */
export function MySalaryCard({ entry, backed, onDeleted }: MySalaryCardProps) {
  const t = useTranslations("companySalaries.mine");
  const tType = useTranslations("employmentType");
  const tStatus = useTranslations("salaryEmploymentStatus");
  const locale = useLocale();
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const period = formatSalaryPeriod(entry, t("periodOngoing"));

  const remove = useMutation({
    mutationFn: () => companySalariesApi.remove(entry.id),
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
            <ContributionKindBadge kind="Salary" />
            <Link href={`/companies/${entry.companySlug}`} className="text-sm text-gray-500 underline-offset-2 hover:underline dark:text-gray-400">
              {entry.companyName}
            </Link>
          </span>
          <span className="font-semibold text-gray-900 dark:text-gray-100">{occupationName(entry.occupation, locale)}</span>
          <span className="text-xs text-gray-500 dark:text-gray-400">
            {[
              t("years", { count: entry.yearsOfExperience }),
              tType(entry.employmentType),
              tStatus(entry.employmentStatus),
              period ?? t("periodMissing"),
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
            {entry.annualBonusAmount !== null ? t("bonus", { amount: formatAmount(locale, entry.annualBonusAmount, entry.currency) }) : t("noBonus")}
          </span>
        </div>
      </div>

      {/* Where this salary sits in the company's current band — or how far the band is from
          opening (contribution loop #9). */}
      <SalaryPositionPanel entryId={entry.id} variant="card" />

      {/* A row from before the period existed: readers see it as history until this is fixed. */}
      {period === null && <p className="text-xs text-amber-700 dark:text-amber-300">{t("periodMissingHint")}</p>}

      <MyProofLine kind="accepted" backed={backed} />

      {error && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      )}

      <div className="flex gap-2">
        <Link href={`/my-salaries/${entry.id}/edit`} className={buttonClassName("secondary")}>
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
