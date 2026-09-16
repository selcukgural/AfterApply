"use client";

import { type FormEvent, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import type { JobSourceProfileResponse } from "@/types/api";
import { jobSourcesApi } from "@/lib/api/jobSources";
import { ApiError } from "@/lib/api/httpClient";
import { Button } from "@/components/ui/Button";
import { Checkbox } from "@/components/ui/Checkbox";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { WEEKLY_JOBS_QUERY_KEYS } from "@/lib/weeklyJobs/queryKeys";

const MAX_TITLES = 3;

interface CriteriaFormProps {
  /** Null on first save; the consent box is then required and unticked. */
  profile: JobSourceProfileResponse | null;
}

// Re-exported for the pages that imported the keys from here before they moved.
export { WEEKLY_JOBS_QUERY_KEYS };

/**
 * The criteria the weekly sweep searches with, and the one consent this feature needs that the
 * CV upload did not cover: the default CV's text going to the scoring model. The box is never
 * pre-ticked on a first save; once recorded, the server keeps the original moment and the form
 * shows it instead of asking again.
 */
export function CriteriaForm({ profile }: CriteriaFormProps) {
  const t = useTranslations("weeklyJobs.criteria");
  const locale = useLocale();
  const router = useRouter();
  const queryClient = useQueryClient();

  const [titles, setTitles] = useState<string[]>(() => {
    const given = profile?.titles ?? [];
    return [...given, ...Array(Math.max(0, MAX_TITLES - given.length)).fill("")].slice(0, MAX_TITLES);
  });
  const [location, setLocation] = useState(profile?.location ?? "");
  const [remoteOnly, setRemoteOnly] = useState(profile?.remoteOnly ?? false);
  const [emailDigest, setEmailDigest] = useState(profile?.emailDigest ?? true);
  const [minScore, setMinScore] = useState(profile?.minScore ?? 60);
  const [consent, setConsent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [consentError, setConsentError] = useState<string | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  const hasConsent = profile?.aiScoringConsentAcceptedAt != null;

  const save = useMutation({
    mutationFn: () =>
      jobSourcesApi.upsertProfile({
        titles: titles.map((title) => title.trim()).filter((title) => title.length > 0),
        location: location.trim(),
        remoteOnly,
        enabled: true,
        minScore,
        acceptAiScoring: consent,
        emailDigest,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["weeklyJobs"] });
      router.push("/weekly-jobs");
    },
    onError: (mutationError) => setError(mutationError instanceof ApiError ? mutationError.message : t("saveError")),
  });

  const remove = useMutation({
    mutationFn: () => jobSourcesApi.deleteProfile(),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["weeklyJobs"] });
      router.push("/weekly-jobs");
    },
    onError: (mutationError) => setError(mutationError instanceof ApiError ? mutationError.message : t("saveError")),
  });

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setConsentError(null);
    if (!hasConsent && !consent) {
      setConsentError(t("consentRequired"));
      return;
    }
    save.mutate();
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4 rounded-lg border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-gray-900">
      <fieldset className="flex flex-col gap-1">
        <legend className="text-sm font-medium text-gray-700 dark:text-gray-300">
          {t("titles")} <span className="font-normal text-gray-500 dark:text-gray-400">{t("titlesHint", { max: MAX_TITLES })}</span>
        </legend>
        <div className="mt-1 flex flex-col gap-2">
          {titles.map((title, index) => (
            <Input
              key={index}
              aria-label={t("titleAria", { n: index + 1 })}
              value={title}
              placeholder={index === 0 ? t("titlePlaceholder") : ""}
              maxLength={100}
              required={index === 0}
              onChange={(event) => setTitles(titles.map((current, i) => (i === index ? event.target.value : current)))}
            />
          ))}
        </div>
        <p className="text-xs text-gray-500 dark:text-gray-400">{t("titlesHelp")}</p>
      </fieldset>

      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label={t("location")} htmlFor="weekly-jobs-location">
          <Input
            id="weekly-jobs-location"
            value={location}
            maxLength={100}
            required
            onChange={(event) => setLocation(event.target.value)}
          />
        </FormField>
        <FormField label={t("minScore")} htmlFor="weekly-jobs-min-score">
          <div className="flex items-center gap-3">
            <input
              id="weekly-jobs-min-score"
              type="range"
              min={0}
              max={100}
              step={5}
              value={minScore}
              onChange={(event) => setMinScore(Number(event.target.value))}
              className="w-full accent-accent"
            />
            <span className="w-10 text-right text-sm font-medium text-gray-900 dark:text-gray-100">%{minScore}</span>
          </div>
          <p className="text-xs text-gray-500 dark:text-gray-400">{t("minScoreHelp")}</p>
        </FormField>
      </div>

      <Checkbox
        id="weekly-jobs-remote"
        label={t("remoteOnly")}
        checked={remoteOnly}
        onChange={(event) => setRemoteOnly(event.target.checked)}
      />
      <Checkbox
        id="weekly-jobs-digest"
        label={t("emailDigest")}
        checked={emailDigest}
        onChange={(event) => setEmailDigest(event.target.checked)}
      />

      <div className="flex flex-col gap-2 border-t border-gray-100 pt-4 dark:border-gray-800">
        {hasConsent ? (
          <p className="text-sm text-gray-700 dark:text-gray-300">
            {t("consentGiven", { date: new Date(profile!.aiScoringConsentAcceptedAt!).toLocaleDateString(locale) })}{" "}
            <Link href="/privacy#job-matching" className="text-accent-ink underline-offset-2 hover:underline">
              {t("privacyLink")}
            </Link>
          </p>
        ) : (
          <Checkbox
            id="weekly-jobs-consent"
            checked={consent}
            onChange={(event) => setConsent(event.target.checked)}
            error={consentError ?? undefined}
            label={
              <>
                {t("consent")}{" "}
                <Link href="/privacy#job-matching" className="text-accent-ink underline-offset-2 hover:underline">
                  {t("privacyLink")}
                </Link>
              </>
            }
          />
        )}
        <p className="text-xs text-gray-500 dark:text-gray-400">{t("consentNote")}</p>
      </div>

      {error && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      )}

      <div className="flex items-center gap-2 pt-1">
        <Button type="submit" disabled={save.isPending}>
          {save.isPending ? t("saving") : t("save")}
        </Button>
        {profile && !confirmingDelete && (
          <button
            type="button"
            onClick={() => setConfirmingDelete(true)}
            className="rounded-md px-4 py-2 text-sm font-medium text-crit-ink transition-colors hover:bg-crit-wash"
          >
            {t("delete")}
          </button>
        )}
      </div>
      {confirmingDelete && (
        <div className="flex flex-col gap-2 rounded-md border border-crit/40 bg-crit-wash p-3 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-sm text-crit-ink">{t("deleteConfirm")}</p>
          <div className="flex gap-2">
            <Button type="button" variant="danger" disabled={remove.isPending} onClick={() => remove.mutate()}>
              {t("deleteYes")}
            </Button>
            <Button type="button" variant="secondary" disabled={remove.isPending} onClick={() => setConfirmingDelete(false)}>
              {t("deleteNo")}
            </Button>
          </div>
        </div>
      )}
    </form>
  );
}
