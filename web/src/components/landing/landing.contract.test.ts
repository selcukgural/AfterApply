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

  /** hero → tools → problem → why → numbers → import → features: the promise first, then how the
   *  list fills itself, then the feature list for whoever still wants it. */
  it("keeps the tools strip directly under the hero and the 2026-09-08 order after it", () => {
    const order = [
      "HeroSection",
      "ToolsStrip",
      "ProblemSection",
      "AfterApplySection",
      "AnalyticsSection",
      "LinkedInImportSection",
      "FeaturesSection",
    ].map(indexOf);

    expect(order).toEqual([...order].sort((a, b) => a - b));
  });

  // 2026-09-18 (growth audit finding 11): the vision, mission and roadmap sections moved to the
  // about page, and the site's running totals sit right under the tools strip, as one line.
  it("carries the stats strip under the tools strip and no vision/mission/roadmap section", () => {
    expect(indexOf("SiteStatsStrip")).toBeGreaterThan(indexOf("ToolsStrip"));
    expect(indexOf("SiteStatsStrip")).toBeLessThan(indexOf("ProblemSection"));
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
    expect(source).toContain('["extension", "companies", "benchmark", "cv"]');
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

  it("carries the sample-data badge on every panel's mock", () => {
    for (const mock of ["ExtensionPopupMock.tsx", "CvScanResultMock.tsx", "BenchmarkResultMock.tsx", "CompanyReviewsMock.tsx"]) {
      expect(readLanding(mock), mock).toContain("<SampleDataBadge");
    }
  });
});

describe("outbound Chrome Web Store links", () => {
  for (const file of ["ToolsStrip.tsx", "FeaturesSection.tsx"]) {
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

describe("the features grid", () => {
  it("lists the company pages and links to them", () => {
    const source = stripComments(readLanding("FeaturesSection.tsx"));
    expect(source).toContain('"/companies"');
    expect(source).toContain("sm:col-span-2");
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
