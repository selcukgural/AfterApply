"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import type { FeatureFlag, FeatureFlagChangeResponse } from "@/types/api";
import { Link } from "@/i18n/navigation";
import { featureFlagsApi } from "@/lib/api/featureFlags";
import { ApiError } from "@/lib/api/httpClient";
import { FEATURE_FLAGS } from "@/lib/admin/featureFlags";
import { AdminTabs } from "@/components/admin/AdminTabs";
import { Card } from "@/components/dashboard/Card";
import { Select } from "@/components/ui/Select";
import { DangerBanner } from "@/components/admin/flags/DangerBanner";
import { FEATURE_FLAG_HISTORY_QUERY_KEY, FEATURE_FLAGS_QUERY_KEY } from "@/components/admin/flags/FlagChangeDialog";

const LIMIT = 200;

/**
 * Every flag switch, newest first: who, when, from which state to which, and why. The connection's
 * IP is stored with each change and deliberately absent here — the API never returns it
 * (DECISIONS.md 2026-09-14 and 2026-09-27).
 */
export default function AdminFlagHistoryPage() {
  const t = useTranslations("adminFlags");
  const locale = useLocale();
  const [filter, setFilter] = useState<FeatureFlag | "">("");
  const retry = (failureCount: number, error: unknown) => !(error instanceof ApiError && error.status === 403) && failureCount < 2;

  const { data: flags } = useQuery({ queryKey: FEATURE_FLAGS_QUERY_KEY, queryFn: featureFlagsApi.list, retry });
  const { data, isLoading, error } = useQuery({
    queryKey: [...FEATURE_FLAG_HISTORY_QUERY_KEY, filter || "all", LIMIT],
    queryFn: () => featureFlagsApi.history(filter || undefined, LIMIT),
    retry,
  });

  const titleOf = new Map(flags?.map((flag) => [flag.flag, flag.title]) ?? []);
  const stateWord = (on: boolean) => t(on ? "stateWord.on" : "stateWord.off");
  const when = (iso: string) => new Date(iso).toLocaleString(locale, { dateStyle: "medium", timeStyle: "short" });
  const kindOf = (row: FeatureFlagChangeResponse) =>
    row.enabled === null ? t("history.reset") : t("history.setTo", { state: stateWord(row.enabled) });

  return (
    <div className="flex flex-col gap-6">
      <DangerBanner />
      <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("history.title")}</h1>
      <AdminTabs />

      {error instanceof ApiError && error.status === 403 ? (
        <Card className="flex flex-col gap-1">
          <p className="font-medium text-gray-900 dark:text-gray-100">{t("forbiddenTitle")}</p>
          <p className="text-sm text-gray-600 dark:text-gray-400">{t("forbiddenBody")}</p>
        </Card>
      ) : (
        <>
          <div className="flex flex-wrap items-end gap-4">
            <div className="flex min-w-0 flex-1 flex-col gap-1">
              <Link href="/admin/flags" className="w-fit text-sm font-medium text-accent-ink hover:underline">
                ← {t("title")}
              </Link>
              <p className="text-sm text-gray-600 dark:text-gray-400">{t("history.subtitle")}</p>
            </div>
            <label className="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300">
              {t("history.filter")}
              <Select value={filter} onChange={(event) => setFilter(event.target.value as FeatureFlag | "")} className="w-auto">
                <option value="">{t("history.all")}</option>
                {FEATURE_FLAGS.map((flag) => (
                  <option key={flag} value={flag}>
                    {titleOf.get(flag) ?? flag}
                  </option>
                ))}
              </Select>
            </label>
          </div>

          {isLoading ? <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p> : null}
          {error ? (
            <p role="alert" className="text-sm text-red-700 dark:text-red-400">
              {t("history.error")}
            </p>
          ) : null}
          {data && data.length === 0 ? <p className="text-sm text-gray-600 dark:text-gray-400">{t("history.empty")}</p> : null}
          {data && data.length > 0 ? (
            <div className="overflow-hidden rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
              <div className="hidden grid-cols-[12rem_14rem_11rem_minmax(0,1fr)] gap-4 border-b border-gray-200 bg-gray-50 px-5 py-3 text-xs font-semibold tracking-wide text-gray-500 uppercase lg:grid dark:border-gray-800 dark:bg-gray-950 dark:text-gray-400">
                <span>
                  {t("history.columns.at")} · {t("history.columns.by")}
                </span>
                <span>{t("history.columns.flag")}</span>
                <span>{t("history.columns.change")}</span>
                <span>{t("history.columns.reason")}</span>
              </div>
              <ul>
                {data.map((row) => (
                  <li
                    key={row.id}
                    className="grid grid-cols-1 gap-1.5 border-t border-gray-100 px-5 py-3.5 text-sm first:border-t-0 lg:grid-cols-[12rem_14rem_11rem_minmax(0,1fr)] lg:gap-4 dark:border-gray-800"
                  >
                    <span className="flex flex-col">
                      <span className="text-gray-900 dark:text-gray-100">{when(row.changedAt)}</span>
                      <span className="text-xs break-all text-gray-500 dark:text-gray-400">{row.changedBy ?? t("history.deletedAccount")}</span>
                    </span>
                    <span className="flex flex-col">
                      <span className="font-medium text-gray-900 dark:text-gray-100">{titleOf.get(row.flag) ?? row.flag}</span>
                      <code className="font-mono text-xs text-gray-500 dark:text-gray-400">{row.flag}</code>
                    </span>
                    <span className="flex flex-col">
                      <span className="text-gray-900 dark:text-gray-100">
                        {t(row.wasOn ? "state.on" : "state.off")} → {t(row.isOn ? "state.on" : "state.off")}
                      </span>
                      <span className="text-xs text-gray-500 dark:text-gray-400">{kindOf(row)}</span>
                    </span>
                    <span className="break-words text-gray-700 dark:text-gray-300">{row.reason}</span>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </>
      )}
    </div>
  );
}
