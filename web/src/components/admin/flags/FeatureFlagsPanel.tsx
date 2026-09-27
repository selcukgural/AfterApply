"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import type { FeatureFlag, FeatureFlagResponse } from "@/types/api";
import { Link } from "@/i18n/navigation";
import { featureFlagsApi } from "@/lib/api/featureFlags";
import { ApiError } from "@/lib/api/httpClient";
import { type ChangeKind, FLAG_GROUPS, canReset, isBlocked, toggleKind } from "@/lib/admin/featureFlags";
import { Card } from "@/components/dashboard/Card";
import { buttonClassName } from "@/components/ui/Button";
import { FEATURE_FLAG_HISTORY_QUERY_KEY, FEATURE_FLAGS_QUERY_KEY, FlagChangeDialog } from "./FlagChangeDialog";
import { FlagCouplings, FlagStatePill, WarningIcon } from "./FlagBits";

const noRetryOn403 = (failureCount: number, error: unknown) =>
  !(error instanceof ApiError && error.status === 403) && failureCount < 2;

/**
 * The runtime feature flags (canvas "Özellik bayrakları paneli", variant C, 2026-09-27): the flags
 * on the left in four groups, the chosen one's full texts on the right — what it does, what "off"
 * means, what moves with it, its last changes — and under them its own danger-zone box. Nothing
 * here switches anything by itself; every action opens the two-step FlagChangeDialog.
 *
 * On a phone the list and the detail take turns: a tap opens the detail, "Flags" goes back.
 */
export function FeatureFlagsPanel() {
  const t = useTranslations("adminFlags");
  const [selected, setSelected] = useState<FeatureFlag>("Board");
  const [mobileDetail, setMobileDetail] = useState(false);
  const [change, setChange] = useState<ChangeKind | null>(null);

  const { data, isLoading, error } = useQuery({
    queryKey: FEATURE_FLAGS_QUERY_KEY,
    queryFn: featureFlagsApi.list,
    retry: noRetryOn403,
  });

  if (error instanceof ApiError && error.status === 403) {
    return (
      <Card className="flex flex-col gap-1">
        <p className="font-medium text-gray-900 dark:text-gray-100">{t("forbiddenTitle")}</p>
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("forbiddenBody")}</p>
      </Card>
    );
  }

  if (isLoading) {
    return <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>;
  }

  if (!data) {
    return <p className="text-sm text-red-700 dark:text-red-400">{t("error")}</p>;
  }

  const byFlag = new Map(data.map((flag) => [flag.flag, flag]));
  const current = byFlag.get(selected) ?? data[0];
  const overridden = data.filter((flag) => flag.override !== null).length;

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-end gap-3">
        <p className="min-w-0 flex-1 text-sm text-gray-600 dark:text-gray-400">{t("summary", { total: data.length, overridden })}</p>
        <Link href="/admin/flags/history" className="text-sm font-medium text-accent-ink hover:underline">
          {t("historyLink")} →
        </Link>
      </div>

      <div className="flex flex-col gap-6 lg:flex-row lg:items-start">
        <nav
          aria-label={t("listLabel")}
          className={`${mobileDetail ? "hidden lg:block" : "block"} overflow-hidden rounded-xl border border-gray-200 bg-white lg:w-[380px] lg:shrink-0 dark:border-gray-800 dark:bg-gray-900`}
        >
          {FLAG_GROUPS.map((group) => (
            <div key={group.key}>
              <p className="px-4 pt-3 pb-1.5 text-xs font-semibold tracking-wide text-gray-500 uppercase dark:text-gray-400">{t(`groups.${group.key}`)}</p>
              {group.flags.map((key) => {
                const flag = byFlag.get(key);
                if (!flag) {
                  return null;
                }
                const isSelected = flag.flag === current.flag;
                return (
                  <button
                    key={key}
                    type="button"
                    aria-current={isSelected ? "true" : undefined}
                    onClick={() => {
                      setSelected(key);
                      setMobileDetail(true);
                      // On a phone the list gives way to the detail; start the reader at its top
                      // rather than wherever in the list the tap happened.
                      if (!window.matchMedia("(min-width: 1024px)").matches) {
                        window.requestAnimationFrame(() => document.getElementById("flag-detail-title")?.scrollIntoView({ block: "start" }));
                      }
                    }}
                    className={`flex min-h-11 w-full items-center gap-2.5 border-l-[3px] px-4 text-left text-sm ${
                      isSelected
                        ? "border-accent bg-accent-wash font-semibold text-gray-900 dark:text-gray-100"
                        : "border-transparent text-gray-900 hover:bg-gray-50 dark:text-gray-100 dark:hover:bg-gray-800"
                    }`}
                  >
                    <span aria-hidden="true" className={`h-2.5 w-2.5 shrink-0 rounded-full ${flag.enabled ? "bg-green-600" : "bg-gray-400"}`} />
                    <span className="flex-1">{flag.title}</span>
                    {flag.override !== null && (
                      <span className="rounded-full bg-orange-100 px-1.5 text-[11px] font-semibold text-orange-900 dark:bg-orange-950 dark:text-orange-300">
                        {t("panelBadge")}
                      </span>
                    )}
                    <span className="w-12 text-right text-xs font-normal text-gray-500 dark:text-gray-400">
                      {t(flag.enabled ? "state.on" : "state.off")}
                    </span>
                  </button>
                );
              })}
            </div>
          ))}
        </nav>

        <div className={`${mobileDetail ? "flex" : "hidden lg:flex"} min-w-0 flex-1 flex-col gap-4`}>
          <button
            type="button"
            onClick={() => setMobileDetail(false)}
            className="flex min-h-11 w-fit items-center text-sm font-medium text-accent-ink lg:hidden"
          >
            ← {t("back")}
          </button>
          <FlagDetail flag={current} />
          <DangerZone flag={current} onChange={setChange} />
        </div>
      </div>

      {change && <FlagChangeDialog key={`${current.flag}-${change}`} flag={current} kind={change} onClose={() => setChange(null)} />}
    </div>
  );
}

