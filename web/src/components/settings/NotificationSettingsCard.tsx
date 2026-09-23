"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { notificationsApi } from "@/lib/api/notifications";
import { notificationCountQueryKey } from "@/hooks/useNotificationCount";
import { notificationsQueryKey } from "@/hooks/useNotifications";
import { useClientConfig } from "@/hooks/useClientConfig";
import {
  CONTRIBUTION_PREFERENCE_KEYS,
  effectiveSwitch,
  toggled,
  type ContributionPreferenceKey,
} from "@/lib/notifications/preferences";
import type { NotificationPreferences } from "@/types/api";

const preferencesQueryKey = ["notifications", "preferences"] as const;

/**
 * Account settings › Notifications (canvas "Son — Hesap Ayarları", DECISIONS.md 2026-09-23). Each
 * switch saves on its own, optimistically; a failed save puts it back and says so. A kind whose
 * feature is off in this environment is not offered — there is nothing it could tell.
 */
export function NotificationSettingsCard() {
  const t = useTranslations("settings.notifications");
  const queryClient = useQueryClient();
  const { config } = useClientConfig();
  const [error, setError] = useState<string | null>(null);
  const { data: preferences, error: loadError } = useQuery({
    queryKey: preferencesQueryKey,
    queryFn: notificationsApi.getPreferences,
  });

  const save = useMutation({
    mutationFn: notificationsApi.updatePreferences,
    onMutate: async (next: NotificationPreferences) => {
      setError(null);
      await queryClient.cancelQueries({ queryKey: preferencesQueryKey });
      const previous = queryClient.getQueryData<NotificationPreferences>(preferencesQueryKey);
      queryClient.setQueryData(preferencesQueryKey, next);
      return { previous };
    },
    onError: (_error, _next, context) => {
      if (context?.previous) queryClient.setQueryData(preferencesQueryKey, context.previous);
      setError(t("saveError"));
    },
    onSuccess: (saved) => queryClient.setQueryData(preferencesQueryKey, saved),
    // Gmail rows appear or vanish from the bell with their switch.
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: notificationsQueryKey }),
        queryClient.invalidateQueries({ queryKey: notificationCountQueryKey }),
      ]),
  });

  const offered: Record<ContributionPreferenceKey, boolean> = {
    reviewHelpful: config?.companyReviews?.enabled === true,
    salaryHelpful: config?.companySalaries?.enabled === true,
    experienceHelpful: config?.candidateExperiences?.enabled === true,
    blogCommentHelpful: config?.blog?.enabled === true,
  };

  const flip = (key: keyof NotificationPreferences) => {
    if (preferences) save.mutate(toggled(preferences, key));
  };

  const shownError = error ?? (loadError ? t("loadError") : null);

  return (
    <section id="notifications" className="scroll-mt-20 rounded-lg border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-800 dark:bg-gray-900">
      <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
      <p className="mb-4 text-sm text-gray-600 dark:text-gray-400">{t("description")}</p>
      {shownError && (
        <p className="mb-3 text-sm text-red-600 dark:text-red-400" role="alert">
          {shownError}
        </p>
      )}
      {preferences && (
        <ul className="flex flex-col">
          <SwitchRow
            label={t("contributions.label")}
            hint={t("contributions.hint")}
            on={preferences.contributions}
            onToggle={() => flip("contributions")}
          />
          {CONTRIBUTION_PREFERENCE_KEYS.filter((key) => offered[key]).map((key) => {
            const { on, locked } = effectiveSwitch(preferences, key);
            return (
              <SwitchRow
                key={key}
                label={t(`${key}.label`)}
                hint={t("dailyHint")}
                on={on}
                locked={locked}
                indent
                onToggle={() => flip(key)}
              />
            );
          })}
          <SwitchRow
            label={t("gmailUpdates.label")}
            hint={t("gmailUpdates.hint")}
            on={preferences.gmailUpdates}
            onToggle={() => flip("gmailUpdates")}
          />
        </ul>
      )}
    </section>
  );
}

function SwitchRow({
  label,
  hint,
  on,
  locked = false,
  indent = false,
  onToggle,
}: {
  label: string;
  hint: string;
  on: boolean;
  locked?: boolean;
  indent?: boolean;
  onToggle: () => void;
}) {
  return (
    <li className={`flex items-center gap-4 border-t border-gray-100 py-3 dark:border-gray-800 ${indent ? "pl-4" : ""} ${locked ? "opacity-50" : ""}`}>
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium text-gray-900 dark:text-gray-100">{label}</p>
        <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">{hint}</p>
      </div>
      <button
        type="button"
        role="switch"
        aria-checked={on}
        aria-label={label}
        disabled={locked}
        onClick={onToggle}
        className={`flex h-6 w-11 shrink-0 items-center rounded-full p-0.5 transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 disabled:cursor-not-allowed ${
          on ? "justify-end bg-blue-600" : "justify-start bg-gray-300 dark:bg-gray-700"
        }`}
      >
        <span className="h-5 w-5 rounded-full bg-white shadow" />
      </button>
    </li>
  );
}
