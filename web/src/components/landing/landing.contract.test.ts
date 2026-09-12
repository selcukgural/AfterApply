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
});

describe("the tools strip", () => {
  const source = stripComments(readLanding("ToolsStrip.tsx"));

  it("opens on the extension, and lists it first", () => {
    expect(source).toContain('useState<Tool>("extension")');
    expect(source).toContain('["extension", "benchmark", "cv"]');
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
    for (const mock of ["ExtensionPopupMock.tsx", "CvScanResultMock.tsx", "BenchmarkResultMock.tsx"]) {
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

describe("the #extension anchor", () => {
  it("lands on the tools strip, which the navbar and footer point at", () => {
    const strip = stripComments(readLanding("ToolsStrip.tsx"));
    expect(strip).toContain('id="extension"');
    expect(strip).toContain("scroll-mt-20");
    expect(strip).toContain('window.location.hash === "#extension"');
    expect(readLanding("LandingNavbar.tsx")).toContain('"#extension"');
    expect(readLanding("LandingFooter.tsx")).toContain('"#extension"');
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
