"use client";

import { useMemo, useRef, useState, type KeyboardEvent } from "react";
import { useLocale, useTranslations } from "next-intl";
import { compareOffers, type SalaryBasis } from "@/lib/offerCompare/compare";
import { emptyDraft, exampleDrafts, toOffer, type DraftField, type OfferDraft } from "@/lib/offerCompare/input";
import { OfferForm } from "./OfferForm";
import { OfferVerdict } from "./OfferVerdict";
import { MonthlyTakeHome } from "./MonthlyTakeHome";

const LETTERS = ["A", "B", "C"] as const;
const MAX_OFFERS = LETTERS.length;
const MIN_OFFERS = 2;

/**
 * The offer comparison, all of it in the browser: the form, the verdict and the month chart read
 * one list of drafts held here and nowhere else — no request, no storage, gone on reload. That is
 * the page's promise ("hiçbir şey kaydedilmez"), so nothing may be added here that keeps it.
 */
export function OfferComparison() {
  const t = useTranslations("offerCompare");
  const locale = useLocale();
  const defaultName = (letter: string) => t("offers.defaultName", { letter });

  const examples = () => exampleDrafts(locale, [defaultName("A"), defaultName("B")]);
  const [drafts, setDrafts] = useState<OfferDraft[]>(examples);
  const [active, setActive] = useState(0);
  const [chartIndex, setChartIndex] = useState(0);
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([]);

  const offers = useMemo(() => drafts.map((draft) => toOffer(draft, locale)), [drafts, locale]);
  const comparison = useMemo(() => compareOffers(offers), [offers]);
  const names = offers.map((offer, index) => offer.name.trim() || defaultName(LETTERS[index]));
  const current = Math.min(active, drafts.length - 1);

  const update = (index: number, patch: Partial<OfferDraft>) =>
    setDrafts((list) => list.map((draft, i) => (i === index ? { ...draft, ...patch } : draft)));

  const add = () => {
    if (drafts.length >= MAX_OFFERS) return;
    const letter = LETTERS.find((l) => !drafts.some((draft) => draft.name === defaultName(l))) ?? LETTERS[drafts.length];
    // The office cost is the visitor's own figure, not the offer's: the new offer starts with it.
    setDrafts((list) => [...list, emptyDraft(defaultName(letter), list[0]?.officeDayCost ?? "0")]);
    setActive(drafts.length);
  };

  const remove = (index: number) => {
    if (drafts.length <= MIN_OFFERS) return;
    setDrafts((list) => list.filter((_, i) => i !== index));
    setActive(0);
    setChartIndex(0);
  };

  const reset = () => {
    setDrafts(examples());
    setActive(0);
    setChartIndex(0);
  };

  // The WAI-ARIA tabs pattern: arrows move between tabs, focus follows the selection.
  const onTabKey = (event: KeyboardEvent<HTMLDivElement>) => {
    const step = event.key === "ArrowRight" ? 1 : event.key === "ArrowLeft" ? -1 : 0;
    if (step === 0) return;
    event.preventDefault();
    const next = (current + step + drafts.length) % drafts.length;
    setActive(next);
    tabRefs.current[next]?.focus();
  };

  return (
    <div className="flex flex-col gap-8">
      <div className="grid gap-8 lg:grid-cols-[minmax(0,520px)_minmax(0,1fr)] lg:items-start">
        <section className="flex flex-col rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
          <div className="flex items-end gap-1 overflow-x-auto rounded-t-xl border-b border-gray-200 bg-gray-50 px-2 pt-2 dark:border-gray-800 dark:bg-gray-950/40">
            <div role="tablist" aria-label={t("offers.tabsLabel")} onKeyDown={onTabKey} className="flex gap-1">
              {drafts.map((_, index) => (
                <button
                  key={index}
                  ref={(element) => {
                    tabRefs.current[index] = element;
                  }}
                  type="button"
                  role="tab"
                  id={`offer-tab-${index}`}
                  aria-selected={index === current}
                  aria-controls="offer-panel"
                  tabIndex={index === current ? 0 : -1}
                  onClick={() => setActive(index)}
                  className={`-mb-px h-11 max-w-[11rem] shrink-0 truncate rounded-t-md border px-4 text-sm font-semibold ${
                    index === current
                      ? "border-gray-200 border-b-white bg-white text-gray-900 dark:border-gray-800 dark:border-b-gray-900 dark:bg-gray-900 dark:text-gray-100"
                      : "border-transparent text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100"
                  }`}
                >
                  {names[index]}
                </button>
              ))}
            </div>
            {drafts.length < MAX_OFFERS && (
              <button
                type="button"
                onClick={add}
                className="h-11 shrink-0 px-3 text-sm font-medium text-accent-ink hover:underline"
              >
                {t("offers.add")}
              </button>
            )}
          </div>

          <div id="offer-panel" role="tabpanel" aria-labelledby={`offer-tab-${current}`} className="flex flex-col gap-5 p-4 sm:p-6">
            <OfferForm
              draft={drafts[current]}
              onChange={(field: DraftField, value: string) => update(current, { [field]: value })}
              onBasisChange={(basis: SalaryBasis) => update(current, { basis })}
            />
            <div className="flex flex-wrap items-center justify-between gap-3 border-t border-gray-100 pt-4 text-sm dark:border-gray-800">
              <p className="text-gray-500 dark:text-gray-400">{t("examplesNote")}</p>
              <div className="flex gap-3">
                {drafts.length > MIN_OFFERS && (
                  <button type="button" onClick={() => remove(current)} className="font-medium text-crit-ink hover:underline">
                    {t("offers.remove")}
                  </button>
                )}
                <button type="button" onClick={reset} className="font-medium text-accent-ink hover:underline">
                  {t("offers.reset")}
                </button>
              </div>
            </div>
          </div>
        </section>

        <OfferVerdict names={names} comparison={comparison} />
      </div>

      <MonthlyTakeHome
        offers={offers.map((offer, index) => ({ ...offer, name: names[index] }))}
        comparison={comparison}
        chartIndex={chartIndex}
        onChartIndexChange={setChartIndex}
      />
    </div>
  );
}