function FlagDetail({ flag }: { flag: FeatureFlagResponse }) {
  const t = useTranslations("adminFlags");
  const locale = useLocale();
  const stateWord = (on: boolean) => t(on ? "stateWord.on" : "stateWord.off");
  const { data: recent } = useQuery({
    queryKey: [...FEATURE_FLAG_HISTORY_QUERY_KEY, flag.flag, 3],
    queryFn: () => featureFlagsApi.history(flag.flag, 3),
    retry: noRetryOn403,
  });
  const when = (iso: string) => new Date(iso).toLocaleString(locale, { dateStyle: "medium", timeStyle: "short" });

  return (
    <section aria-labelledby="flag-detail-title" className="flex flex-col gap-5 rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex flex-wrap items-start gap-4">
        <div className="flex min-w-0 flex-1 flex-col gap-2">
          <h3 id="flag-detail-title" className="scroll-mt-24 text-xl font-semibold text-gray-900 dark:text-gray-100">
            {flag.title}
          </h3>
          <div className="flex flex-wrap items-center gap-2">
            <code className="rounded bg-gray-100 px-1.5 py-0.5 font-mono text-xs text-gray-600 dark:bg-gray-800 dark:text-gray-400">{flag.flag}</code>
            <FlagCouplings couplings={flag.couplings} />
          </div>
        </div>
        <div className="flex flex-col items-end gap-1">
          <FlagStatePill on={flag.enabled} />
          <span className="text-xs text-gray-500 dark:text-gray-400">
            {flag.override === null ? t("source.default") : t("source.override", { state: stateWord(flag.default) })}
          </span>
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <p className="text-sm font-semibold text-gray-700 dark:text-gray-300">{t("sections.description")}</p>
        <p className="text-sm leading-relaxed text-gray-700 dark:text-gray-300">{flag.description}</p>
      </div>
      <div className="flex flex-col gap-1.5">
        <p className="text-sm font-semibold text-gray-700 dark:text-gray-300">{t("sections.whenOff")}</p>
        <p className="text-sm leading-relaxed text-gray-700 dark:text-gray-300">{flag.whenOff}</p>
      </div>
      {flag.notes && (
        <div className="flex flex-col gap-1.5 rounded-md border border-orange-200 bg-orange-50 px-3.5 py-3 dark:border-orange-900 dark:bg-orange-950/30">
          <p className="text-sm font-semibold text-orange-900 dark:text-orange-300">{t("sections.notes")}</p>
          <p className="text-sm leading-relaxed text-orange-950 dark:text-orange-200">{flag.notes}</p>
        </div>
      )}
      {flag.missingPrerequisite && (
        <div className="flex flex-col gap-1 rounded-md border border-gray-300 bg-gray-50 px-3.5 py-3 dark:border-gray-700 dark:bg-gray-950">
          <p className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("missing.title")}</p>
          <p className="text-sm text-gray-700 dark:text-gray-300">
            {t.has(`missing.${flag.missingPrerequisite}`)
              ? t(`missing.${flag.missingPrerequisite}`)
              : t("missing.unknown", { code: flag.missingPrerequisite })}
          </p>
        </div>
      )}

      <div className="flex flex-col gap-2">
        <p className="text-sm font-semibold text-gray-700 dark:text-gray-300">{t("sections.recent")}</p>
        {recent && recent.length > 0 ? (
          <ul className="flex flex-col gap-2">
            {recent.map((row) => (
              <li key={row.id} className="text-sm leading-relaxed text-gray-700 dark:text-gray-300">
                {when(row.changedAt)} · {t(row.wasOn ? "state.on" : "state.off")} → {t(row.isOn ? "state.on" : "state.off")} ·{" "}
                {row.changedBy ?? t("history.deletedAccount")}
                <br />
                <span className="text-gray-500 dark:text-gray-400">“{row.reason}”</span>
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("sections.neverChanged")}</p>
        )}
        <Link href="/admin/flags/history" className="w-fit text-sm text-accent-ink hover:underline">
          {t("sections.allHistory")} →
        </Link>
      </div>
    </section>
  );
}

function DangerZone({ flag, onChange }: { flag: FeatureFlagResponse; onChange: (kind: ChangeKind) => void }) {
  const t = useTranslations("adminFlags");
  const toggle = toggleKind(flag);
  const blocked = isBlocked(flag, toggle);
  const stateWord = (on: boolean) => t(on ? "stateWord.on" : "stateWord.off");

  return (
    <section aria-labelledby="flag-danger-title" className="overflow-hidden rounded-xl border-2 border-red-600 bg-white dark:border-red-700 dark:bg-gray-900">
      <h3
        id="flag-danger-title"
        className="flex items-center gap-2 border-b border-red-200 bg-red-50 px-5 py-3 text-sm font-bold tracking-wide text-red-800 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300"
      >
        <WarningIcon className="h-[18px] w-[18px]" />
        {t("danger.title")}
      </h3>
      <div className="flex flex-col gap-3 px-5 py-4 sm:flex-row sm:items-center sm:gap-5">
        <div className="flex flex-1 flex-col gap-1">
          <p className="text-[15px] font-semibold text-gray-900 dark:text-gray-100">{t(toggle === "turnOff" ? "danger.turnOff" : "danger.turnOn")}</p>
          <p className="text-sm leading-relaxed text-gray-600 dark:text-gray-400">{t("danger.actionBody")}</p>
        </div>
        <button
          type="button"
          onClick={() => onChange(toggle)}
          disabled={blocked}
          className={buttonClassName("danger", "min-h-11 whitespace-nowrap")}
        >
          {t(toggle === "turnOff" ? "danger.turnOffButton" : "danger.turnOnButton")}
        </button>
      </div>
      <div className="flex flex-col gap-3 border-t border-red-100 px-5 py-4 sm:flex-row sm:items-center sm:gap-5 dark:border-red-950">
        <div className="flex flex-1 flex-col gap-1">
          <p className="text-[15px] font-semibold text-gray-900 dark:text-gray-100">{t("danger.resetTitle")}</p>
          <p className="text-sm leading-relaxed text-gray-600 dark:text-gray-400">
            {canReset(flag) ? t("danger.resetBody", { state: stateWord(flag.default) }) : t("danger.resetNone")}
          </p>
        </div>
        <button
          type="button"
          onClick={() => onChange("reset")}
          disabled={!canReset(flag)}
          className="min-h-11 rounded-md border border-red-300 bg-white px-4 py-2 text-sm font-medium whitespace-nowrap text-red-700 hover:bg-red-50 disabled:cursor-not-allowed disabled:border-gray-200 disabled:bg-gray-100 disabled:text-gray-500 dark:border-red-800 dark:bg-gray-900 dark:text-red-300 dark:hover:bg-red-950/40 dark:disabled:border-gray-700 dark:disabled:bg-gray-800 dark:disabled:text-gray-500"
        >
          {t("danger.resetButton")}
        </button>
      </div>
    </section>
  );
}
