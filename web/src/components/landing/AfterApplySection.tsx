import { getTranslations } from "next-intl/server";
import { ScrollReveal } from "@/components/landing/ScrollReveal";

/**
 * The problem and the reason the product exists, in one band (2026-09-22).
 *
 * These used to be two full sections back to back — ProblemSection ("your history is spread
 * across four places") and this one ("the real story starts after you apply") — each with its own
 * eyebrow, its own heading and its own py-20. They make one argument, not two: the history is
 * scattered *because* nothing follows an application after it is sent. Merging them costs a screen
 * and a heading, and nothing else; the two paragraphs, the source chips and both timelines are the
 * ones that stood here before.
 *
 * Two beats did go: the italic "What happened to this one again?" that closed the problem section,
 * which the silent timeline below answers in the same words, and this section's own eyebrow, since
 * one section carries one.
 *
 * Keeps `id="how-it-works"` — the header, the footer, the hero's secondary button and the final
 * call to action all point at it.
 */
export async function AfterApplySection() {
  const t = await getTranslations("landing.afterApply");
  const tProblem = await getTranslations("landing.problem");

  const sources = [
    tProblem("sourceLinkedin"),
    tProblem("sourceJobSites"),
    tProblem("sourceCompanyPages"),
    tProblem("sourceOther"),
  ];

  const happyPath = [
    t("happyApplied"),
    t("happyScreening"),
    t("happyInterview"),
    t("happyTechnical"),
    t("happyFinal"),
    t("happyOffer"),
    t("happyOutcome"),
  ];

  return (
    <section id="how-it-works" className="scroll-mt-20 border-t border-gray-200 bg-gray-50 py-20 dark:border-gray-800 dark:bg-gray-900/40">
      <ScrollReveal className="mx-auto flex max-w-5xl flex-col gap-12 px-4">
        <div className="flex flex-col items-center gap-4 text-center">
          <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{tProblem("eyebrow")}</span>
          <h2 className="text-3xl font-semibold text-gray-900 sm:text-4xl dark:text-gray-100">{tProblem("title")}</h2>
          <p className="max-w-2xl text-base text-gray-600 dark:text-gray-400">{tProblem("body1")}</p>
          <p className="max-w-2xl text-base text-gray-600 dark:text-gray-400">{tProblem("body2")}</p>

          <div className="flex flex-col items-center gap-3 pt-2">
            <div className="flex flex-wrap justify-center gap-2">
              {sources.map((source) => (
                <span
                  key={source}
                  className="rounded-full border border-gray-200 bg-white px-3 py-1 text-sm text-gray-600 dark:border-gray-800 dark:bg-gray-900 dark:text-gray-400"
                >
                  {source}
                </span>
              ))}
            </div>
            <span aria-hidden="true" className="text-gray-300 dark:text-gray-700">
              ↓
            </span>
            <p className="text-sm font-medium text-gray-700 dark:text-gray-300">{tProblem("outcome")}</p>
          </div>
        </div>

        {/* The turn: from "where the history went" to "what happens to one application". An h3
            rather than a second h2 — the band has one heading, and this is the line under it. */}
        <h3 className="text-center text-2xl font-semibold text-gray-900 sm:text-3xl dark:text-gray-100">{t("title")}</h3>

        <div className="grid gap-8 md:grid-cols-2">
          <div className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
            <ol className="flex flex-col gap-3">
              {happyPath.map((step, index) => (
                <li key={step} className="flex items-center gap-3 text-sm">
                  <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-blue-100 text-xs font-medium text-blue-700 dark:bg-blue-900/40 dark:text-blue-300">
                    {index + 1}
                  </span>
                  <span className="text-gray-800 dark:text-gray-200">{step}</span>
                </li>
              ))}
            </ol>
          </div>

          <div className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
            <ol className="flex flex-col gap-3">
              <li className="flex items-center gap-3 text-sm">
                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-blue-100 text-xs font-medium text-blue-700 dark:bg-blue-900/40 dark:text-blue-300">
                  1
                </span>
                <span className="text-gray-800 dark:text-gray-200">{t("silentApplied")}</span>
              </li>
              {[0, 1, 2].map((i) => (
                <li key={i} className="flex items-center gap-3 text-sm">
                  <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-amber-100 text-xs font-medium text-amber-700 dark:bg-amber-900/30 dark:text-amber-300">
                    {i + 2}
                  </span>
                  <span className="text-gray-500 dark:text-gray-500">{t("silentWaiting")}</span>
                </li>
              ))}
              <li className="flex items-center gap-3 text-sm">
                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-gray-100 text-xs font-medium text-gray-500 dark:bg-gray-800 dark:text-gray-400">
                  ?
                </span>
                <span className="font-medium text-gray-900 italic dark:text-gray-100">&ldquo;{t("silentQuestion")}&rdquo;</span>
              </li>
            </ol>
          </div>
        </div>

        <p className="mx-auto max-w-2xl text-center text-sm text-gray-500 dark:text-gray-500">{t("note")}</p>
      </ScrollReveal>
    </section>
  );
}
