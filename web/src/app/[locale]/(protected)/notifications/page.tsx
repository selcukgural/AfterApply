"use client";

import { useEffect, useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { useQueryClient } from "@tanstack/react-query";
import { notificationsApi } from "@/lib/api/notifications";
import { ApiError } from "@/lib/api/httpClient";
import type { EmailNotificationResponse } from "@/types/api";
import { StatusBadge } from "@/components/applications/StatusBadge";
import { notificationCountQueryKey } from "@/hooks/useNotificationCount";

export default function NotificationsPage() {
  const t = useTranslations("notifications");
  const tCommon = useTranslations("common");
  const locale = useLocale();
  const queryClient = useQueryClient();
  const [notifications, setNotifications] = useState<EmailNotificationResponse[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    notificationsApi
      .getNotifications()
      .then((data) => {
        setNotifications(data);
        // The user has now seen everything currently in the list — mark it all read in one
        // bulk call and clear the nav badge, rather than tracking per-row visibility.
        return notificationsApi.markAllRead();
      })
      .then(() => queryClient.invalidateQueries({ queryKey: notificationCountQueryKey }))
      .catch((err) => setError(err instanceof ApiError ? err.message : t("loadError")));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>

      {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}

      {notifications === null ? (
        <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>
      ) : notifications.length === 0 ? (
        <p className="text-sm text-gray-500 dark:text-gray-400">{t("empty")}</p>
      ) : (
        <ul className="flex flex-col gap-4">
          {notifications.map((n) => (
            <li key={n.id} className="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-4 shadow-sm">
              <div className="mb-2 flex items-start justify-between gap-3">
                <div>
                  <p className="font-medium text-gray-900 dark:text-gray-100">
                    {n.companyName} — {n.jobTitle}
                  </p>
                  <p className="text-xs text-gray-500 dark:text-gray-400">
                    {new Date(n.createdAt).toLocaleString(locale)}
                  </p>
                </div>
                {n.status && <StatusBadge status={n.status} />}
              </div>
              <div className="mb-2 flex flex-wrap gap-2">
                <span
                  className={
                    n.wasAutoApplied
                      ? "inline-block rounded-full bg-blue-100 px-2.5 py-0.5 text-xs font-medium text-blue-700 dark:bg-blue-900/40 dark:text-blue-300"
                      : "inline-block rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium text-gray-600 dark:bg-gray-800 dark:text-gray-400"
                  }
                >
                  {n.wasAutoApplied ? t("autoApplied") : t("confirmed")}
                </span>
                {n.isNewApplicationSuggestion && (
                  <span className="inline-block rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-medium text-amber-700 dark:bg-amber-900/40 dark:text-amber-300">
                    {t("newJobBadge")}
                  </span>
                )}
              </div>
              {n.wasAutoApplied && (
                <p className="text-xs text-gray-500 dark:text-gray-400">
                  {t("confidence")}: {Math.round(n.confidenceScore * 100)}%
                  {n.matchType && ` · ${t(`matchType.${n.matchType}`)}`}
                </p>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
