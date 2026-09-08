import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { getTranslations } from "next-intl/server";
import { Link } from "@/i18n/navigation";

// The Chrome Web Store's Privacy practices tab requires a publicly reachable privacy policy URL,
// and the policy it points at has to describe the extension's own data handling. It lives on its
// own page rather than as a section of /privacy so a Web Store reviewer lands on a document that
// is about nothing else — /privacy is the account-level policy (sign-in providers, retention,
// KVKK rights) and would bury the extension's disclosures. The two link to each other.
export async function generateMetadata({ params }: PageProps<"/[locale]/extension-privacy">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/extension-privacy", "extensionPrivacy");
}

export default async function ExtensionPrivacyPage() {
  const t = await getTranslations("extensionPrivacy");

  return (
    <div className="mx-auto max-w-2xl px-4 py-12">
      <h1 className="mb-2 text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
      <p className="mb-8 text-sm text-gray-500 dark:text-gray-400">{t("lastUpdated")}</p>

      <div className="flex flex-col gap-8 text-sm leading-6 text-gray-700 dark:text-gray-300">
        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("scope.title")}</h2>
          <p>{t("scope.body")}</p>
        </section>

        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("access.title")}</h2>
          <p>{t("access.intro")}</p>
          <ul className="mt-2 list-disc pl-5">
            <li>{t("access.item1")}</li>
            <li>{t("access.item2")}</li>
            <li>{t("access.item3")}</li>
            <li>{t("access.item4")}</li>
          </ul>
          <p className="mt-2">{t("access.outro")}</p>
        </section>

        <section id="gmail-scanning">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("gmail.title")}</h2>
          <p>{t("gmail.intro")}</p>
          <p className="mt-2">{t("gmail.whileOff")}</p>
          <p className="mt-2">{t("gmail.whenOn")}</p>
          <p className="mt-2">{t("gmail.localScoring")}</p>
          <p className="mt-2">{t("gmail.whatIsSent")}</p>
          <p className="mt-2">{t("gmail.dedupe")}</p>
        </section>

        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("stored.title")}</h2>
          <p>{t("stored.intro")}</p>
          <ul className="mt-2 list-disc pl-5">
            <li>{t("stored.item1")}</li>
            <li>{t("stored.item2")}</li>
            <li>{t("stored.item3")}</li>
            <li>{t("stored.item4")}</li>
            <li>{t("stored.item5")}</li>
          </ul>
          <p className="mt-2">{t("stored.outro")}</p>
        </section>

        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("sent.title")}</h2>
          <p>{t("sent.body")}</p>
          <p className="mt-2">{t("sent.hrNote")}</p>
          <p className="mt-2">{t("sent.gmailNote")}</p>
          <p className="mt-2">{t("sent.noOther")}</p>
        </section>

        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("controls.title")}</h2>
          <ul className="mt-2 list-disc pl-5">
            <li>{t("controls.item1")}</li>
            <li>{t("controls.item2")}</li>
            <li>{t("controls.item3")}</li>
            <li>{t("controls.item4")}</li>
          </ul>
        </section>

        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("appPolicy.title")}</h2>
          <p>
            {t("appPolicy.before")}{" "}
            <Link href="/privacy" className="text-blue-600 hover:underline dark:text-blue-400">
              {t("appPolicy.link")}
            </Link>
            {t("appPolicy.after")}
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
