import type { FlowCard } from "./card";
import { unansweredTotal } from "./card";
import type { FlowNodeKey } from "./layout";
import { turkishPossessiveSuffix } from "./turkish";
import type { FlowPeriod } from "@/types/api";

/** A next-intl translator narrowed to what the card needs, so this stays testable without one. */
export type Translate = (key: string, values?: Record<string, string | number>) => string;

/**
 * "50'si" in Turkish, "50" elsewhere: the count as it sits in "86 başvurumun 50'si cevapsız kaldı".
 * Formatted with the locale's digit grouping first ("1.204'ü").
 */
export function countInSentence(count: number, locale: string): string {
  const formatted = new Intl.NumberFormat(locale).format(count);
  return locale === "tr" ? `${formatted}${turkishPossessiveSuffix(count)}` : formatted;
}

/** The card's big line. `t` is scoped to `flowCard.card`. */
export function flowHeadline(card: FlowCard, locale: string, t: Translate): string {
  const unanswered = unansweredTotal(card.counts);
  const total = new Intl.NumberFormat(locale).format(card.counts.total);
  return unanswered === 0
    ? t("headlineAllAnswered", { total })
    : t("headline", { total, unanswered: countInSentence(unanswered, locale) });
}

/** "Başvurularım nereye gitti? Cevap verenlerin yarısı ilk 11 günde döndü." — the median half drops when nothing was answered. */
export function flowSubline(card: FlowCard, t: Translate): string {
  const question = t("question");
  if (card.medianDays === null) return question;
  const median = card.medianDays === 0 ? t("medianSameDay") : t("median", { days: card.medianDays });
  return `${question} ${median}`;
}

/** The post text offered next to a downloaded image or a shared link. `t` is scoped to `flowCard.share`. */
export function flowCaption(card: FlowCard, period: FlowPeriod, locale: string, t: Translate): string {
  const unanswered = unansweredTotal(card.counts);
  const values = {
    period: t(`captionPeriod.${period}`),
    total: new Intl.NumberFormat(locale).format(card.counts.total),
    unanswered: countInSentence(unanswered, locale),
  };
  return unanswered === 0 ? t("captionAllAnswered", values) : t("caption", values);
}

export const FLOW_NODE_KEYS: readonly FlowNodeKey[] = [
  "total",
  "unanswered",
  "awaitingReply",
  "rejectedBeforeInterview",
  "inScreening",
  "withdrawnBeforeInterview",
  "interviewed",
  "offer",
  "interviewInProgress",
  "rejectedAfterInterview",
  "silentAfterInterview",
  "withdrawnAfterInterview",
];
