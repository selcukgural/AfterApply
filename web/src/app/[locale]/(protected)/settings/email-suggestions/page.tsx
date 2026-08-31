"use client";

import { useEffect, useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { emailIntegrationsApi } from "@/lib/api/emailIntegrations";
import { ApiError } from "@/lib/api/httpClient";
import type { EmailSuggestionResponse } from "@/types/api";
import { StatusBadge } from "@/components/applications/StatusBadge";
import { Button } from "@/components/ui/Button";

export default function EmailSuggestionsPage() {
  const t = useTranslations("emailSuggestions");
  const tCommon = useTranslations("common");
  const locale = useLocale();
  const [suggestions, setSuggestions] = useState<EmailSuggestionResponse[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pendingActionId, setPendingActionId] = useState<string | null>(null);

  useEffect(() => {
    emailIntegrationsApi
      .getPendingSuggestions()
      .then(setSuggestions)
      .catch((err) => setError(err instanceof ApiError ? err.message : t("loadError")));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleConfirm = async (id: string) => {
    setPendingActionId(id);
    setError(null);
    try {
      await emailIntegrationsApi.confirmSuggestion(id);
      setSuggestions((prev) => prev?.filter((s) => s.id !== id) ?? prev);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t("confirmError"));
    } finally {
      setPendingActionId(null);
    }
  };

  const handleDismiss = async (id: string) => {
    setPendingActionId(id);
    setError(null);
    try {
      await emailIntegrationsApi.dismissSuggestion(id);
      setSuggestions((prev) => prev?.filter((s) => s.id !== id) ?? prev);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t("dismissError"));
    } finally {
      setPendingActionId(null);
    }
  };

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <div>
        <Link href="/settings" className="text-sm text-blue-600 dark:text-blue-400 hover:underline">
          {t("back")}
        </Link>
        <h1 className="mt-2 text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
      </div>

      {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}

      {suggestions === null ? (
        <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>
      ) : suggestions.length === 0 ? (
        <p className="text-sm text-gray-500 dark:text-gray-400">{t("empty")}</p>
      ) : (
        <ul className="flex flex-col gap-4">
          {suggestions.map((s) => (
            <li key={s.id} className="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-4 shadow-sm">
              <div className="mb-2 flex items-start justify-between gap-3">
                <div>
                  <p className="font-medium text-gray-900 dark:text-gray-100">
                    {s.companyName} — {s.jobTitle}
                  </p>
                  <p className="text-xs text-gray-500 dark:text-gray-400">
                    {new Date(s.emailReceivedAt).toLocaleString(locale)} · {t("confidence")}:{" "}
                    {Math.round(s.confidenceScore * 100)}%
                  </p>
                </div>
                {s.suggestedStatus ? (
                  <StatusBadge status={s.suggestedStatus} />
                ) : (
                  <span className="inline-block rounded-full bg-gray-100 dark:bg-gray-800 px-2.5 py-0.5 text-xs font-medium text-gray-600 dark:text-gray-400">
                    {t("stillPending")}
                  </span>
                )}
              </div>
              {s.isNewApplicationSuggestion && (
                <span className="mb-2 inline-block rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-medium text-amber-700 dark:bg-amber-900/40 dark:text-amber-300">
                  {t("newJobBadge")}
                </span>
              )}
              {s.location && (
                <p className="mb-1 text-xs text-gray-500 dark:text-gray-400">
                  {t("location")}: {s.location}
                </p>
              )}
              <p className="mb-1 text-sm font-medium text-gray-800 dark:text-gray-200">{s.subject}</p>
              <p className="mb-3 text-sm text-gray-600 dark:text-gray-400">{s.description ?? s.snippet}</p>
              <div className="flex gap-3">
                {s.suggestedStatus && (
                  <Button onClick={() => handleConfirm(s.id)} disabled={pendingActionId === s.id}>
                    {t("confirm")}
                  </Button>
                )}
                <Button
                  variant="secondary"
                  onClick={() => handleDismiss(s.id)}
                  disabled={pendingActionId === s.id}
                >
                  {t("dismiss")}
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
