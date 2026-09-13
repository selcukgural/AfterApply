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
  // when company reviews did — because nothing checked them.
  const deletionNotices = [
    "privacy.rights.after",
    "settings.delete.description",
    "settings.delete.finalConfirm",
    "help.settings.delete.calloutIrreversible.body",
    "help.faq.q2.answer",
  ];
  const exportNotices = ["settings.export.description", "help.settings.export.body"];

  it.each(deletionNotices)("%s lists CVs, company reviews and feedback (tr + en)", (key) => {
    expect(trValue(key)).toMatch(/CV/);
    expect(trValue(key)).toMatch(/değerlendirme/);
    expect(trValue(key)).toMatch(/geri bildirim/);
    expect(enValue(key)).toMatch(/CV/);
    expect(enValue(key)).toMatch(/review/);
    expect(enValue(key)).toMatch(/feedback/);
  });

  it.each(exportNotices)("%s lists company reviews and CV records but not the tracked-jobs list (tr + en)", (key) => {
    expect(trValue(key)).toMatch(/değerlendirme/);
    expect(trValue(key)).toMatch(/CV/);
    expect(trValue(key)).not.toMatch(/takip liste/);
    expect(enValue(key)).toMatch(/review/);
    expect(enValue(key)).toMatch(/CV/);
    expect(enValue(key)).not.toMatch(/tracked job/);
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

  it("does not describe the CV dropzone by a screen position", () => {
    expect(trValue("cv.empty")).not.toMatch(/sol/i);
    expect(enValue("cv.empty")).not.toMatch(/left/i);
  });
});
