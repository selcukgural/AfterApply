import { describe, expect, it } from "vitest";
import {
  SEO_CHECK_SOURCES,
  containsKeyword,
  effectiveTitle,
  firstParagraph,
  headings,
  imageAlts,
  isDescriptiveSlug,
  linkCountOf,
  seoChecklist,
  slugContainsKeyword,
  seoScore,
  slugFromTitle,
  textOfHtml,
  wordCount,
  type SeoInput,
} from "./seoChecks";

describe("slugFromTitle", () => {
  it("folds Turkish letters the way the API's generator does", () => {
    expect(slugFromTitle("İşe Alım Sürecinde Ghosting: Neden Cevap Alamıyorsunuz?")).toBe("ise-alim-surecinde-ghosting-neden-cevap-alamiyorsunuz");
    expect(slugFromTitle("IĞDIR'da çalışmak — öğrenci için")).toBe("igdir-da-calismak-ogrenci-icin");
  });

  it("falls back, caps and dodges reserved segments", () => {
    expect(slugFromTitle("   ")).toBe("post");
    expect(slugFromTitle("???")).toBe("post");
    expect(slugFromTitle("admin")).toBe("admin-2");
    expect(slugFromTitle("a".repeat(150)).length).toBe(100);
    expect(slugFromTitle(`${"a".repeat(99)} b`)).toBe("a".repeat(99));
  });
});

describe("html reading", () => {
  const html = "<h2>Üç ana bulgu</h2><p></p><p>Başvuruların &amp; yüzde <strong>sekseni</strong> yanıtsız.</p><h3>Red mailleri</h3><img src=\"/a\" alt=\"Grafik\"><img src=\"/b\" alt=\"\"><img src='/c'>";

  it("turns html into words", () => {
    expect(textOfHtml("<p>Merhaba&nbsp;dünya</p>")).toBe("Merhaba dünya");
    expect(wordCount(html)).toBe(9);
    expect(wordCount("")).toBe(0);
  });

  it("finds the first non-empty paragraph and the headings", () => {
    expect(firstParagraph(html)).toBe("Başvuruların & yüzde sekseni yanıtsız.");
    expect(firstParagraph("<h2>Only</h2>")).toBe("");
    expect(headings(html)).toEqual(["Üç ana bulgu", "Red mailleri"]);
  });

  it("lists every image's alt, empty for the ones without", () => {
    expect(imageAlts(html)).toEqual(["Grafik", "", ""]);
  });
});

describe("keywords", () => {
  it("matches case-folded, Turkish i's included", () => {
    expect(containsKeyword("İşe Alımda Ghosting", "işe alimda ghosting")).toBe(true);
    expect(containsKeyword("Ghosting", "")).toBe(false);
  });

  it("counts a phrase whose every word is in the text, with other words between — Google matches words, not strings", () => {
    expect(containsKeyword("İşe Alım Sürecinde Ghosting: Neden Cevap Alamıyorsunuz?", "işe alım ghosting")).toBe(true);
    expect(containsKeyword("İşe Alım Sürecinde Ghosting", "maaş pazarlığı")).toBe(false);
    expect(containsKeyword("İşe Alım Sürecinde Ghosting", "işe alım maaş")).toBe(false);
  });
});

describe("slugContainsKeyword", () => {
  it("looks for the keyword's words among the slug's segments", () => {
    expect(slugContainsKeyword("ise-alim-surecinde-ghosting", "işe alım ghosting")).toBe(true);
    expect(slugContainsKeyword("ise-alim-surecinde-ghosting", "maaş")).toBe(false);
    expect(slugContainsKeyword("ise-alim", "")).toBe(false);
  });
});

describe("linkCountOf", () => {
  it("counts anchors with an href", () => {
    expect(linkCountOf('<p><a href="/tr/guide/x">rehber</a> ve <a href=\'https://e.com\'>dış</a> <a name="n">yok</a></p>')).toBe(2);
    expect(linkCountOf("<p>none</p>")).toBe(0);
  });
});

describe("isDescriptiveSlug", () => {
  it("wants words joined by hyphens, not an id or the fallback", () => {
    expect(isDescriptiveSlug("ise-alim-surecinde-ghosting")).toBe(true);
    expect(isDescriptiveSlug("post")).toBe(false);
    expect(isDescriptiveSlug("ghosting")).toBe(false);
    expect(isDescriptiveSlug("2026-09")).toBe(false);
    expect(isDescriptiveSlug("")).toBe(false);
  });
});

