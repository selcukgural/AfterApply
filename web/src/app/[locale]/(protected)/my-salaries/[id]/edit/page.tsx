"use client";

import { use, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import type { CompanySalaryRequest } from "@/types/api";
import { companySalariesApi } from "@/lib/api/companySalaries";
import { ApiError } from "@/lib/api/httpClient";
import { draftFromSalary } from "@/lib/companySalaries/salaryDraft";
import { CompanySalaryForm } from "@/components/companySalaries/CompanySalaryForm";
import { SalaryGuidelines } from "@/components/companySalaries/SalaryGuidelines";

export default function EditSalaryPage({ params }: PageProps<"/[locale]/my-salaries/[id]/edit">) {
  const { id } = use(params);
  const t = useTranslations("companySalaries.edit");
  const locale = useLocale();
  const router = useRouter();
  const queryClient = useQueryClient();
  const [serverError, setServerError] = useState<string | null>(null);

  // No single-entry endpoint: the author's list is at most their quota long, so it is the lookup.
  const { data, isLoading } = useQuery({ queryKey: ["companySalaries", "mine"], queryFn: companySalariesApi.listMine });
  const entry = data?.items.find((item) => item.id === id) ?? null;

  const update = useMutation({
    mutationFn: (request: CompanySalaryRequest) => companySalariesApi.update(id, request),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["contributions", "mine"] }),
        queryClient.invalidateQueries({ queryKey: ["companySalaries", "mine"] }),
        queryClient.invalidateQueries({ queryKey: ["companies", entry!.companyId, "salaries"] }),
        queryClient.invalidateQueries({ queryKey: ["companies", entry!.companyId, "salaryViewer"] }),
      ]);
      router.push("/my-reviews");
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
          <Link href="/my-reviews" className="text-accent-ink underline-offset-2 hover:underline">
            {t("backToMine")}
          </Link>
        </p>
      )}

      {entry && data && (
        <div className="grid gap-6 lg:grid-cols-[1fr_20rem]">
          <CompanySalaryForm
            companyName={entry.companyName}
            initialDraft={draftFromSalary(entry, locale)}
            submitLabel={t("submit")}
            serverError={serverError}
            quota={data.quota}
            onSubmit={async (request) => {
              setServerError(null);
              await update.mutateAsync(request).catch(() => undefined);
            }}
          />
          <SalaryGuidelines />
        </div>
      )}
    </div>
  );
}
