"use client";

import { useState, type FormEvent } from "react";
import { useLocale, useTranslations } from "next-intl";
import { ShareRow } from "@/components/share/ShareRow";
import { SITE_URL } from "@/lib/seo/routes";
import { cvScanPath } from "@/lib/cvScan/path";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Link } from "@/i18n/navigation";
import { benchmarkApi } from "@/lib/api/benchmark";
import { ApiError } from "@/lib/api/httpClient";
import { formatCount } from "@/lib/dashboard/format";
import {
  BENCHMARK_LOCATIONS as LOCATIONS,
  BENCHMARK_PERIODS as PERIODS,
  BENCHMARK_SECTORS as SECTORS,
  BENCHMARK_SENIORITIES as SENIORITIES,
} from "@/lib/benchmark/options";
import { Button, buttonClassName } from "@/components/ui/Button";
import { BenchmarkMedianCard, BenchmarkYourRateCard } from "@/components/benchmark/BenchmarkResultCards";
import { BenchmarkParticipantCard, BenchmarkSurveySteps } from "@/components/benchmark/BenchmarkSurvey";
import { benchmarkSourceFromSearch } from "@/lib/benchmark/source";
import { surveyProgress } from "@/lib/benchmark/survey";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import type {
  BenchmarkLocation,
  BenchmarkPeriod,
  BenchmarkResultResponse,
  BenchmarkSector,
  BenchmarkSeniority,
} from "@/types/api";

