"use client";

import { useTranslations } from "next-intl";
import { LinkedInImportUploader } from "@/components/imports/LinkedInImportUploader";

export default function ImportPage() {
  const t = useTranslations("imports");

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-6">
      <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
      <LinkedInImportUploader />
    </div>
  );
}
