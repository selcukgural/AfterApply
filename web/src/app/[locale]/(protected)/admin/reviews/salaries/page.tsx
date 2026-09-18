"use client";

import { useCallback, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import type { AdminCompanySalaryListItem } from "@/types/api";
import { adminApi } from "@/lib/api/admin";
import { ApiError } from "@/lib/api/httpClient";
import { parseContributionFilters, toContributionSearchParams, withCompanyChange } from "@/lib/admin/contributionListView";
import { formatAmount, formatSalaryPeriod, occupationName } from "@/lib/companySalaries/salaryDraft";
import { clampPage } from "@/lib/dashboard/reminders";
import { Card } from "@/components/dashboard/Card";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Modal } from "@/components/ui/Modal";
import { Pagination } from "@/components/applications/Pagination";
import { AdminTabs } from "@/components/admin/AdminTabs";
import { AdminContributionTabs } from "@/components/admin/AdminContributionTabs";

/** The admin's salary table (2026-09-18): every entry with its author, newest first, ten per page.
 *  There is nothing to moderate — a row is either left alone or removed, after a second look in
 *  the modal. Admin-only: the author's e-mail is on every row. */
export default function AdminSalariesPage() {
  const t = useTranslations("adminSalaries");
  const tType = useTranslations("employmentType");
  const tStatus = useTranslations("salaryEmploymentStatus");
  const tMine = useTranslations("companySalaries.mine");
  const locale = useLocale();
  const router = useRouter();
  const queryClient = useQueryClient();
  const filters = parseContributionFilters(useSearchParams());
  const [open, setOpen] = useState<AdminCompanySalaryListItem | null>(null);
  const [confirming, setConfirming] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const apply = useCallback(
    (next: { company: string; page: number }) => {
      const params = toContributionSearchParams(next).toString();
      router.push(params ? `/admin/reviews/salaries?${params}` : "/admin/reviews/salaries");
    },
    [router],
  );

  const list = useQuery({
    queryKey: ["admin", "salaries", filters],
    queryFn: () => adminApi.listSalaries(filters),
    retry: (failureCount, err) => !(err instanceof ApiError && (err.status === 403 || err.status === 404)) && failureCount < 2,
  });

  const remove = useMutation({
    mutationFn: (id: string) => adminApi.deleteSalary(id),
    onSuccess: async () => {
      setOpen(null);
      setConfirming(false);
      await queryClient.invalidateQueries({ queryKey: ["admin", "salaries"] });
      if (list.data) {
        apply({ ...filters, page: clampPage(filters.page, list.data.totalCount - 1, list.data.pageSize) });
      }
    },
    onError: (err) => setActionError(err instanceof ApiError ? err.message : t("error")),
  });

  const formatDate = (iso: string) => new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(new Date(iso));

  if (list.error instanceof ApiError && list.error.status === 403) {
    return (
      <div className="flex flex-col gap-6">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <Card className="flex flex-col gap-1">
          <p className="font-medium text-gray-900 dark:text-gray-100">{t("forbiddenTitle")}</p>
          <p className="text-sm text-gray-600 dark:text-gray-400">{t("forbiddenBody")}</p>
        </Card>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </div>
      <AdminTabs />
      <AdminContributionTabs />

      <div className="grid gap-3 sm:grid-cols-4">
        <FormField label={t("filters.company")} htmlFor="salary-company">
          <Input
            id="salary-company"
            defaultValue={filters.company}
            key={filters.company}
            onBlur={(e) => e.target.value !== filters.company && apply(withCompanyChange(filters, e.target.value))}
            onKeyDown={(e) => e.key === "Enter" && apply(withCompanyChange(filters, (e.target as HTMLInputElement).value))}
          />
        </FormField>
      </div>

      {list.isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
      {list.error && !(list.error instanceof ApiError && list.error.status === 403) && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {list.error instanceof ApiError && list.error.status === 404 ? t("disabled") : t("error")}
        </p>
      )}

      {list.data && list.data.items.length === 0 && <p className="text-sm text-gray-600 dark:text-gray-400">{t("empty")}</p>}

      {list.data && list.data.items.length > 0 && (
        <Card className="overflow-x-auto p-0">
          <table className="w-full min-w-[48rem] border-collapse text-sm">
            <thead>
              <tr className="border-b border-gray-200 text-left text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
                <th className="px-4 py-2 font-medium">{t("columns.company")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.occupation")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.net")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.years")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.period")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.author")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.submitted")}</th>
              </tr>
            </thead>
            <tbody>
              {list.data.items.map((item) => (
                <tr
                  key={item.id}
                  className="cursor-pointer border-b border-gray-100 last:border-b-0 hover:bg-gray-50 dark:border-gray-800 dark:hover:bg-gray-800/60"
                  onClick={() => {
                    setActionError(null);
                    setConfirming(false);
                    setOpen(item);
                  }}
                >
                  <td className="px-4 py-2 text-gray-900 dark:text-gray-100">{item.companyName}</td>
                  <td className="max-w-[14rem] truncate px-4 py-2 text-gray-700 dark:text-gray-300">{occupationName(item.occupation, locale)}</td>
                  <td className="px-4 py-2 tabular-nums text-gray-900 dark:text-gray-100">{formatAmount(locale, item.monthlyNetAmount, item.currency)}</td>
                  <td className="px-4 py-2 tabular-nums text-gray-700 dark:text-gray-300">{item.yearsOfExperience}</td>
                  <td className="px-4 py-2 tabular-nums text-gray-700 dark:text-gray-300">
                    {formatSalaryPeriod(item, tMine("periodOngoing")) ?? tMine("periodMissing")}
                  </td>
                  <td className="max-w-[14rem] truncate px-4 py-2 text-gray-700 dark:text-gray-300">{item.authorEmail}</td>
                  <td className="px-4 py-2 text-gray-600 dark:text-gray-400">{formatDate(item.submittedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}

      {list.data && (
        <Pagination
          page={list.data.page}
          pageSize={list.data.pageSize}
          totalCount={list.data.totalCount}
          unit="salaries"
          onPageChange={(page) => apply({ ...filters, page })}
        />
      )}

      {open && (
        <Modal
          title={t("detail.title")}
          onClose={() => setOpen(null)}
          busy={remove.isPending}
          footer={
            <>
              <Button variant="secondary" onClick={() => setOpen(null)} disabled={remove.isPending}>
                {t("detail.close")}
              </Button>
              {confirming ? (
                <Button variant="danger" disabled={remove.isPending} onClick={() => remove.mutate(open.id)}>
                  {t("detail.confirmDelete")}
                </Button>
              ) : (
                <Button variant="danger" onClick={() => setConfirming(true)}>
                  {t("detail.delete")}
                </Button>
              )}
            </>
          }
        >
          <div className="flex flex-col gap-4 text-sm">
            <div>
              <h2 className="text-lg font-semibold">{occupationName(open.occupation, locale)}</h2>
              <p className="text-gray-600 dark:text-gray-400">
                {open.companySlug ? (
                  <Link href={`/companies/${open.companySlug}`} className="underline-offset-2 hover:underline">
                    {open.companyName}
                  </Link>
                ) : (
                  open.companyName
                )}
              </p>
            </div>

            <dl className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
              <dt className="text-gray-600 dark:text-gray-400">{t("detail.net")}</dt>
              <dd className="tabular-nums text-gray-900 dark:text-gray-100">{formatAmount(locale, open.monthlyNetAmount, open.currency)}</dd>
              <dt className="text-gray-600 dark:text-gray-400">{t("detail.bonus")}</dt>
              <dd className="tabular-nums text-gray-900 dark:text-gray-100">
                {open.annualBonusAmount !== null ? formatAmount(locale, open.annualBonusAmount, open.currency) : tMine("noBonus")}
              </dd>
              <dt className="text-gray-600 dark:text-gray-400">{t("detail.years")}</dt>
              <dd className="text-gray-900 dark:text-gray-100">{tMine("years", { count: open.yearsOfExperience })}</dd>
              <dt className="text-gray-600 dark:text-gray-400">{t("detail.period")}</dt>
              <dd className="tabular-nums text-gray-900 dark:text-gray-100">{formatSalaryPeriod(open, tMine("periodOngoing")) ?? tMine("periodMissing")}</dd>
              <dt className="text-gray-600 dark:text-gray-400">{t("detail.employment")}</dt>
              <dd className="text-gray-900 dark:text-gray-100">
                {tType(open.employmentType)} · {tStatus(open.employmentStatus)}
              </dd>
              <dt className="text-gray-600 dark:text-gray-400">{t("detail.submitted")}</dt>
              <dd className="text-gray-900 dark:text-gray-100">{formatDate(open.submittedAt)}</dd>
              <dt className="text-gray-600 dark:text-gray-400">{t("detail.updated")}</dt>
              <dd className="text-gray-900 dark:text-gray-100">{formatDate(open.updatedAt)}</dd>
            </dl>

            {/* The author: admin-only, on purpose. */}
            <div className="rounded-lg bg-gray-100 p-3 text-xs dark:bg-gray-800">
              <p className="font-semibold uppercase text-gray-500 dark:text-gray-400">{t("detail.author")}</p>
              <p className="mt-1 text-gray-900 dark:text-gray-100">{open.authorEmail}</p>
            </div>

            {confirming && (
              <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-800 dark:border-red-900 dark:bg-red-950/40 dark:text-red-200">
                <p className="font-medium">{t("detail.deleteWarning")}</p>
                <p className="mt-1">{t("detail.deleteBody")}</p>
              </div>
            )}

            {actionError && (
              <p role="alert" className="text-red-600 dark:text-red-400">
                {actionError}
              </p>
            )}
          </div>
        </Modal>
      )}
    </div>
  );
}
