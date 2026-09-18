import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";

/**
 * Rules about what the catalogues *say*, not whether they agree with each other (that is
 * messages.test.ts). Each rule pins a 2026-09-13 audit finding that had crept in unnoticed: a
 * Turkish help page quoting an English button, a landing promise of a feature with no screen, a
 * deletion notice that forgot two kinds of data. None of them fail a build; only a reader notices.
 */
type MessageTree = { [key: string]: string | MessageTree };

function entries(tree: MessageTree, prefix = ""): [string, string][] {
  return Object.entries(tree).flatMap(([key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key;
    return typeof value === "string" ? [[path, value] as [string, string]] : entries(value, path);
  });
}

const trEntries = entries(tr as MessageTree);
const enEntries = entries(en as MessageTree);
const trValue = (path: string) => trEntries.find(([key]) => key === path)?.[1] ?? "";
const enValue = (path: string) => enEntries.find(([key]) => key === path)?.[1] ?? "";

describe("Turkish copy names the buttons the product actually renders", () => {
  // The extension and the app render these in Turkish ("Başvurdum", "Onayla", "Yoksay", "Hâlâ
  // bekleniyor"); a Turkish help page quoting the English label sends the reader looking for a
  // button that is not there.
  const englishLabels = ['"I Applied"', '"Confirm"', '"Dismiss"', '"Still Pending"', '"Mark as Applied"', '"New Job"'];

  it("quotes no English UI label inside help, landing or dashboard copy", () => {
    const offenders = trEntries
      .filter(([key]) => /^(help|landing|dashboard)\./.test(key))
      .filter(([, value]) => englishLabels.some((label) => value.includes(label)))
      .map(([key]) => key);
    expect(offenders).toEqual([]);
  });

  it("quotes the labels the extension and the suggestions page use", () => {
    expect(trValue("help.chromeExtension.use.step4.title")).toContain('"Başvurdum"');
    expect(trValue("help.suggestions.actions.step2.title")).toContain('"Onayla"');
    expect(trValue("help.suggestions.actions.step2.title")).toContain('"Yoksay"');
    expect(trValue("help.suggestions.how.body")).toContain('"Hâlâ bekleniyor"');
    expect(trValue("help.faq.q6.answer")).toContain('"Başvurdum"');
  });
});

describe("the catalogues promise only what ships", () => {
  it("calls nothing beta", () => {
    const offenders = [...trEntries, ...enEntries].filter(([, value]) => /\bbeta\b/i.test(value)).map(([key]) => key);
    expect(offenders).toEqual([]);
  });

  // There is no CSV uploader; the only import is LinkedIn's own export. The help page and FAQ q5
  // say so, and are the only places allowed to mention CSV at all.
  it("mentions CSV only where it explains that CSV is not supported", () => {
    const offenders = [...trEntries, ...enEntries]
      // Also allowed: the import screen itself (the LinkedIn zip *contains* a CSV, and the
      // troubleshooting text says so) and the import-source enum label the API still emits.
      .filter(([key, value]) => /\bCSV\b/.test(value) && !/^(help\.import|imports)\./.test(key) && key !== "source.CsvImport" && key !== "help.faq.q5.question" && key !== "help.faq.q5.answer")
      .map(([key]) => key);
    expect(offenders).toEqual([]);
  });

  it("separates stored CVs from the account-free scan in the FAQ", () => {
    for (const value of [trValue("help.faq.q11.answer"), enValue("help.faq.q11.answer")]) {
      expect(value).toContain("Vertex AI");
    }
  });
});

describe("every notice that enumerates account data names all of it", () => {
  // Deletion removes everything the account owns; the export contains the subset the API writes
  // (AuthService.ExportAccountDataAsync). Both lists went out of date twice — when CVs shipped and
  // when company reviews did — because nothing checked them. Salary entries (2026-09-16) were
  // the third addition, and the first one these assertions caught; candidate experiences
  // (2026-09-17) the fourth.
  const deletionNotices = [
    "privacy.rights.after",
    "settings.delete.description",
    "settings.delete.finalConfirm",
    "help.settings.delete.calloutIrreversible.body",
    "help.faq.q2.answer",
  ];
  const exportNotices = ["settings.export.description", "help.settings.export.body"];

  it.each(deletionNotices)("%s lists CVs, company reviews, salary entries, candidate experiences and feedback (tr + en)", (key) => {
    expect(trValue(key)).toMatch(/CV/);
    expect(trValue(key)).toMatch(/değerlendirme/);
    expect(trValue(key)).toMatch(/maaş/);
    expect(trValue(key)).toMatch(/aday deneyim/);
    expect(trValue(key)).toMatch(/geri bildirim/);
    expect(enValue(key)).toMatch(/CV/);
    expect(enValue(key)).toMatch(/review/);
    expect(enValue(key)).toMatch(/salar/);
    expect(enValue(key)).toMatch(/candidate experience/);
    expect(enValue(key)).toMatch(/feedback/);
  });

  // Payment records are the one kind of data that is exported but survives deletion (detached,
  // statutory retention) — both notices have to say so in their own words.
  it.each(["settings.export.description", "settings.delete.description"])("%s mentions the Pro plan payment records (tr + en)", (key) => {
    expect(trValue(key)).toMatch(/ödeme kay/);
    expect(enValue(key)).toMatch(/payment record/);
  });

  it.each(exportNotices)("%s lists company reviews, salary entries, candidate experiences and CV records but not the tracked-jobs list (tr + en)", (key) => {
    expect(trValue(key)).toMatch(/değerlendirme/);
    expect(trValue(key)).toMatch(/maaş/);
    expect(trValue(key)).toMatch(/aday deneyim/);
    expect(trValue(key)).toMatch(/CV/);
    expect(trValue(key)).not.toMatch(/takip liste/);
    expect(enValue(key)).toMatch(/review/);
    expect(enValue(key)).toMatch(/salar/);
    expect(enValue(key)).toMatch(/candidate experience/);
    expect(enValue(key)).toMatch(/CV/);
    expect(enValue(key)).not.toMatch(/tracked job/);
  });

  it("the request-log notice names salary entries and candidate experiences among the audited actions (tr + en)", () => {
    expect(trValue("privacy.dataCollection.item6")).toMatch(/maaş/);
    expect(trValue("privacy.dataCollection.item6")).toMatch(/aday deneyimi/);
    expect(enValue("privacy.dataCollection.item6")).toMatch(/salary/);
    expect(enValue("privacy.dataCollection.item6")).toMatch(/candidate experience/);
  });
});

describe("empty states point somewhere", () => {
  it.each([
    "applications.table",
    "trackedJobs.list",
    "emailSuggestions",
    "notifications",
  ])("%s has a body and a call to action next to its empty line", (namespace) => {
    for (const value of [trValue, enValue]) {
      expect(value(`${namespace}.empty`)).not.toBe("");
      expect(value(`${namespace}.emptyBody`)).not.toBe("");
      expect(value(`${namespace}.emptyCta`)).not.toBe("");
    }
  });

  it("lets a notification be cleared one at a time and all at once, in both languages", () => {
    for (const value of [trValue, enValue]) {
      for (const key of ["dismiss", "swipeHint", "clearAll", "clearAllConfirm", "clearAllYes", "clearAllCancel"]) {
        expect(value(`notifications.${key}`)).not.toBe("");
      }
      expect(value("applications.pagination.pageInfoNotifications")).toContain("{totalCount}");
    }
  });

  it("does not describe the CV dropzone by a screen position", () => {
    expect(trValue("cv.empty")).not.toMatch(/sol/i);
    expect(enValue("cv.empty")).not.toMatch(/left/i);
  });
});

describe("the closing lines of an application keep the T-series tone (2026-09-18)", () => {
  // DEVELOPMENT_PLAN.md, "Standing kurallar": calm, factual, adult. The two closing surfaces —
  // the ending line (T6) and the accepted-offer card (T7) — are where a slip would be loudest:
  // an exclamation mark on a congratulation, a "don't give up" after a rejection, a nudge
  // towards deleting the account on the way out.
  const closingCopy = [...trEntries, ...enEntries].filter(([key]) => /^applications\.detail\.(shareExperience|accepted)\./.test(key));

  it("covers both surfaces", () => {
    expect(closingCopy.map(([key]) => key)).toEqual(expect.arrayContaining(["applications.detail.accepted.title", "applications.detail.shareExperience.text"]));
  });

  it("uses no exclamation mark", () => {
    const offenders = closingCopy.filter(([, value]) => value.includes("!")).map(([key]) => key);
    expect(offenders).toEqual([]);
  });

  it("says nothing motivational", () => {
    const offenders = closingCopy
      .filter(([, value]) => /(pes etme|vazgeçme|don't give up|never give up|keep going|yaklaştır|closer to)/i.test(value))
      .map(([key]) => key);
    expect(offenders).toEqual([]);
  });

  it("leaves the account alone on the way out — the data offer is a download, never a deletion", () => {
    const offenders = closingCopy.filter(([, value]) => /(hesabını sil|hesabı sil|delete your account|delete the account)/i.test(value)).map(([key]) => key);
    expect(offenders).toEqual([]);
    for (const value of [trValue("applications.detail.accepted.keep.link"), enValue("applications.detail.accepted.keep.link")]) {
      expect(value).toMatch(/indir|download/i);
    }
  });
});

describe("company reviews have no free text (2026-09-16)", () => {
  // A review is ratings plus catalogue statements. Copy that still promises "pros and cons", a
  // title, or a moderator reading every review before it is published describes the old form.
  // The legacy rows are the one legitimate reason to say "pros and cons": where the author's own
  // list, the export/privacy text and the moderation detail explain what the old format was.
  const allowed = [
    /^companyReviews\.mine\.legacy/,
    /^companyReviews\.edit\.legacyBanner$/,
    /^companyReviews\.card\.legacy/,
    /^privacy\.companyReviews\.what$/,
    /^adminReviews\.detail\.legacy/,
    /^help\.companyReviews\.calloutModeration\.body$/,
    /^companies\.scoring\.moderation\.legacy$/,
  ];
  const reviewCopy = [...trEntries, ...enEntries].filter(([key]) =>
    /^(companyReviews|companies|adminReviews|help\.companyReviews|help\.faq\.q1[345]|privacy\.companyReviews|landing\.tools)\./.test(key),
  );

  it("promises no title, pros or cons outside the legacy explanations", () => {
    const offenders = reviewCopy
      .filter(([key]) => !allowed.some((rule) => rule.test(key)))
      .filter(([, value]) => /\b(pros|cons|artılar|eksiler|artı ve eksi)\b/i.test(value) || /\b(başlık|a title)\b/i.test(value))
      .map(([key]) => key);
    expect(offenders).toEqual([]);
  });

  it("says nowhere that a moderator reads every review before it is published", () => {
    const offenders = reviewCopy
      .filter(([key]) => !allowed.some((rule) => rule.test(key)))
      .filter(([, value]) => /(reads every review|her değerlendirmeyi .*okur|yayımlanmadan önce .*moderatör|before it is published)/i.test(value))
      .map(([key]) => key);
    expect(offenders).toEqual([]);
  });
});


describe("the refund policy says the same thing everywhere (2026-09-16)", () => {
  // The owner's policy — a full refund within seven days, used time deducted after that, a reply
  // within three business days, requests from the app or by e-mail (destek@ for Turkish readers,
  // support@ for English ones, both @ekariyerim.com) — is
  // written into the agreement, the policy page, the help centre, the FAQ, the checkout consent
  // and both refund dialogs. One of them drifting is a broken promise the reader cannot see.
  const policySurfaces = [
    "termsOfSale.sections.withdrawal.body",
    "refundPolicy.sections.withdrawal.body",
    "help.settings.billing.calloutWithdrawal.body",
    "help.weeklyJobs.refund.body",
    "help.faq.q18.answer",
    "payments.refund.body",
  ];

  it.each(policySurfaces)("%s names the seven-day window (tr + en)", (key) => {
    expect(trValue(key)).toMatch(/7 (?:\(yedi\) )?g[üu]n/i);
    expect(enValue(key)).toMatch(/7 (?:\(seven\) )?days|seven days/i);
  });

  it.each(["termsOfSale.sections.refunds.body", "refundPolicy.sections.decision.body", "help.faq.q18.answer", "payments.refund.body"])(
    "%s promises a reply within three business days (tr + en)",
    (key) => {
      expect(trValue(key)).toMatch(/3 iş günü/);
      expect(enValue(key)).toMatch(/3 business days/);
    },
  );

  it.each(["termsOfSale.sections.parties.body", "refundPolicy.sections.howToRequest.body", "refundPolicy.sections.contact.body", "help.faq.q18.answer"])(
    "%s gives the support address (tr + en)",
    (key) => {
      expect(trValue(key)).toContain("destek@ekariyerim.com");
      expect(trValue(key)).not.toContain("support@");
      expect(enValue(key)).toContain("support@ekariyerim.com");
      expect(enValue(key)).not.toContain("destek@");
    },
  );

  it("carries no placeholder or draft marker in the legal texts", () => {
    for (const [key, value] of [...trEntries, ...enEntries]) {
      if (!key.startsWith("termsOfSale.") && !key.startsWith("refundPolicy.")) continue;
      expect(value, key).not.toMatch(/\[[^\]]*(gelecek|to come)[^\]]*\]/i);
      expect(value, key).not.toMatch(/taslak|\bdraft\b/i);
    }
    expect(trValue("legalDraft.notice")).toBe("");
  });

  it("the checkout consent keeps the statutory waiver and names the seven-day right", () => {
    // The waiver sentence is what the Regulation requires for a service that starts at once; the
    // contractual seven-day right is what makes it fair. Both, in one sentence, in both languages.
    expect(trValue("payments.checkout.terms")).toMatch(/cayma hakkım/);
    expect(trValue("payments.checkout.terms")).toMatch(/7 gün/);
    expect(enValue("payments.checkout.terms")).toMatch(/withdrawal/);
    expect(enValue("payments.checkout.terms")).toMatch(/7 days/);
  });
});

describe("the dashboard does not keep score against the reader (T-series, 2026-09-18)", () => {
  // A long job search turns the board into a monument to what did not happen. The words below
  // each turn a fact into a verdict — "win rate" makes every rejection a loss, "only 12%" grades
  // the person, "don't give up" tells them they were about to. Whatever the dashboard says, it
  // says without them. Word boundaries so "kazanmak" is caught and "kazanç" (earnings) is not.
  const forbiddenTr = [/\bkazanma\b/i, /\bkazandın\b/i, /\bkaybettin\b/i, /\bkaybedilen\b/i, /\byalnızca\b/i, /\bsadece\b/i, /\bpes\b/i, /\bmaalesef\b/i, /ne yazık ki/i];
  const forbiddenEn = [/\bwin rate\b/i, /\bwon\b/i, /\blost\b/i, /\bonly\b/i, /\bunfortunately\b/i, /don't give up/i, /\bkeep going\b/i];

  const offenders = (list: [string, string][], patterns: RegExp[]) =>
    list
      .filter(([key]) => key.startsWith("dashboard."))
      .filter(([, value]) => patterns.some((pattern) => pattern.test(value)))
      .map(([key]) => key);

  it("uses none of the verdict words in Turkish", () => {
    expect(offenders(trEntries, forbiddenTr)).toEqual([]);
  });

  it("uses none of the verdict words in English", () => {
    expect(offenders(enEntries, forbiddenEn)).toEqual([]);
  });

  it("names the outcome card's good half neutrally", () => {
    expect(trValue("dashboard.outcome.winRate")).toBe("Olumlu sonuç");
    expect(enValue("dashboard.outcome.winRate")).toBe("Positive outcomes");
  });

  it("only ever says what is in motion in the headline", () => {
    // The first sentence stands alone; the second exists in three shapes and none of them can
    // say "0 offers" — a zero is expressed by the sentence not being there (lib/dashboard/tone).
    for (const value of [trValue("dashboard.headline"), enValue("dashboard.headline")]) {
      expect(value).not.toMatch(/\{interviews|\{offers/);
    }
    for (const lang of [trValue, enValue]) {
      expect(lang("dashboard.headlineProgress.both")).toMatch(/\{interviews/);
      expect(lang("dashboard.headlineProgress.both")).toMatch(/\{offers/);
      expect(lang("dashboard.headlineProgress.interviews")).not.toMatch(/\{offers/);
      expect(lang("dashboard.headlineProgress.offers")).not.toMatch(/\{interviews/);
    }
  });

  it("reads the silence against the person's own norm, not against a threshold", () => {
    expect(trValue("dashboard.reminders.usualReply")).toContain("{median}");
    expect(enValue("dashboard.reminders.usualReply")).toContain("{median}");
  });
});
