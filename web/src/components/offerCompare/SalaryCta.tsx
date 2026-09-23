"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { useClientConfig } from "@/hooks/useClientConfig";
import { buttonClassName } from "@/components/ui/Button";

const SALARY_FORM = "/contribute?tab=salary";

/**
 * Where the comparison hands over to the product: an accepted offer is exactly the salary the
 * company pages lack. Signed out, the button goes through sign-in and comes back to the form.
 * Hidden while the salaries feature is off — a button to a switched-off form is worse than none.
 */
export function SalaryCta() {
  const t = useTranslations("offerCompare.salaryCta");
  const { isAuthenticated } = useAuth();
  const { config } = useClientConfig();
  if (config.companySalaries?.enabled !== true) return null;

  const href = isAuthenticated ? SALARY_FORM : `/login?next=${encodeURIComponent(SALARY_FORM)}`;
  return (
    <section className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-5 sm:p-6 dark:border-gray-800 dark:bg-gray-900">
      <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
      <p className="text-sm leading-relaxed text-gray-600 dark:text-gray-400">{t("body")}</p>
      <Link href={href} className={buttonClassName("primary", "self-start")}>
        {t("button")}
      </Link>
    </section>
  );
}
