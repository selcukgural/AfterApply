import { readdirSync, readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

/**
 * What the landing page promised on 2026-09-12 when the extension moved to the first screen.
 *
 * Source scans rather than render tests, like heroCvDropzone.contract.test.ts: this suite runs in
 * node with no DOM. Each block pins a rule from DECISIONS.md that would otherwise be a matter of
 * remembering — the section order that carries the promise, "components, not screenshots" for
 * every visual, a tab strip that keeps its state out of the URL and the browser's storage, and an
 * outbound store link that never leaks a referrer.
 */
const LANDING_DIR = path.join(process.cwd(), "src/components/landing");

const read = (relative: string) => readFileSync(path.join(process.cwd(), relative), "utf8");
const readLanding = (file: string) => readFileSync(path.join(LANDING_DIR, file), "utf8");

/** JSX and block comments explain the rules in the same words the tests look for. */
const stripComments = (source: string) => source.replace(/\{\/\*[\s\S]*?\*\/\}/g, "").replace(/\/\*[\s\S]*?\*\//g, "");

describe("the landing page order", () => {
  const page = stripComments(read("src/app/[locale]/page.tsx"));

  const indexOf = (component: string) => {
    const index = page.indexOf(`<${component} />`);
    expect(index, `${component} is rendered on the landing page`).toBeGreaterThan(-1);
    return index;
  };

  /** hero → tools → why → numbers → import → close: the promise first, then how the list fills
   *  itself. Narrowed on 2026-09-22 (report item 0.4) from ten sections to seven. */
  it("keeps the tools strip directly under the hero and the 2026-09-08 order after it", () => {
    const order = [
      "HeroSection",
      "ToolsStrip",
      "AfterApplySection",
      "AnalyticsSection",
      "LinkedInImportSection",
      "FinalCtaSection",
    ].map(indexOf);

    expect(order).toEqual([...order].sort((a, b) => a - b));
  });

  /** The three sections that said something the page had already said. Each one's content is
   *  still on the page — merged into a neighbour — so this pins the merge, not a deletion. */
  it("renders no separate problem, feature-grid or privacy section", () => {
    for (const gone of ["ProblemSection", "FeaturesSection", "PrivacySection"]) {
      expect(page).not.toContain(`<${gone} />`);
    }
  });

  // 2026-09-18 (growth audit finding 11): the vision, mission and roadmap sections moved to the
  // about page, and the site's running totals sit right under the tools strip, as one line.
  it("carries the stats strip under the tools strip and no vision/mission/roadmap section", () => {
    expect(indexOf("SiteStatsStrip")).toBeGreaterThan(indexOf("ToolsStrip"));
    expect(indexOf("SiteStatsStrip")).toBeLessThan(indexOf("AfterApplySection"));
    for (const gone of ["VisionSection", "MissionSection", "RoadmapSection"]) {
      expect(page).not.toContain(`<${gone} />`);
    }
  });

  it("points at the about page from the final call to action", () => {
    expect(stripComments(readLanding("FinalCtaSection.tsx"))).toContain("aboutPath(locale)");
  });
});

describe("the stats strip", () => {
  const source = stripComments(readLanding("SiteStatsStrip.tsx"));

  it("draws nothing when no figure cleared the threshold, and never a tile", () => {
    expect(source).toContain("if (figures.length === 0) return null;");
    expect(source).not.toMatch(/text-[3-5]xl/);
  });
});

describe("the tools strip", () => {
  const source = stripComments(readLanding("ToolsStrip.tsx"));

  it("opens on the extension, and lists it first", () => {
    expect(source).toContain('useState<Tool>("extension")');
    expect(source).toContain('["extension", "companies", "benchmark", "offer", "cv"]');
  });

  it("is a tab group, not three links", () => {
    expect(source).toContain('role="tablist"');
    expect(source).toContain('role="tab"');
    expect(source).toContain('role="tabpanel"');
    expect(source).toContain("aria-selected={selected}");
    expect(source).toContain("aria-controls=");
  });

  /** The open tab is React state and nothing else: a query parameter would put state into the
   *  traffic counter's path allowlist, storage would add a key the cookie policy has to list, and
   *  a traffic event would need the justification DECISIONS asks for. */
  it("keeps its state out of the URL, the browser's storage and the traffic counter", () => {
    expect(source).not.toContain("useSearchParams");
    expect(source).not.toContain("URLSearchParams");
    expect(source).not.toContain("localStorage");
    expect(source).not.toContain("sessionStorage");
    expect(source).not.toContain("indexedDB");
    expect(source).not.toContain("trackSiteTraffic");
  });

  /** 2026-09-22 (report item 0.4). The strip's heading was sr-only and three of its four cards
   *  wore the same "no account" pill, so the page's most-repeated sentence was repeated three
   *  more times inside one strip. The heading is visible and says it once; a pill is left only
   *  where it names something the card does not. */
  it("says the no-account promise once, in a visible heading", () => {
    expect(source).not.toContain('<h2 className="sr-only">');
    expect(source).toContain('<h2 className="text-sm font-medium text-accent-ink">{t("title")}</h2>');
    expect(source).not.toContain('t("noAccount")');
    expect(source.match(/pill: t\(/g) ?? []).toHaveLength(1);
  });

  it("carries the sample-data badge on every panel's mock", () => {
    for (const mock of ["ExtensionPopupMock.tsx", "CvScanResultMock.tsx", "BenchmarkResultMock.tsx", "CompanyReviewsMock.tsx"]) {
      expect(readLanding(mock), mock).toContain("<SampleDataBadge");
    }
  });
});

describe("outbound Chrome Web Store links", () => {
  // The feature grid held the second one until 2026-09-22; the tools strip is the page's only
  // way to the store now, and the footer's own link lives in components/layout.
  for (const file of ["ToolsStrip.tsx"]) {
    const source = readLanding(file);

    it(`${file} uses the shared constant, opens a new tab and sends no referrer`, () => {
      expect(source).toContain("CHROME_WEB_STORE_URL");
      expect(source).not.toContain("chromewebstore.google.com");
      expect(source).toContain('target="_blank"');
      expect(source).toContain('rel="noopener noreferrer"');
    });
  }
});

describe("the extension mock", () => {
  const source = stripComments(readLanding("ExtensionPopupMock.tsx"));

  it("is one picture for assistive technology, with a sentence in its place", () => {
    expect(source).toContain('role="img"');
    expect(source).toContain('aria-label={t("ariaLabel")}');
    expect(source).toContain('aria-hidden="true"');
  });

  /** A hidden region must not contain anything focusable, so the fields and the button are divs. */
  it("has no focusable controls inside the picture", () => {
    expect(source).not.toMatch(/<input\b/);
    expect(source).not.toMatch(/<button\b/);
    expect(source).not.toMatch(/<a\b/);
  });

  it("shows the same job as the Web Store screenshots", () => {
    const scene = read("../extension/store-listing/screenshots/scene-job.html");
    for (const value of ["Acme Yazılım", "Senior Backend Engineer", "İstanbul, Türkiye", "Elif Demir"]) {
      expect(source, value).toContain(value);
      expect(scene, `${value} in scene-job.html`).toContain(value);
    }
  });
});

describe("the companies mock", () => {
  const source = stripComments(readLanding("CompanyReviewsMock.tsx"));

  it("is one picture for assistive technology and holds nothing focusable", () => {
    expect(source).toContain('role="img"');
    expect(source).toContain('aria-hidden="true"');
    expect(source).not.toMatch(/<a\b/);
    expect(source).not.toMatch(/<button\b/);
    expect(source).not.toMatch(/<input\b/);
  });

  /** The aggregate is the company page's own component, so the demo cannot drift from the product;
   *  its scoring link is switched off because a hidden picture must not contain a link. */
  it("renders the real summary panel, without its link", () => {
    expect(source).toContain("<ReviewSummaryPanel");
    expect(source).toContain("showScoringLink={false}");
  });

  it("shows the same company as the extension mock", () => {
    expect(source).toContain("Acme Yazılım");
  });
});

describe("the merged problem/why band (2026-09-22, report item 0.4)", () => {
  const source = stripComments(readLanding("AfterApplySection.tsx"));

  /** Two sections made one argument — the history is scattered because nothing follows an
   *  application — in two bands, two eyebrows and two headings. One band now, and the anchor the
   *  header, footer, hero and final CTA all point at stays on it. */
  it("carries both halves and keeps the how-it-works anchor", () => {
    expect(source).toContain('id="how-it-works"');
    expect(source).toContain('getTranslations("landing.problem")');
    expect(source).toContain('getTranslations("landing.afterApply")');
    expect(source).toContain('tProblem("outcome")');
    expect(source).toContain('t("silentQuestion")');
  });

  it("keeps one <h2> in the band, with the turn under it as an <h3>", () => {
    expect(source.match(/<h2 /g) ?? []).toHaveLength(1);
    expect(source).toMatch(/<h3[^>]*>\{t\("title"\)\}<\/h3>/);
  });
});

describe("the closing band (2026-09-22)", () => {
  const source = stripComments(readLanding("FinalCtaSection.tsx"));

  /** The privacy promises used to be a section of their own right before this one: two closing
   *  bands in a row. Same three points and the same policy link, now above the invitation. */
  it("carries the three privacy promises and the policy link with the call to action", () => {
    for (const key of ["privateTitle", "anonymousTitle", "deleteTitle", "link"]) {
      expect(source, key).toContain(`tPrivacy("${key}")`);
    }
    expect(source).toContain('href="/privacy"');
    expect(source).toContain("<CtaButtons");
  });
});

describe("the landing copy says a thing once (2026-09-22, report item 0.4)", () => {
  const catalogues = [
    { name: "tr", landing: (JSON.parse(read("messages/tr.json")) as Record<string, unknown>).landing, phrase: /hesap gerek/gi },
    { name: "en", landing: (JSON.parse(read("messages/en.json")) as Record<string, unknown>).landing, phrase: /no account/gi },
  ];

  const strings = (value: unknown): string[] => {
    if (typeof value === "string") return [value];
    if (value && typeof value === "object") return Object.values(value).flatMap(strings);
    return [];
  };

  /** "No account needed" appeared nine times on one page. Twice is the budget: the tools strip's
   *  own heading, which is the offer, and the hero drop zone, where it is part of what happens to
   *  the file. A third is a copy pass re-introducing the thing this change removed. */
  for (const { name, landing, phrase } of catalogues) {
    it(`says "no account" at most twice in ${name}`, () => {
      const hits = strings(landing).join(" ").match(phrase) ?? [];
      expect(hits.length, hits.join(" | ")).toBeLessThanOrEqual(2);
    });
  }

  it("keeps no copy for the sections that left", () => {
    for (const name of ["tr", "en"]) {
      const landing = (JSON.parse(read(`messages/${name}.json`)) as { landing: Record<string, unknown> }).landing;
      expect(landing, name).not.toHaveProperty("features");
      expect(landing.analytics, name).not.toHaveProperty("benchmarkCard");
    }
  });
});

describe("the #extension anchor", () => {
  it("lands on the tools strip, which the navbar and footer point at", () => {
    const strip = stripComments(readLanding("ToolsStrip.tsx"));
    expect(strip).toContain('id="extension"');
    expect(strip).toContain("scroll-mt-20");
    expect(strip).toContain('window.location.hash === "#extension"');
    // The footer lives in components/layout and points at the strip from every public page, so
    // the anchor carries a leading slash. The header stopped listing it on 2026-09-17: one link
    // set for every signed-out page, and "how it works" is the only anchor in it.
    expect(read("src/components/layout/SiteFooter.tsx")).toContain('"/#extension"');
    expect(read("src/components/layout/SiteHeader.tsx")).not.toContain('"/#extension"');
    // #features went with the grid it pointed at (2026-09-22): a footer link to an anchor no
    // section carries scrolls nowhere.
    expect(read("src/components/layout/SiteFooter.tsx")).not.toContain('"/#features"');
  });
});

describe("every landing component", () => {
  const files = readdirSync(LANDING_DIR).filter((file) => file.endsWith(".tsx"));

  /** Visuals are components with demo data, never image files: a screenshot cannot follow the
   *  product, and the page had none before this change either. (The brand mark is an <img>, but it
   *  lives in components/layout — outside this directory on purpose.) */
  it("ships no screenshot", () => {
    for (const file of files) {
      const source = stripComments(readLanding(file));
      expect(source, file).not.toContain("next/image");
      expect(source, file).not.toMatch(/<img\b/);
      expect(source, file).not.toMatch(/\.(png|jpe?g|webp|gif)"/);
      expect(source, file).not.toContain("/help/screenshots");
    }
  });
});

describe("the hero", () => {
  const source = stripComments(readLanding("HeroSection.tsx"));

  it("keeps the glow decorative and the drop zone in place", () => {
    expect(source).toContain("aa-hero-glow");
    expect(source).toContain("pointer-events-none");
    expect(source).toContain('aria-hidden="true"');
    expect(source).toContain("<HeroCvDropzone />");
  });

  /** 2026-09-22 (report item 0.4): the page addressed everyone and so named nobody. The line is
   *  on both the full hero and the band, because the band is the first screen whenever the weekly
   *  postings lead. It narrows the tone, not the product — so it says who the numbers are about,
   *  and no form anywhere lost an option to it. */
  it("says who the page is for, on the hero and on the band", () => {
    expect(source.match(/t\("audience"\)/g) ?? []).toHaveLength(2);

    const tr = JSON.parse(read("messages/tr.json")) as { landing: { hero: { audience: string } } };
    const en = JSON.parse(read("messages/en.json")) as { landing: { hero: { audience: string } } };
    expect(tr.landing.hero.audience).toMatch(/yazılım/i);
    expect(en.landing.hero.audience).toMatch(/software/i);
  });
});

describe("the weekly postings hero (2026-09-16, direction B)", () => {
  const page = stripComments(read("src/app/[locale]/page.tsx"));
  const hero = stripComments(readLanding("WeeklyJobsHero.tsx"));
  const original = stripComments(readLanding("HeroSection.tsx"));

  /** The flag is read on the server so the first screen is right in the HTML: a hero that swaps
   *  after hydration jumps, and this is the one screen where that costs the most. */
  it("is chosen from the server-side flag, with the original hero as the band under it", () => {
    expect(page).toContain("fetchJobSourcesEnabled()");
    expect(page).not.toContain("useClientConfig");
    const branch = page.slice(page.indexOf("weeklyJobsOnSale ? ("), page.indexOf("<ToolsStrip />"));
    const order = ["<WeeklyJobsHero />", "<HeroSection band />", ") : (", "<HeroSection />"].map((token) => {
      const index = branch.indexOf(token);
      expect(index, token).toBeGreaterThan(-1);
      return index;
    });
    expect(order).toEqual([...order].sort((a, b) => a - b));
  });

  it("keeps one <h1> per page: the band variant demotes the original hero to an <h2>", () => {
    expect(hero).toContain("<h1 ");
    // The band branch comes first in the file; it must carry an h2 and no glow, and the hero
    // branch after it keeps the h1 and the glow.
    const bandMarkup = original.slice(original.indexOf("if (band)"), original.lastIndexOf("return ("));
    expect(bandMarkup.length).toBeGreaterThan(0);
    expect(bandMarkup).toContain("<h2 ");
    expect(bandMarkup).not.toContain("<h1 ");
    expect(bandMarkup).not.toContain("aa-hero-glow");
    expect(bandMarkup).toContain("<HeroCvDropzone />");
    expect(bandMarkup).toContain("<HeroCtaButtons />");
  });

  it("sends the visitor to registration and to the help topic, and shows no price", () => {
    const ctas = stripComments(readLanding("WeeklyJobsHeroCtas.tsx"));
    expect(ctas).toContain('href="/register"');
    expect(ctas).toContain('trackSiteTraffic("cta_get_started")');
    expect(ctas).toContain('href="/help/weekly-jobs"');
    expect(ctas).toContain('href="/weekly-jobs"');
    expect(hero).not.toMatch(/formatMinor|paymentsApi|₺/);
  });

  it("shares the sample list with the dashboard announcement", () => {
    expect(hero).toContain("<WeeklyJobsSampleList");
    expect(stripComments(read("src/components/dashboard/WeeklyJobsAnnouncement.tsx"))).toContain("<WeeklyJobsSampleList");
  });

  it("says in both languages that the tracker itself stays free", () => {
    const tr = JSON.parse(read("messages/tr.json")) as { landing: { weeklyHero: { subtitle: string } } };
    const en = JSON.parse(read("messages/en.json")) as { landing: { weeklyHero: { subtitle: string } } };
    expect(tr.landing.weeklyHero.subtitle).toContain("ücretsiz");
    expect(en.landing.weeklyHero.subtitle.toLowerCase()).toContain("free");
  });
});
