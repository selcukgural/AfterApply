import { getTranslations } from "next-intl/server";
import { Link } from "@/i18n/navigation";

export default async function PrivacyPage() {
  const t = await getTranslations("privacy");

  return (
    <div className="mx-auto max-w-2xl px-4 py-12">
      <h1 className="mb-2 text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
      <p className="mb-8 text-sm text-gray-500 dark:text-gray-400">{t("lastUpdated")}</p>

      <div className="flex flex-col gap-8 text-sm leading-6 text-gray-700 dark:text-gray-300">
        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("dataCollection.title")}</h2>
          <p>{t("dataCollection.intro")}</p>
          <ul className="mt-2 list-disc pl-5">
            <li>{t("dataCollection.item1")}</li>
            <li>{t("dataCollection.item2")}</li>
            <li>{t("dataCollection.item3")}</li>
            <li>{t("dataCollection.item4")}</li>
          </ul>
          <p className="mt-2">{t("dataCollection.outro")}</p>
        </section>

        <section id="google-sign-in">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("googleSignIn.title")}</h2>
          <p>{t("googleSignIn.intro")}</p>
          <ul className="mt-2 list-disc pl-5">
            <li>{t("googleSignIn.item1")}</li>
            <li>{t("googleSignIn.item2")}</li>
            <li>{t("googleSignIn.item3")}</li>
          </ul>
          <p className="mt-2">{t("googleSignIn.noAccess")}</p>
          <p className="mt-2">{t("googleSignIn.linking")}</p>
          <p className="mt-2">{t("googleSignIn.revoke")}</p>
        </section>

        <section id="cross-border-transfer">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("crossBorderTransfer.title")}</h2>
          <p>{t("crossBorderTransfer.intro")}</p>
          <ul className="mt-2 list-disc pl-5">
            <li>{t("crossBorderTransfer.recipient")}</li>
            <li>{t("crossBorderTransfer.purpose")}</li>
            <li>{t("crossBorderTransfer.legalBasis")}</li>
            <li>{t("crossBorderTransfer.withdraw")}</li>
          </ul>
          <p className="mt-2">{t("crossBorderTransfer.sensitiveDataNote")}</p>
        </section>

        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("retention.title")}</h2>
          <p>{t("retention.body")}</p>
        </section>

        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("rights.title")}</h2>
          <p>
            {t("rights.before")}{" "}
            <Link href="/settings" className="text-blue-600 hover:underline dark:text-blue-400">
              {t("rights.link")}
            </Link>{" "}
            {t("rights.after")}
          </p>
        </section>

        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("notApplicable.title")}</h2>
          <p>{t("notApplicable.body")}</p>
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