export function BenchmarkForm() {
  const t = useTranslations("benchmark");
  const locale = useLocale();

  const [applications, setApplications] = useState("");
  const [replies, setReplies] = useState("");
  const [sector, setSector] = useState<BenchmarkSector | "">("");
  const [period, setPeriod] = useState<BenchmarkPeriod | "">("");
  const [seniority, setSeniority] = useState<BenchmarkSeniority | "">("");
  const [location, setLocation] = useState<BenchmarkLocation | "">("");
  const [website, setWebsite] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [result, setResult] = useState<BenchmarkResultResponse | null>(null);

  const { data: summary } = useQuery({
    queryKey: ["benchmark", "summary"],
    queryFn: benchmarkApi.getSummary,
  });

  const submit = useMutation({
    mutationFn: benchmarkApi.submit,
    onSuccess: setResult,
  });

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();

    const applicationCount = Number(applications);
    const replyCount = Number(replies);
    const found: Record<string, string> = {};

    if (!applications || !Number.isInteger(applicationCount) || applicationCount < 1) {
      found.applications = t("errors.required");
    }
    if (!replies || !Number.isInteger(replyCount) || replyCount < 0) {
      found.replies = t("errors.required");
    }
    // The one mistake a person actually makes. Caught here so it is answered next to the field
    // rather than by a round trip.
    if (!found.applications && !found.replies && replyCount > applicationCount) {
      found.replies = t("errors.repliesExceed");
    }
    if (!sector) found.sector = t("errors.required");
    if (!period) found.period = t("errors.required");

    setErrors(found);
    if (Object.keys(found).length > 0) return;

    submit.mutate({
      applicationCount,
      replyCount,
      sector: sector as BenchmarkSector,
      period: period as BenchmarkPeriod,
      seniority: seniority === "" ? null : seniority,
      location: location === "" ? null : location,
      locale,
      website,
      // Read at submit rather than render: the page stays static, and the only thing wanted from
      // the address is which campaign link, if any, brought this visitor here.
      source: benchmarkSourceFromSearch(window.location.search),
    });
  };

  const submitError = submit.isError
    ? submit.error instanceof ApiError && submit.error.status === 429
      ? t("errors.tooMany")
      : t("errors.generic")
    : null;

  if (result) {
    return <BenchmarkResult result={result} onReset={() => setResult(null)} />;
  }

  return (
    <div className="flex flex-col gap-6">
      {/* The participation line only once there are enough answers to compare against. Below the
          threshold it was social proof in reverse — "6 people answered" reads as "nobody uses
          this" — and the honest thing to say there is nothing (growth audit 2026-09-14, finding 10). */}
      {summary && summary.totalSubmissions >= summary.minimumSampleSize ? (
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t("participation", { count: formatCount(summary.totalSubmissions, locale), n: summary.totalSubmissions })}
        </p>
      ) : null}

      <form onSubmit={handleSubmit} className="flex flex-col gap-5" noValidate>
        <div className="grid gap-5 sm:grid-cols-2">
          <FormField label={t("form.applicationsLabel")} htmlFor="applications" error={errors.applications}>
            <Input
              id="applications"
              type="number"
              inputMode="numeric"
              min={1}
              value={applications}
              onChange={(e) => setApplications(e.target.value)}
            />
            <p className="text-xs text-gray-500 dark:text-gray-400">{t("form.applicationsHint")}</p>
          </FormField>

          <FormField label={t("form.repliesLabel")} htmlFor="replies" error={errors.replies}>
            <Input
              id="replies"
              type="number"
              inputMode="numeric"
              min={0}
              value={replies}
              onChange={(e) => setReplies(e.target.value)}
            />
            {/* The definition is the whole measurement. Left visible rather than in a tooltip:
                someone who counts only offers reports a different number entirely. */}
            <p className="text-xs text-gray-500 dark:text-gray-400">{t("form.repliesHint")}</p>
          </FormField>
        </div>

        <FormField label={t("form.periodLabel")} htmlFor="period" error={errors.period}>
          <Select id="period" value={period} onChange={(e) => setPeriod(e.target.value as BenchmarkPeriod)}>
            <option value="">{t("form.choose")}</option>
            {PERIODS.map((value) => (
              <option key={value} value={value}>
                {t(`periods.${value}`)}
              </option>
            ))}
          </Select>
        </FormField>

        <FormField label={t("form.sectorLabel")} htmlFor="sector" error={errors.sector}>
          <Select id="sector" value={sector} onChange={(e) => setSector(e.target.value as BenchmarkSector)}>
            <option value="">{t("form.choose")}</option>
            {SECTORS.map((value) => (
              <option key={value} value={value}>
                {t(`sectors.${value}`)}
              </option>
            ))}
          </Select>
        </FormField>

        <div className="grid gap-5 sm:grid-cols-2">
          <FormField label={`${t("form.seniorityLabel")} (${t("form.optional")})`} htmlFor="seniority">
            <Select
              id="seniority"
              value={seniority}
              onChange={(e) => setSeniority(e.target.value as BenchmarkSeniority)}
            >
              <option value="">{t("form.choose")}</option>
              {SENIORITIES.map((value) => (
                <option key={value} value={value}>
                  {t(`seniorities.${value}`)}
                </option>
              ))}
            </Select>
          </FormField>

          <FormField label={`${t("form.locationLabel")} (${t("form.optional")})`} htmlFor="location">
            <Select id="location" value={location} onChange={(e) => setLocation(e.target.value as BenchmarkLocation)}>
              <option value="">{t("form.choose")}</option>
              {LOCATIONS.map((value) => (
                <option key={value} value={value}>
                  {t(`locations.${value}`)}
                </option>
              ))}
            </Select>
          </FormField>
        </div>

        {/* Honeypot. Hidden from people and from assistive technology, left in the DOM for anything
            that fills every input it finds. The usual answer to this — reCAPTCHA, hCaptcha,
            Turnstile — is a third-party script, which the CSP forbids and the Cookie Policy says
            the site does not carry. */}
        <div className="hidden" aria-hidden="true">
          <label htmlFor="website">Website</label>
          <input
            id="website"
            name="website"
            type="text"
            tabIndex={-1}
            autoComplete="off"
            value={website}
            onChange={(e) => setWebsite(e.target.value)}
          />
        </div>

        {submitError ? <p className="text-sm text-red-600 dark:text-red-400">{submitError}</p> : null}

        <div>
          <Button type="submit" disabled={submit.isPending}>
            {submit.isPending ? t("form.submitting") : t("form.submit")}
          </Button>
        </div>
      </form>
    </div>
  );
}

