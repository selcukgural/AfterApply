import { useTranslations } from "next-intl";
import { BrowserFrame } from "@/components/landing/BrowserFrame";
import { SampleDataBadge } from "@/components/landing/SampleDataBadge";
import { LogoMark } from "@/components/layout/Logo";

/**
 * The extension's popup, open over a LinkedIn job page, as markup with demo values.
 *
 * The values are the ones the Web Store screenshots use (extension/store-listing/screenshots/
 * scene-job.html), so the store and the site show the same job. The field labels come from the
 * catalogue under the extension's own words — a Turkish visitor sees the popup a Turkish user gets.
 *
 * Accessibility: the whole thing is one picture. `role="img"` with a one-sentence label on the
 * outside, `aria-hidden` on the markup inside, and — because hidden regions must not contain
 * focusable elements — the fields and the button are divs, not inputs and buttons. The sample-data
 * badge sits outside the picture so it is announced.
 */
const DEMO = {
  url: "www.linkedin.com/jobs/view/4123456789",
  source: "LinkedIn",
  company: "Acme Yazılım",
  jobTitle: "Senior Backend Engineer",
  location: "İstanbul, Türkiye",
  contact: "Elif Demir",
} as const;

const SKELETON_WIDTHS = ["w-11/12", "w-5/6", "w-7/12", "w-10/12", "w-5/6", "w-11/12"];

export function ExtensionPopupMock({ className = "" }: { className?: string }) {
  const t = useTranslations("landing.extension.mock");

  const fields = [
    { label: t("company"), value: DEMO.company, optional: false },
    { label: t("jobTitle"), value: DEMO.jobTitle, optional: false },
    { label: t("location"), value: DEMO.location, optional: false },
    { label: t("contact"), value: DEMO.contact, optional: true },
  ];

  return (
    <div className={`relative ${className}`}>
      <SampleDataBadge className="-bottom-2.5 left-3" />
      <div role="img" aria-label={t("ariaLabel")}>
        <div aria-hidden="true">
          <BrowserFrame url={DEMO.url}>
            {/* A grey page behind the popup. Static bars, not .aa-skeleton: a shimmer would say
                "loading", and nothing here is. */}
            <div className="flex min-h-[27rem] flex-col gap-3 p-6 sm:p-8">
              <div className="h-6 w-3/5 rounded-md bg-gray-300 dark:bg-gray-700" />
              <div className="mb-3 h-3.5 w-2/5 rounded-md bg-gray-200 dark:bg-gray-800" />
              {SKELETON_WIDTHS.map((width, index) => (
                <div key={index} className={`h-2.5 rounded bg-gray-200 dark:bg-gray-800 ${width}`} />
              ))}
            </div>

            <div className="absolute top-4 right-4 w-64 rounded-xl border border-gray-200 bg-white shadow-2xl sm:w-72 dark:border-gray-700 dark:bg-gray-900">
              <span className="absolute -top-2 right-5 h-4 w-4 rotate-45 border-t border-l border-gray-200 bg-white dark:border-gray-700 dark:bg-gray-900" />
              <div className="relative flex items-center gap-2 border-b border-gray-100 px-3.5 py-3 dark:border-gray-800">
                <LogoMark className="h-5 w-5" />
                <span className="flex-1 text-sm font-semibold text-gray-900 dark:text-gray-100">e-kariyerim</span>
                <span className="h-7 w-7 rounded-lg border border-gray-200 dark:border-gray-800" />
              </div>
              <div className="flex flex-col gap-2.5 p-3.5">
                <span className="inline-flex w-fit items-center gap-1.5 rounded-full bg-accent-wash px-2 py-0.5 text-[11px] font-semibold text-accent-ink">
                  <span className="h-1.5 w-1.5 rounded-full bg-current" />
                  {DEMO.source}
                </span>
                {fields.map((field) => (
                  <div key={field.label}>
                    <p className="text-xs font-semibold text-gray-700 dark:text-gray-300">
                      {field.label}
                      {field.optional ? (
                        <span className="font-normal text-gray-500 dark:text-gray-400"> ({t("optional")})</span>
                      ) : null}
                    </p>
                    <div className="mt-1 rounded-lg border border-gray-300 bg-white px-2.5 py-1.5 text-[13px] text-gray-900 dark:border-gray-700 dark:bg-gray-950 dark:text-gray-100">
                      {field.value}
                    </div>
                  </div>
                ))}
                <div className="mt-1 rounded-lg bg-accent px-3 py-2 text-center text-[13px] font-semibold text-white">
                  {t("submit")}
                </div>
              </div>
            </div>
          </BrowserFrame>
        </div>
      </div>
    </div>
  );
}
