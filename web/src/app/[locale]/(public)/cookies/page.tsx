import { getTranslations } from "next-intl/server";
import { Link } from "@/i18n/navigation";

// KVKK's cookie guidance asks for disclosure rather than consent when every cookie is strictly
// necessary or functional, which is the case here (see DECISIONS.md, "Çerez banner'ı yok").
// It lives on its own page rather than as a section of /privacy for the same reason
// /extension-privacy does: it is the document a reader is sent to when they ask the narrow
// question "what do you put on my device", and /privacy would bury it.
//
// The two rows below are the whole inventory: `NEXT_LOCALE`, written server-side by next-intl's
// proxy (web/src/proxy.ts), and `theme`, written by web/src/lib/theme/theme.ts. Anything that
// widens that list has to update this page too — browserStorage.test.ts fails otherwise.
export default async function CookiesPage() {
  const t = await getTranslations("cookies");

  const rows = [
    {
      name: "NEXT_LOCALE",
      purpose: t("table.localePurpose"),
      type: t("table.localeType"),
      duration: t("table.localeDuration"),
    },
    {
      name: "theme",
      purpose: t("table.themePurpose"),
      type: t("table.themeType"),
      duration: t("table.themeDuration"),
    },
  ];

  return (
    <div className="mx-auto max-w-2xl px-4 py-12">
      <h1 className="mb-2 text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
      <p className="mb-8 text-sm text-gray-500 dark:text-gray-400">{t("lastUpdated")}</p>

      <div className="flex flex-col gap-8 text-sm leading-6 text-gray-700 dark:text-gray-300">
        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("intro.title")}</h2>
          <p>{t("intro.body")}</p>
          <p className="mt-2">{t("intro.legalNote")}</p>
        </section>

        <section id="cookie-table">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("table.title")}</h2>
          {/* Wide content on a narrow phone: the table scrolls inside its own box rather than
              pushing the page sideways. */}
          <div className="mt-2 overflow-x-auto">
            <table className="w-full min-w-[32rem] border-collapse text-left">
              <thead>
                <tr className="border-b border-gray-200 dark:border-gray-800">
                  <th className="py-2 pr-4 font-medium text-gray-900 dark:text-gray-100">{t("table.name")}</th>
                  <th className="py-2 pr-4 font-medium text-gray-900 dark:text-gray-100">{t("table.purpose")}</th>
                  <th className="py-2 pr-4 font-medium text-gray-900 dark:text-gray-100">{t("table.type")}</th>
                  <th className="py-2 font-medium text-gray-900 dark:text-gray-100">{t("table.duration")}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.name} className="border-b border-gray-100 align-top dark:border-gray-900">
                    <td className="py-2 pr-4 font-mono text-xs text-gray-900 dark:text-gray-100">{row.name}</td>
                    <td className="py-2 pr-4">{row.purpose}</td>
                    <td className="py-2 pr-4">{row.type}</td>
                    <td className="py-2">{row.duration}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="mt-2">{t("table.note")}</p>
        </section>

        <section id="browser-storage">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("storage.title")}</h2>
          <p>{t("storage.intro")}</p>
          <ul className="mt-2 list-disc pl-5">
            <li>{t("storage.item1")}</li>
            <li>{t("storage.item2")}</li>
          </ul>
          <p className="mt-2">{t("storage.outro")}</p>
        </section>

        <section id="no-third-party">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("noThirdParty.title")}</h2>
          <p>{t("noThirdParty.intro")}</p>
          <ul className="mt-2 list-disc pl-5">
            <li>{t("noThirdParty.item1")}</li>
            <li>{t("noThirdParty.item2")}</li>
            <li>{t("noThirdParty.item3")}</li>
            <li>{t("noThirdParty.item4")}</li>
          </ul>
          <p className="mt-2">{t("noThirdParty.outro")}</p>
        </section>

        <section id="error-monitoring">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("errorMonitoring.title")}</h2>
          <p>
            {t("errorMonitoring.before")}{" "}
            <Link href="/privacy#error-monitoring" className="text-blue-600 hover:underline dark:text-blue-400">
              {t("errorMonitoring.link")}
            </Link>
            {t("errorMonitoring.after")}
          </p>
        </section>

        <section id="control">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("control.title")}</h2>
          <p>{t("control.body")}</p>
          <p className="mt-2">{t("control.accountNote")}</p>
        </section>

        <section id="changes">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("changes.title")}</h2>
          <p>{t("changes.body")}</p>
        </section>

        <section>
          <p>
            {t("policyLink.before")}{" "}
            <Link href="/privacy" className="text-blue-600 hover:underline dark:text-blue-400">
              {t("policyLink.link")}
            </Link>
            {t("policyLink.after")}
          </p>
        </section>

        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("contact.title")}</h2>
          <p>
            {t("contact.before")}{" "}
            <a href="mailto:privacy@ekariyerim.com" className="text-blue-600 hover:underline dark:text-blue-400">
              privacy@ekariyerim.com
            </a>
            {t("contact.after")}
          </p>
        </section>
      </div>
    </div>
  );
}
