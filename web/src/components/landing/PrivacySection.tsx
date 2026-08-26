import { getTranslations } from "next-intl/server";
import { Link } from "@/i18n/navigation";
import { ScrollReveal } from "@/components/landing/ScrollReveal";

export async function PrivacySection() {
  const t = await getTranslations("landing.privacy");

  const points = [
    { title: t("privateTitle"), body: t("privateBody") },
    { title: t("anonymousTitle"), body: t("anonymousBody") },
    { title: t("deleteTitle"), body: t("deleteBody") },
  ];

  return (
    <section className="border-t border-gray-200 py-20 dark:border-gray-800">
      <ScrollReveal className="mx-auto flex max-w-4xl flex-col gap-10 px-4">
        <div className="flex flex-col gap-2 text-center">
          <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
          <h2 className="text-3xl font-semibold text-gray-900 sm:text-4xl dark:text-gray-100">{t("title")}</h2>
        </div>

        <div className="grid gap-6 sm:grid-cols-3">
          {points.map((point) => (
            <div key={point.title} className="flex flex-col gap-2 text-center sm:text-left">
              <h3 className="text-base font-semibold text-gray-900 dark:text-gray-100">{point.title}</h3>
              <p className="text-sm text-gray-600 dark:text-gray-400">{point.body}</p>
            </div>
          ))}
        </div>

        <p className="text-center">
          <Link href="/privacy" className="text-sm text-blue-600 hover:underline dark:text-blue-400">
            {t("link")}
          </Link>
        </p>
      </ScrollReveal>
    </section>
  );
}
