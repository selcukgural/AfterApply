"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { buttonClassName } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";

/**
 * What a brand-new account sees instead of twelve tiles reading "0". Every figure on this
 * dashboard is derived from applications, so with none there is nothing to derive — say that and
 * point at the ways to get some in: by hand, from the LinkedIn export, or from the extension.
 */
export function DashboardEmptyState() {
  const t = useTranslations("dashboard.empty");

  return (
    <EmptyState
      title={t("title")}
      body={t("body")}
      actions={
        <>
          <Link href="/applications/new" className={buttonClassName("primary")}>
            {t("cta")}
          </Link>
          <Link href="/import" className={buttonClassName("secondary")}>
            {t("importCta")}
          </Link>
          <Link href="/help/chrome-extension" className={buttonClassName("outline")}>
            {t("extensionCta")}
          </Link>
        </>
      }
    />
  );
}
