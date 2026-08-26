import { getTranslations } from "next-intl/server";
import { ScrollReveal } from "@/components/landing/ScrollReveal";

export async function MissionSection() {
  const t = await getTranslations("landing.mission");

  const narrative = [t("narrative1"), t("narrative2"), t("narrative3"), t("narrative4")];
  const philosophy = [t("philosophy1"), t("philosophy2"), t("philosophy3"), t("philosophy4"), t("philosophy5")];

  return (
    <section id="mission" className="scroll-mt-20 border-t border-gray-200 py-24 dark:border-gray-800">
      <ScrollReveal className="mx-auto flex max-w-3xl flex-col items-center gap-10 px-4 text-center">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h2 className="text-3xl font-semibold tracking-tight text-gray-900 sm:text-5xl dark:text-gray-100">
          {t("statement")}
        </h2>

        <div className="flex flex-col gap-2 text-base text-gray-600 dark:text-gray-400">
          {narrative.map((line) => (
            <p key={line}>{line}</p>
          ))}
        </div>

        <div className="flex flex-col gap-1">
          <p className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("closing")}</p>
          <p className="text-base text-gray-600 dark:text-gray-400">{t("closingNote")}</p>
        </div>

        <ul className="grid gap-3 text-left text-sm text-gray-600 sm:grid-cols-2 dark:text-gray-400">
          {philosophy.map((item) => (
            <li key={item} className="flex items-start gap-2">
              <span aria-hidden="true" className="mt-1 h-1.5 w-1.5 shrink-0 rounded-full bg-blue-500" />
              <span>{item}</span>
            </li>
          ))}
        </ul>
      </ScrollReveal>
    </section>
  );
}
