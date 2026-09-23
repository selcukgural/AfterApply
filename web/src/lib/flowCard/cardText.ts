import { formatMonthRange, type FlowCard } from "./card";
import { FLOW_NODE_KEYS, flowHeadline, flowSubline, type Translate } from "./copy";
import type { FlowNodeKey } from "./layout";

/** Every word printed on the flow card, in one locale. */
export interface FlowCardText {
  headline: string;
  subline: string;
  dateRange: string;
  headers: [string, string, string];
  names: Record<FlowNodeKey, string>;
  cta: string;
  footnote: string;
}

/**
 * The card's text, built once for both places that draw the card: the `/og` route (the PNG) and
 * the share dialog (the draft drawn in the browser while that PNG renders). One source, so the
 * draft the person reads first and the image that replaces it never say different things.
 * `t` is the `flowCard.card` namespace.
 */
export function flowCardText(card: FlowCard, locale: string, t: Translate): FlowCardText {
  return {
    headline: flowHeadline(card, locale, t),
    subline: flowSubline(card, t),
    dateRange: formatMonthRange(card.from, card.to, locale),
    headers: [t("columns.applications"), t("columns.firstOutcome"), t("columns.afterInterview")],
    names: Object.fromEntries(FLOW_NODE_KEYS.map((key) => [key, t(`nodes.${key}`)])) as Record<FlowNodeKey, string>,
    cta: t("cta"),
    footnote: t("footnote"),
  };
}