describe("seoChecklist — Google's items plus the field's practices", () => {
  const base: SeoInput = {
    title: "İşe Alım Sürecinde Ghosting: Neden Cevap Alamıyorsunuz?",
    seoTitle: "",
    excerpt: "Başvuruların büyük çoğunluğu yanıtsız kalıyor; kendi verimizden üç bulgu, ne zaman ve ne bekleyebileceğiniz.",
    slug: "ise-alim-surecinde-ghosting-neden-cevap-alamiyorsunuz",
    primaryKeyword: "",
    secondaryKeywords: [],
    coverAlt: "",
    hasCover: false,
    contentHtml: "<p>Ghosting nedir?</p>",
  };
  const byId = (input: SeoInput) => Object.fromEntries(seoChecklist(input).map((c) => [c.id, c]));

  it("lists the base items without a keyword, skipping the image ones that do not apply", () => {
    expect(seoChecklist(base).map((c) => c.id)).toEqual(["title", "description", "searchTerms", "headings", "links", "url", "structuredData"]);
  });

  it("adds the placement items once there is a keyword, each with a basis and a source", () => {
    const ids = seoChecklist({ ...base, primaryKeyword: "ghosting" }).map((c) => c.id);
    expect(ids).toEqual(["title", "description", "searchTerms", "keywordInTitle", "keywordInUrl", "keywordEarly", "headings", "links", "url", "structuredData"]);
    for (const id of ids) {
      expect(["google", "practice"]).toContain(SEO_CHECK_SOURCES[id].basis);
      expect(SEO_CHECK_SOURCES[id].url).toMatch(/^https:\/\//);
    }
    // Google-documented items point at Google's own pages.
    for (const id of ids) if (SEO_CHECK_SOURCES[id].basis === "google") expect(SEO_CHECK_SOURCES[id].url).toContain("developers.google.com/search/docs/");
  });

  it("has no word-count item and no secondary-keyword target — Google rules out the first, nothing backs the second", () => {
    const ids = seoChecklist({ ...base, primaryKeyword: "ghosting", secondaryKeywords: [] }).map((c) => c.id);
    expect(ids).not.toContain("wordCount");
    expect(ids).not.toContain("secondaryKeywords");
  });

  it("places the keyword: title, address, opening paragraph", () => {
    const post = {
      ...base,
      primaryKeyword: "işe alım ghosting",
      contentHtml: "<p>İşe alım sürecinde ghosting yaşayan adaylar…</p><h2>Neden olur</h2><p><a href=\"/tr/guide/x\">rehber</a></p>",
    };
    const checks = byId(post);
    expect(checks.keywordInTitle.status).toBe("ok");
    expect(checks.keywordInUrl.status).toBe("ok");
    expect(checks.keywordEarly.status).toBe("ok");
    expect(checks.links).toMatchObject({ status: "ok", value: 1 });
    const elsewhere = byId({ ...post, primaryKeyword: "maaş pazarlığı" });
    expect(elsewhere.keywordInTitle.status).toBe("warn");
    expect(elsewhere.keywordInUrl.status).toBe("warn");
    expect(elsewhere.keywordEarly.status).toBe("warn");
  });

  it("uses the SEO title over the headline for the title item", () => {
    expect(effectiveTitle(base)).toBe(base.title);
    expect(byId(base).title.status).toBe("ok");
    expect(byId({ ...base, seoTitle: "x".repeat(61) }).title).toMatchObject({ status: "warn", value: 61 });
    expect(byId({ ...base, title: "", seoTitle: " " }).title.status).toBe("missing");
  });

  it("grades the description by what a snippet shows", () => {
    expect(byId(base).description.status).toBe("ok");
    expect(byId({ ...base, excerpt: "Kısa." }).description.status).toBe("warn");
    expect(byId({ ...base, excerpt: "" }).description.status).toBe("missing");
  });

  it("asks for the readers' search terms once, then looks for them in the title or the body", () => {
    expect(byId(base).searchTerms.status).toBe("missing");
    expect(byId({ ...base, primaryKeyword: "işe alım ghosting" }).searchTerms.status).toBe("ok");
    expect(byId({ ...base, primaryKeyword: "ghosting nedir" }).searchTerms.status).toBe("ok");
    expect(byId({ ...base, primaryKeyword: "maaş pazarlığı" }).searchTerms.status).toBe("warn");
  });

  it("wants at least one subheading, one link and a descriptive address", () => {
    expect(byId(base).headings).toMatchObject({ status: "warn", value: 0 });
    expect(byId(base).links).toMatchObject({ status: "warn", value: 0 });
    expect(byId({ ...base, contentHtml: "<h2>Üç bulgu</h2><p>x</p>" }).headings).toMatchObject({ status: "ok", value: 1 });
    expect(byId({ ...base, slug: "post" }).url.status).toBe("warn");
    expect(byId(base).url.status).toBe("ok");
  });

  it("asks for a cover alt only with a cover, and for body alts only with images", () => {
    expect(byId({ ...base, hasCover: true }).coverAlt.status).toBe("missing");
    expect(byId({ ...base, hasCover: true, coverAlt: "Gradyan" }).coverAlt.status).toBe("ok");
    const images = { ...base, contentHtml: '<p>x</p><img alt="a"><img alt="">' };
    expect(byId(images).bodyImageAlts).toMatchObject({ status: "warn", value: 1 });
    expect(byId({ ...base, contentHtml: '<p>x</p><img alt="a">' }).bodyImageAlts.status).toBe("ok");
  });

  it("always reports the structured data the page emits as covered", () => {
    expect(byId(base).structuredData.status).toBe("ok");
  });
});

describe("seoScore", () => {
  it("bands by the share of green checks", () => {
    const ok = { id: "title" as const, status: "ok" as const };
    const warn = { id: "title" as const, status: "warn" as const };
    expect(seoScore([ok, ok, ok, ok, warn])).toEqual({ ok: 4, total: 5, band: "good" });
    expect(seoScore([ok, warn])).toEqual({ ok: 1, total: 2, band: "fair" });
    expect(seoScore([warn, warn, ok])).toEqual({ ok: 1, total: 3, band: "poor" });
    expect(seoScore([])).toEqual({ ok: 0, total: 0, band: "poor" });
  });
});
