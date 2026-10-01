"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { authApi } from "@/lib/api/auth";
import { authStore } from "@/lib/api/authStore";
import { ApiError } from "@/lib/api/httpClient";
import { useAuth } from "@/lib/auth/AuthContext";

/**
 * Account settings › Privacy (DECISIONS.md 2026-10-01): whether this account's applications count
 * toward the anonymous response figures — the sector table and a company's "Response" tab. On by
 * default; it is the objection the "legitimate interest" basis owes the user, so it is a switch
 * saved the moment it is flipped, not something buried in the privacy text.
 */
export function PrivacySettingsCard() {
  const t = useTranslations("settings.privacy");
  const { user } = useAuth();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!user) return null;
  // Missing on a profile cached before the field existed: that account was contributing.
  const on = user.contributesToAggregates !== false;

  const toggle = async () => {
    setError(null);
    setPending(true);
    try {
      authStore.updateUser(await authApi.setAggregateContribution(!on));
    } catch (err) {
      setError(err instanceof ApiError && err.message ? err.message : t("saveError"));
    } finally {
      setPending(false);
    }
  };

  return (
    <section id="privacy" className="scroll-mt-20 rounded-lg border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-800 dark:bg-gray-900">
      <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
      <p className="mb-4 text-sm text-gray-600 dark:text-gray-400">
        {t("description")}{" "}
        <Link href="/privacy#aggregates" className="text-accent-ink underline-offset-2 hover:underline">
          {t("link")}
        </Link>
      </p>
      {error && (
        <p className="mb-3 text-sm text-red-600 dark:text-red-400" role="alert">
          {error}
        </p>
      )}
      <div className="flex items-center gap-4 border-t border-gray-100 py-3 dark:border-gray-800">
        <div className="min-w-0 flex-1">
          <p id="aggregate-contribution-label" className="text-sm font-medium text-gray-900 dark:text-gray-100">
            {t("contribute.label")}
          </p>
          <p id="aggregate-contribution-hint" className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">
            {on ? t("contribute.hintOn") : t("contribute.hintOff")}
          </p>
        </div>
        <button
          type="button"
          role="switch"
          aria-checked={on}
          aria-labelledby="aggregate-contribution-label"
          aria-describedby="aggregate-contribution-hint"
          disabled={pending}
          onClick={toggle}
          className={`flex h-6 w-11 shrink-0 items-center rounded-full p-0.5 transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 disabled:opacity-60 ${
            on ? "justify-end bg-blue-600" : "justify-start bg-gray-300 dark:bg-gray-700"
          }`}
        >
          <span className="h-5 w-5 rounded-full bg-white shadow" />
        </button>
      </div>
    </section>
  );
}
