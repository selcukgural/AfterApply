import { getTranslations } from "next-intl/server";
import { Link } from "@/i18n/navigation";

export async function LandingFooter() {
  const t = await getTranslations("landing.footer");
  const tNav = await getTranslations("landing.navbar");
  const year = new Date().getFullYear();

  return (
    <footer className="border-t border-gray-200 py-12 dark:border-gray-800">
      <div className="mx-auto flex max-w-6xl flex-col gap-8 px-4 sm:flex-row sm:justify-between">
        <div className="flex flex-col gap-2">
          <span className="text-lg font-semibold text-gray-900 dark:text-gray-100">AfterApply</span>
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("tagline")}</p>
        </div>

        <div className="flex gap-12">
          <div className="flex flex-col gap-2 text-sm">
            <span className="font-medium text-gray-700 dark:text-gray-300">{t("product")}</span>
            <a href="#how-it-works" className="text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100">
              {tNav("howItWorks")}
            </a>
            <a href="#features" className="text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100">
              {tNav("features")}
            </a>
            <a href="#mission" className="text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100">
              {tNav("mission")}
            </a>
          </div>

          <div className="flex flex-col gap-2 text-sm">
            <span className="font-medium text-gray-700 dark:text-gray-300">{t("resources")}</span>
            <Link href="/privacy" className="text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100">
              {t("privacy")}
            </Link>
          </div>
        </div>
      </div>

      <p className="mx-auto mt-8 max-w-6xl px-4 text-xs text-gray-400 dark:text-gray-600">
        © {year} AfterApply. {t("rights")}
      </p>
    </footer>
  );
}
