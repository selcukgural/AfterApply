"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { matchingApi } from "@/lib/api/matching";
import { ApiError } from "@/lib/api/httpClient";
import type { JobMatchRecommendation } from "@/types/api";
import { Textarea } from "@/components/ui/Textarea";
import { Button } from "@/components/ui/Button";

interface JobMatchPanelProps {
  applicationId: string;
}

const RECOMMENDATION_STYLES: Record<JobMatchRecommendation, string> = {
  Apply: "bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300",
  Consider: "bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300",
  Skip: "bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300",
};

export function JobMatchPanel({ applicationId }: JobMatchPanelProps) {
  const t = useTranslations("applications.detail.match");
  const queryClient = useQueryClient();
  const [jobDescription, setJobDescription] = useState("");
  const [error, setError] = useState<string | null>(null);

  const { data: match } = useQuery({
    queryKey: ["matching", "match", applicationId],
    queryFn: () => matchingApi.getMatch(applicationId),
    // 404 (no match computed yet) is an expected, non-error state here — don't retry it.
    retry: false,
  });

  const computeMutation = useMutation({
    mutationFn: () => matchingApi.computeMatch(applicationId, jobDescription),
    onSuccess: (result) => {
      setError(null);
      queryClient.setQueryData(["matching", "match", applicationId], result);
    },
    onError: (err: unknown) => {
      if (err instanceof ApiError && err.status === 400) {
        setError(t("profileRequiredError"));
      } else {
        setError(t("genericError"));
      }
    },
  });

  return (
    <div className="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-4">
      <h2 className="mb-3 text-sm font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>

      <div className="flex flex-col gap-3">
        <div>
          <label className="mb-1 block text-xs text-gray-500 dark:text-gray-400">{t("jobDescriptionLabel")}</label>
          <Textarea
            rows={6}
            value={jobDescription}
            placeholder={t("jobDescriptionPlaceholder")}
            onChange={(e) => setJobDescription(e.target.value)}
          />
        </div>

        {error && (
          <p className="text-sm text-red-600 dark:text-red-400">
            {error} {error === t("profileRequiredError") && <Link href="/settings" className="underline">{t("setCvLink")}</Link>}
          </p>
        )}

        <div>
          <Button
            variant="secondary"
            disabled={computeMutation.isPending || jobDescription.trim().length === 0}
            onClick={() => computeMutation.mutate()}
          >
            {computeMutation.isPending ? t("computing") : match ? t("recompute") : t("compute")}
          </Button>
        </div>

        {match && (
          <div className="flex flex-col gap-2 rounded-md border border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-800 p-3">
            <div className="flex items-center justify-between">
              <span className="text-xs text-gray-500 dark:text-gray-400">{t("score")}</span>
              <span className="text-lg font-semibold text-gray-900 dark:text-gray-100">{match.score}/100</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-xs text-gray-500 dark:text-gray-400">{t("recommendation")}</span>
              <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${RECOMMENDATION_STYLES[match.recommendation]}`}>
                {t(`recommendation${match.recommendation}`)}
              </span>
            </div>
            {match.strongMatches.length > 0 && (
              <div>
                <p className="text-xs text-gray-500 dark:text-gray-400">{t("strongMatches")}</p>
                <p className="text-sm text-gray-900 dark:text-gray-100">{match.strongMatches.join(", ")}</p>
              </div>
            )}
            {match.missing.length > 0 && (
              <div>
                <p className="text-xs text-gray-500 dark:text-gray-400">{t("missing")}</p>
                <p className="text-sm text-gray-900 dark:text-gray-100">{match.missing.join(", ")}</p>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
