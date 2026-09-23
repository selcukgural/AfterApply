import { describe, expect, it } from "vitest";
import tr from "../../../messages/tr.json";
import en from "../../../messages/en.json";
import type { ApplicationFlowCounts } from "@/types/api";
import { countInSentence, flowCaption, flowHeadline, flowSubline, type Translate } from "./copy";

// A stand-in for next-intl's translator over the real catalogue: `{name}` placeholders and the one
// English plural the card uses.
function translator(messages: Record<string, unknown>, namespace: string): Translate {
  return (key, values = {}) => {
    const template = `${namespace}.${key}`.split(".").reduce<unknown>((node, part) => (node as Record<string, unknown>)[part], messages);
    if (typeof template !== "string") throw new Error(`missing ${namespace}.${key}`);
    return template
      .replace(/\{(\w+), plural, one \{# (\w+)\} other \{# (\w+)\}\}/g, (_m, name, one, other) =>
        `${values[name]} ${values[name] === 1 ? one : other}`)
      .replace(/\{(\w+)\}/g, (_m, name) => String(values[name]));
  };
}

const counts: ApplicationFlowCounts = {
  total: 86, unanswered: 47, awaitingReply: 0, rejectedBeforeInterview: 21, inScreening: 0,
  withdrawnBeforeInterview: 0, interviewed: 18, offer: 2, interviewInProgress: 4,
  rejectedAfterInterview: 9, silentAfterInterview: 3, withdrawnAfterInterview: 0,
};
const card = { counts, medianDays: 11, from: { year: 2026, month: 6 }, to: { year: 2026, month: 9 } };
const allAnswered = { ...card, counts: { ...counts, unanswered: 0, rejectedBeforeInterview: 68, silentAfterInterview: 0, rejectedAfterInterview: 12 } };

describe("the flow card's sentences", () => {
  it("puts the Turkish suffix on the count and nothing on the English one", () => {
    expect(countInSentence(50, "tr")).toBe("50'si");
    expect(countInSentence(1204, "tr")).toBe("1.204'ü");
    expect(countInSentence(50, "en")).toBe("50");
  });

  it("writes the headline in both languages", () => {
    expect(flowHeadline(card, "tr", translator(tr, "flowCard.card"))).toBe("86 başvurumun 50'si cevapsız kaldı.");
    expect(flowHeadline(card, "en", translator(en, "flowCard.card"))).toBe("50 of my 86 applications went unanswered.");
    expect(flowHeadline(allAnswered, "tr", translator(tr, "flowCard.card"))).toBe("86 başvurumun hepsine cevap geldi.");
  });

  it("adds the median reply time to the question, and drops it when nothing was answered", () => {
    const t = translator(tr, "flowCard.card");
    expect(flowSubline(card, t)).toBe("Başvurularım nereye gitti? Cevap verenlerin yarısı ilk 11 günde döndü.");
    expect(flowSubline({ ...card, medianDays: 0 }, t)).toBe("Başvurularım nereye gitti? Cevap verenlerin yarısı aynı gün döndü.");
    expect(flowSubline({ ...card, medianDays: null }, t)).toBe("Başvurularım nereye gitti?");
    expect(flowSubline({ ...card, medianDays: 1 }, translator(en, "flowCard.card"))).toBe(
      "Where did my applications go? Half of the replies came within 1 day.",
    );
  });

  it("offers a post text for the period", () => {
    expect(flowCaption(card, "90", "tr", translator(tr, "flowCard.share"))).toBe(
      "Son 3 ayda 86 başvuru yaptım, 50'si cevapsız kaldı. Sen de kendi akışını çıkar:",
    );
    expect(flowCaption(allAnswered, "all", "en", translator(en, "flowCard.share"))).toBe(
      "So far I sent 86 applications and every one got an answer. Map your own:",
    );
  });
});