function BenchmarkResult({ result, onReset }: { result: BenchmarkResultResponse; onReset: () => void }) {
  const t = useTranslations("benchmark");
  const locale = useLocale();

  const median = result.medianRate;
  const progress = surveyProgress(result);

  return (
    <div className="flex flex-col gap-6">
      {/* Until the reader's own sector can stand alone, the result is framed as taking part in a
          survey (growth research 2026-09-21, item 0.1; canvas variant B): their place in it beside
          their rate, and what opens next. Once the sector clears the threshold this falls away and
          the page is the plain comparison it was built to be. */}
      {progress ? (
        <div className="grid gap-4 sm:grid-cols-2">
          <BenchmarkYourRateCard
            yourRate={result.yourRate}
            replyCount={result.replyCount}
            applicationCount={result.applicationCount}
          />
          <BenchmarkParticipantCard participant={progress.participant} />
        </div>
      ) : (
        <BenchmarkYourRateCard
          yourRate={result.yourRate}
          replyCount={result.replyCount}
          applicationCount={result.applicationCount}
        />
      )}

      {/* The difference between a sector median and an overall one is the whole honesty of the
          page: a median drawn from every field must never read as one drawn from the reader's. The
          scope decides the heading and the label beside the number, inside the card. With nothing
          at all to compare against yet, the survey steps say so instead. */}
      {median !== null && result.scope !== "None" ? (
        <BenchmarkMedianCard
          scope={result.scope}
          sector={result.sector}
          sampleSize={result.sampleSize}
          minimumSampleSize={result.minimumSampleSize}
          comparedAgainstCount={result.comparedAgainstCount}
          shareBelowYou={result.shareBelowYou}
          yourRate={result.yourRate}
          medianRate={median}
        />
      ) : null}

      {progress ? <BenchmarkSurveySteps progress={progress} sector={result.sector} /> : null}

      {/* The result as a sentence someone can pass on, with the median in it when there was one —
          "is yours normal?" is the question that brings the next answer, and the next answer is
          what lifts a sector over its threshold. The link carries utm_source=share so answers that
          arrive through a participant's own share can be told apart from the campaign channels. */}
      <div className="flex flex-col gap-2">
        {progress ? <p className="text-sm text-gray-700 dark:text-gray-300">{t("survey.shareNudge")}</p> : null}
        <ShareRow
          label={t("result.shareLabel")}
          content={{
            text:
              median !== null && result.scope !== "None"
                ? t("result.shareTextWithMedian", {
                    applications: formatCount(result.applicationCount, locale),
                    replies: formatCount(result.replyCount, locale),
                    rate: formatCount(Math.round(result.yourRate), locale),
                    median: formatCount(Math.round(median), locale),
                  })
                : t("result.shareText", {
                    applications: formatCount(result.applicationCount, locale),
                    replies: formatCount(result.replyCount, locale),
                    rate: formatCount(Math.round(result.yourRate), locale),
                  }),
            url: `${SITE_URL}/${locale}/benchmark?utm_source=share`,
          }}
        />
      </div>

      <button type="button" onClick={onReset} className="self-start text-sm text-blue-600 hover:underline dark:text-blue-400">
        {t("form.again")}
      </button>

      <div className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-gray-50 p-6 dark:border-gray-800 dark:bg-gray-900/40">
        <p className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("cta.title")}</p>
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("cta.body")}</p>

        {/* The reciprocal of the link the scan's own result already carries. It comes before the
            account CTA because it asks for less: both pages put the free thing first and the
            sign-up last, and a reader who just got one number is the likeliest person to want the
            other. */}
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t("cta.cvScan")}{" "}
          <Link href={cvScanPath(locale)} className="text-blue-600 underline underline-offset-2 dark:text-blue-400">
            {t("cta.cvScanLink")}
          </Link>
        </p>

        <Link href="/register" className={buttonClassName("primary", "self-start px-5 py-2.5")}>
          {t("cta.button")}
        </Link>
      </div>
    </div>
  );
}
