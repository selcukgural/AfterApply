import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { getTranslations } from "next-intl/server";
import { Link } from "@/i18n/navigation";

export async function generateMetadata({ params }: PageProps<"/[locale]/privacy">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/privacy", "privacy");
}

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
            <li>{t("dataCollection.item5")}</li>
          </ul>
          <p className="mt-2">{t("dataCollection.outro")}</p>
        </section>

        <section id="cv-storage">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("cvStorage.title")}</h2>
          <p>{t("cvStorage.intro")}</p>
          <ul className="mt-2 list-disc pl-5">
            <li>{t("cvStorage.where")}</li>
            <li>{t("cvStorage.access")}</li>
            <li>{t("cvStorage.noTransfer")}</li>
            <li>{t("cvStorage.deletion")}</li>
            <li>{t("cvStorage.consent")}</li>
          </ul>
          <p className="mt-2">{t("cvStorage.sensitiveDataNote")}</p>
        </section>

        {/* Deliberately its own section rather than a paragraph inside cv-storage: one is a file
            we keep for a signed-in user, the other is a file we never keep at all, and running the
            two together is exactly the confusion this page exists to prevent. The list below is the
            same list the scan page itself prints — the two must never drift. */}
        <section id="cv-scan">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("cvScan.title")}</h2>
          <p>{t("cvScan.intro")}</p>
          <ul className="mt-2 list-disc pl-5">
            <li>{t("cvScan.noStorage")}</li>
            <li>{t("cvScan.retained")}</li>
            <li>{t("cvScan.noTransfer")}</li>
            {/* Layer B, and the one bullet above it says "no third party" — so this one has to name
                the exception in the same list rather than in a footnote somewhere else. */}
            <li>{t("cvScan.contentNotes")}</li>
            <li>{t("cvScan.output")}</li>
            <li>{t("cvScan.consent")}</li>
          </ul>
        </section>

        <section id="feedback">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("feedback.title")}</h2>
          <p>{t("feedback.intro")}</p>
          <ul className="mt-2 list-disc pl-5">
            <li>{t("feedback.what")}</li>
            <li>{t("feedback.context")}</li>
            <li>{t("feedback.notCollected")}</li>
            <li>{t("feedback.replyEmail")}</li>
            <li>{t("feedback.storage")}</li>
            <li>{t("feedback.mirror")}</li>
            <li>{t("feedback.mirrorRecipient")}</li>
            <li>{t("feedback.deletion")}</li>
          </ul>
          <p className="mt-2">{t("feedback.legalBasis")}</p>
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

        <section id="linkedin-sign-in">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("linkedInSignIn.title")}</h2>
          <p>{t("linkedInSignIn.intro")}</p>
          <ul className="mt-2 list-disc pl-5">
            <li>{t("linkedInSignIn.item1")}</li>
            <li>{t("linkedInSignIn.item2")}</li>
            <li>{t("linkedInSignIn.item3")}</li>
          </ul>
          <p className="mt-2">{t("linkedInSignIn.noAccess")}</p>
          <p className="mt-2">{t("linkedInSignIn.linking")}</p>
          <p className="mt-2">{t("linkedInSignIn.revoke")}</p>
        </section>

        <section id="github-sign-in">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("gitHubSignIn.title")}</h2>
          <p>{t("gitHubSignIn.intro")}</p>
          <ul className="mt-2 list-disc pl-5">
            <li>{t("gitHubSignIn.item1")}</li>
            <li>{t("gitHubSignIn.item2")}</li>
            <li>{t("gitHubSignIn.item3")}</li>
          </ul>
          <p className="mt-2">{t("gitHubSignIn.noAccess")}</p>
          <p className="mt-2">{t("gitHubSignIn.linking")}</p>
          <p className="mt-2">{t("gitHubSignIn.revoke")}</p>
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
          <p className="mt-2">{t("crossBorderTransfer.feedbackTransfer")}</p>
          <p className="mt-2">{t("crossBorderTransfer.jobSearchTransfer")}</p>
        </section>

        <section id="error-monitoring">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("errorMonitoring.title")}</h2>
          <p>{t("errorMonitoring.intro")}</p>
          <ul className="mt-2 list-disc pl-5">
            <li>{t("errorMonitoring.item1")}</li>
            <li>{t("errorMonitoring.item2")}</li>
            <li>{t("errorMonitoring.item3")}</li>
          </ul>
          <p className="mt-2">{t("errorMonitoring.recipient")}</p>
          <p className="mt-2">{t("errorMonitoring.legalBasis")}</p>
          <p className="mt-2">{t("errorMonitoring.limits")}</p>
          <p className="mt-2">{t("errorMonitoring.retention")}</p>
        </section>

        <section id="visit-counter">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("visitCounter.title")}</h2>
          <p>{t("visitCounter.intro")}</p>
          <ul className="mt-2 list-disc pl-5">
            <li>{t("visitCounter.item1")}</li>
            <li>{t("visitCounter.item2")}</li>
            <li>{t("visitCounter.item3")}</li>
          </ul>
          <p className="mt-2">{t("visitCounter.recipient")}</p>
          <p className="mt-2">{t("visitCounter.legalBasis")}</p>
          <p className="mt-2">{t("visitCounter.limits")}</p>
          <p className="mt-2">{t("visitCounter.retention")}</p>
        </section>

        <section id="cookies">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("cookiesSection.title")}</h2>
          <p>
            {t("cookiesSection.before")}{" "}
            <Link href="/cookies" className="text-blue-600 hover:underline dark:text-blue-400">
              {t("cookiesSection.link")}
            </Link>
            {t("cookiesSection.after")}
          </p>
        </section>

        <section id="browser-extension">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("browserExtension.title")}</h2>
          <p>
            {t("browserExtension.before")}{" "}
            <Link href="/extension-privacy" className="text-blue-600 hover:underline dark:text-blue-400">
              {t("browserExtension.link")}
            </Link>
            {t("browserExtension.after")}
          </p>
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
