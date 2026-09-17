"use client";

import { use, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import type { CandidateExperienceRequest } from "@/types/api";
import { candidateExperiencesApi } from "@/lib/api/candidateExperiences";
import { ApiError } from "@/lib/api/httpClient";
import { draftFromExperience } from "@/lib/candidateExperiences/experienceDraft";
import { CandidateExperienceForm } from "@/components/candidateExperiences/CandidateExperienceForm";
import { ExperienceGuidelines } from "@/components/candidateExperiences/ExperienceGuidelines";

export default function EditExperiencePage({ params }: PageProps<"/[locale]/my-experiences/[id]/edit">) {
  const { id } = use(params);
  const t = useTranslations("candidateExperiences.edit");
  const router = useRouter();
  const queryClient = useQueryClient();
  const [serverError, setServerError] = useState<string | null>(null);

  // No single-entry endpoint: the author's list is at most their quota long, so it is the lookup.
  const { data, isLoading } = useQuery({ queryKey: ["candidateExperiences", "mine"], queryFn: candidateExperiencesApi.listMine });
  const entry = data?.items.find((item) => item.id === id) ?? null;

  const update = useMutation({
    mutationFn: (request: CandidateExperienceRequest) => candidateExperiencesApi.update(id, request),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["candidateExperiences", "mine"] }),
        queryClient.invalidateQueries({ queryKey: ["companies", entry!.companySlug, "experiences"] }),
        queryClient.invalidateQueries({ queryKey: ["companies", entry!.companyId, "experienceViewer"] }),
      ]);
      router.push("/my-experiences");
    },
    onError: (err) => setServerError(err instanceof ApiError ? err.message : t("error")),
  });

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </div>

      {isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
      {data && !entry && (
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t("notFound")}{" "}
          <Link href="/my-experiences" className="text-accent-ink underline-offset-2 hover:underline">
            {t("backToMine")}
          </Link>
        </p>
      )}

      {entry && (
        <div className="grid gap-6 lg:grid-cols-[1fr_20rem]">
          <CandidateExperienceForm
            companyName={entry.companyName}
            initialDraft={draftFromExperience(entry)}
            submitLabel={t("submit")}
            serverError={serverError}
            onSubmit={async (request) => {
              setServerError(null);
              await update.mutateAsync(request).catch(() => undefined);
            }}
          />
          <ExperienceGuidelines />
        </div>
      )}
    </div>
  );
}
