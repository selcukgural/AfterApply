import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import tr from "../../../messages/tr.json";
import en from "../../../messages/en.json";
import type { ApplicationFlowCounts } from "@/types/api";
import type { Translate } from "./copy";
import { flowCardText } from "./cardText";

// Same stand-in as copy.test.ts: next-intl's translator over the real catalogue.
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

describe("flowCardText", () => {
  it("prints the Turkish card exactly as the shipped image reads", () => {
    const text = flowCardText(card, "tr", translator(tr, "flowCard.card"));
    expect(text.headline).toBe("86 başvurumun 50'si cevapsız kaldı.");
    expect(text.subline).toBe("Başvurularım nereye gitti? Cevap verenlerin yarısı ilk 11 günde döndü.");
    expect(text.dateRange).toBe("Haziran – Eylül 2026");
    expect(text.headers).toEqual(["BAŞVURULAR", "İLK SONUÇ", "MÜLAKATTAN SONRA"]);
    expect(text.names.unanswered).toBe("Cevapsız kaldı");
    expect(text.names.silentAfterInterview).toBe("Sessizlik");
    expect(text.cta).toBe("Kendi akışını çıkar → ekariyerim.com");
    expect(text.footnote).toBe("Kişinin kendi başvurularından · şirket adı içermez");
  });

  it("names every node in English too", () => {
    const text = flowCardText(card, "en", translator(en, "flowCard.card"));
    expect(text.headline).toBe("50 of my 86 applications went unanswered.");
    expect(Object.keys(text.names)).toHaveLength(Object.keys(counts).length);
    expect(Object.values(text.names).every((name) => name.length > 0)).toBe(true);
  });

  // The whole point of the helper: the PNG and the browser draft are fed from one place. If the
  // route went back to building its own props, the two could drift apart unnoticed.
  it("is what both the image route and the dialog's draft print", () => {
    const read = (path: string) => readFileSync(fileURLToPath(new URL(`../../${path}`, import.meta.url)), "utf8");
    expect(read("app/[locale]/og/route.tsx")).toContain("{...flowCardText(card, locale, t)}");
    expect(read("components/dashboard/FlowCardPreview.tsx")).toContain("{...flowCardText(card, locale, tCard)}");
  });
});
