"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { PRO_QUERY_KEYS } from "@/components/pro/useProAccess";
import { buttonClassName } from "@/components/ui/Button";
import { useClientConfig } from "@/hooks/useClientConfig";
import { jobSourcesApi } from "@/lib/api/jobSources";
import { paymentsApi } from "@/lib/api/payments";
import { formatMinor } from "@/lib/payments/money";
import { canSeeProNav } from "@/lib/payments/proNav";
import { WeeklyJobsSampleList } from "@/components/weeklyJobs/WeeklyJobsSampleList";
import { WEEKLY_JOBS_QUERY_KEYS } from "@/lib/weeklyJobs/queryKeys";
import type { JobSourceStatusResponse } from "@/types/api";

/**
 * The dashboard's announcement of the paid weekly postings — the "2B1" card from the 2026-09-15
 * design canvas: the app's own white card with the landing hero's brand glow, one headline, the
 * three things the feature does as chips, and a sample of what a delivered week looks like.
 *
 * Shown only while there is something to announce to this account: the feature and the plan are
 * on (server flags), the account is not Pro, and the card has not been closed. Closing it is
 * recorded on the account (POST /announcement/dismiss), so it stays closed on every device; the
 * price is the live monthly plan, never a literal.
 */
export function WeeklyJobsAnnouncement() {
  const t = useTranslations("dashboard.weeklyJobsAnnouncement");
  const locale = useLocale();
  const queryClient = useQueryClient();
  const { config, isLoaded } = useClientConfig();
  const onSale = isLoaded && canSeeProNav(config);

  const status = useQuery({
    queryKey: WEEKLY_JOBS_QUERY_KEYS.status,
    queryFn: jobSourcesApi.getStatus,
    enabled: onSale,
    staleTime: 5 * 60_000,
  });
  const plans = useQuery({
    queryKey: PRO_QUERY_KEYS.plans,
    queryFn: paymentsApi.getPlans,
    enabled: onSale && status.data?.isPro === false && !status.data.announcementDismissed,
  });

  const dismiss = useMutation({
    mutationFn: jobSourcesApi.dismissAnnouncement,
    // Optimistic: the card leaves on the click, and the status cache says so from then on.
    onMutate: () => {
      queryClient.setQueryData<JobSourceStatusResponse>(WEEKLY_JOBS_QUERY_KEYS.status, (current) =>
        current ? { ...current, announcementDismissed: true } : current,
      );
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: WEEKLY_JOBS_QUERY_KEYS.status }),
  });

  const monthly = plans.data?.plans.find((plan) => plan.plan === "Monthly");
  if (!onSale || !status.data || status.data.isPro || status.data.announcementDismissed || !monthly || !plans.data) {
    return null;
  }

  const price = formatMinor(monthly.amountMinor, plans.data.currency, locale);
  const samples = [
    { title: t("sample1Title"), company: t("sample1Company"), score: 92 },
    { title: t("sample2Title"), company: t("sample2Company"), score: 81 },
    { title: t("sample3Title"), company: t("sample3Company"), score: 64 },
  ];

  return (
    <section
      aria-label={t("title")}
      className="aa-card-glow relative grid gap-6 overflow-hidden rounded-xl border border-gray-200 bg-white px-7 py-6 lg:grid-cols-[minmax(0,1fr)_340px] lg:gap-8 dark:border-gray-800 dark:bg-gray-900"
    >
      <button
        type="button"
        onClick={() => dismiss.mutate()}
        aria-label={t("close")}
        className="absolute top-3 right-3 flex h-8 w-8 items-center justify-center rounded-md text-gray-400 hover:bg-gray-100 hover:text-gray-700 dark:hover:bg-gray-800 dark:hover:text-gray-200"
      >
        <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" aria-hidden="true">
          <path d="M6 6l12 12M18 6L6 18" />
        </svg>
      </button>

      <div className="flex flex-col justify-center gap-3">
        <span className="inline-flex w-fit items-center rounded-full bg-gradient-to-r from-[#1C39B7] to-[#15AAB7] px-2.5 py-0.5 text-xs font-semibold tracking-wide text-white">
          {t("badge")}
        </span>
        <h2 className="max-w-[30ch] text-[22px] leading-7 font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
        <p className="max-w-[58ch] text-sm text-gray-700 dark:text-gray-300">{t("body")}</p>
        <ul className="flex flex-wrap gap-2 text-xs text-gray-700 dark:text-gray-300">
          {(["chip1", "chip2", "chip3"] as const).map((key) => (
            <li key={key} className="inline-flex items-center gap-1.5 rounded-full bg-gray-100 px-2.5 py-1 dark:bg-gray-800">
              <svg viewBox="0 0 24 24" className="h-3.5 w-3.5 text-good" fill="none" stroke="currentColor" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="M5 12.5l4.5 4.5L19 7" />
              </svg>
              {t(key)}
            </li>
          ))}
        </ul>
        <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-2">
          <Link href="/pro" className={buttonClassName("primary")}>
            {t("cta", { price })}
          </Link>
          <Link href="/help/weekly-jobs" className="text-sm font-medium text-accent-ink hover:underline">
            {t("how")} →
          </Link>
          <span className="text-xs text-gray-500 dark:text-gray-400">{t("noAutoRenew")}</span>
        </div>
      </div>

      <div className="flex flex-col justify-center">
        <WeeklyJobsSampleList label={t("sampleLabel")} samples={samples} />
      </div>
    </section>
  );
}
