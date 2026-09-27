"use client";

import { useTranslations } from "next-intl";
import { AdminTabs } from "@/components/admin/AdminTabs";
import { DangerBanner } from "@/components/admin/flags/DangerBanner";
import { FeatureFlagsPanel } from "@/components/admin/flags/FeatureFlagsPanel";

/** Runtime feature flags (DECISIONS.md 2026-09-27). Admin only: the API answers 403 to anyone else. */
export default function AdminFlagsPage() {
  const t = useTranslations("adminFlags");
  return (
    <div className="flex flex-col gap-6">
      <DangerBanner />
      <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
      <AdminTabs />
      <FeatureFlagsPanel />
    </div>
  );
}
