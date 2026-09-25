import { beforeEach, describe, expect, it } from "vitest";
import { scrapeJobPosting } from "../scraper.js";

// The generic reader is what makes 0.9.0 cover sites nobody wrote an adapter for, so it is the
// part worth pinning: schema.org markup in the shapes real boards actually publish it in.
function setBody(html) {
  document.body.innerHTML = html;
}

// Built through the DOM rather than innerHTML on purpose: a payload containing "</script>" or
// an HTML entity would otherwise be re-parsed by the test harness itself (the tag closes early,
// entities decode) and the test would be measuring innerHTML, not the scraper. A real page
// escapes those inside its own JSON-LD block.
function jsonLdScript(text) {
  const script = document.createElement("script");
  script.type = "application/ld+json";
  script.textContent = text;
  document.body.appendChild(script);
  return script;
}

function jsonLd(payload) {
  setBody("");
  jsonLdScript(JSON.stringify(payload));
}

const POSTING = {
  "@context": "https://schema.org",
  "@type": "JobPosting",
  title: "Backend Engineer",
  datePosted: "2026-09-01",
  hiringOrganization: { "@type": "Organization", name: "Acme" },
  jobLocation: { "@type": "Place", address: { "@type": "PostalAddress", addressLocality: "Istanbul", addressCountry: "TR" } },
  description: "<p>Build <strong>things</strong>.</p><ul><li>Go</li></ul>",
};

beforeEach(() => setBody(""));

describe("schema.org JobPosting", () => {
  it("reads the fields a posting page publishes", async () => {
    jsonLd(POSTING);

    await expect(scrapeJobPosting({ strategy: "jsonld" })).resolves.toMatchObject({
      title: "Backend Engineer",
      company: "Acme",
      location: "Istanbul, TR",
      publishedAt: "2026-09-01",
      foundBy: "jsonld",
    });
  });

  it("finds the posting inside an array", async () => {
    jsonLd([{ "@type": "WebSite", name: "Careers" }, POSTING]);
    expect((await scrapeJobPosting({ strategy: "jsonld" })).title).toBe("Backend Engineer");
  });

  it("finds the posting inside @graph", async () => {
    jsonLd({ "@context": "https://schema.org", "@graph": [{ "@type": "Organization", name: "Acme" }, POSTING] });
    expect((await scrapeJobPosting({ strategy: "jsonld" })).company).toBe("Acme");
  });

  it("accepts @type as an array, which some boards emit", async () => {
    jsonLd({ ...POSTING, "@type": ["JobPosting", "Thing"] });
    expect((await scrapeJobPosting({ strategy: "jsonld" })).foundBy).toBe("jsonld");
  });

  it("accepts a plain-string hiringOrganization", async () => {
    jsonLd({ ...POSTING, hiringOrganization: "Acme" });
    expect((await scrapeJobPosting({ strategy: "jsonld" })).company).toBe("Acme");
  });

  it("drops the region when a locality is present, so 'Istanbul, Istanbul, TR' cannot happen", async () => {
    jsonLd({ ...POSTING, jobLocation: [{ address: { addressLocality: "Istanbul", addressRegion: "Istanbul", addressCountry: "TR" } }] });
    expect((await scrapeJobPosting({ strategy: "jsonld" })).location).toBe("Istanbul, TR");
  });

  it("survives a malformed block sitting next to a good one", async () => {
    // Several sites ship a broken analytics blob beside a perfectly good JobPosting; one must not
    // cost the other.
    setBody("");
    jsonLdScript("{ not json ");
    jsonLdScript(JSON.stringify(POSTING));
    expect((await scrapeJobPosting({ strategy: "jsonld" })).title).toBe("Backend Engineer");
  });

  it("leaves missing fields empty rather than guessing", async () => {
    jsonLd({ "@type": "JobPosting", title: "Backend Engineer" });

    const result = await scrapeJobPosting({ strategy: "jsonld" });
    expect(result).toMatchObject({ title: "Backend Engineer", company: "", location: "", description: null, publishedAt: null });
  });

  it("ignores a datePosted that is not a string, instead of sending an object to the backend", async () => {
    jsonLd({ ...POSTING, datePosted: { "@value": "2026-09-01" } });
    expect((await scrapeJobPosting({ strategy: "jsonld" })).publishedAt).toBeNull();
  });
});

describe("the description sanitizer", () => {
  it("keeps formatting and the plain text, and drops everything else", async () => {
    jsonLd({
      ...POSTING,
      description: '<p>Build <strong>things</strong>.</p><script>alert(1)</script><img src="x"><ul><li>Go</li></ul>',
    });

    const result = await scrapeJobPosting({ strategy: "jsonld" });
    expect(result.descriptionHtml).toBe("<p>Build <strong>things</strong>.</p><ul><li>Go</li></ul>");
    expect(result.descriptionHtml).not.toContain("script");
    expect(result.descriptionHtml).not.toContain("img");
    expect(result.description).toContain("Build things.");
  });

  it("escapes text that looks like markup, so the stored html cannot grow tags of its own", async () => {
    jsonLd({ ...POSTING, description: "<p>We use C++ &amp; &lt;framework&gt;</p>" });
    expect((await scrapeJobPosting({ strategy: "jsonld" })).descriptionHtml).toBe("<p>We use C++ &amp; &lt;framework&gt;</p>");
  });

  // The shapes a regex filter gets wrong, and the reason there is no regex here any more: an end
  // tag may carry junk (`</script foo="bar">` is valid HTML), and one pass over a nested opener
  // leaves a live one behind. The sanitizer works on parsed elements by tag name, so none of this
  // reaches the output as markup — whatever survives does so as escaped text.
  it.each([
    '<p>ok</p><script foo="bar">alert(1)</script bar>',
    "<p>ok</p><scr<script>ipt>alert(1)</scr</script>ipt>",
    "<p>ok</p><STYLE>body{display:none}</STYLE>",
    "<p>ok</p><script>alert(1)",
  ])("never emits script or style markup for %s", async (description) => {
    jsonLd({ ...POSTING, description });

    const result = await scrapeJobPosting({ strategy: "jsonld" });
    expect(result.descriptionHtml).toContain("<p>ok</p>");
    expect(result.descriptionHtml.toLowerCase()).not.toContain("<script");
    expect(result.descriptionHtml.toLowerCase()).not.toContain("<style");
  });

  it("clamps to what the backend validator accepts", async () => {
    jsonLd({ ...POSTING, description: `<p>${"x".repeat(30_000)}</p>` });

    const result = await scrapeJobPosting({ strategy: "jsonld" });
    expect(result.description.length).toBe(10_000);
    expect(result.descriptionHtml.length).toBe(20_000);
  });
});

describe("pages with no structured data", () => {
  it("falls back to Open Graph and says the fields were guessed", async () => {
    setBody("");
    document.head.innerHTML = `
      <meta property="og:title" content="Backend Engineer at Acme">
      <meta property="og:site_name" content="Acme Careers">
      <meta property="og:description" content="We are hiring.">`;

    await expect(scrapeJobPosting({ strategy: "jsonld" })).resolves.toMatchObject({
      title: "Backend Engineer at Acme",
      company: "Acme Careers",
      description: "We are hiring.",
      foundBy: "meta",
    });
    document.head.innerHTML = "";
  });

  it("reports nothing found when there is nothing to find, so the popup can offer the blank form", async () => {
    document.title = "";
    setBody("<p>Not a job page.</p>");
    expect((await scrapeJobPosting({ strategy: "jsonld" })).foundBy).toBeNull();
  });
});

// The two original sites keep their own readers; 0.9.0 moved them into this file unchanged, and
// these are the tripwires for that move.
describe("the site-specific readers", () => {
  it("kariyer.net reads title, company, location and the company profile link", async () => {
    setBody(`
      <h1>
        <div class="vue-clamp job-title">Backend Developer</div>
        <a data-test="company-name" href="https://www.kariyer.net/firma-profil/acme-1166-1810"><div class="vue-clamp">Acme</div></a>
      </h1>
      <div class="company-location">Istanbul</div>
      <div class="job-detail-container-description"><p>Join <b>us</b>.</p></div>`);

    await expect(scrapeJobPosting({ strategy: "kariyer" })).resolves.toMatchObject({
      title: "Backend Developer",
      company: "Acme",
      location: "Istanbul",
      companyKariyerNetUrl: "https://www.kariyer.net/firma-profil/acme-1166-1810",
      descriptionHtml: "<p>Join <b>us</b>.</p>",
      foundBy: "kariyer",
    });
  });

  it("linkedin scopes the title to the job id it was given", async () => {
    setBody(`
      <a href="/jobs/view/9999">Some other job</a>
      <p><a href="/jobs/view/4242">Backend Engineer</a></p>
      <a href="/company/acme/">Acme</a>
      <div data-testid="expandable-text-box"><p>Join us.</p></div>`);

    await expect(scrapeJobPosting({ strategy: "linkedin", jobId: "4242" })).resolves.toMatchObject({
      title: "Backend Engineer",
      company: "Acme",
      foundBy: "linkedin",
    });
  });

  it("linkedin takes no hr contact from an unrelated profile link", async () => {
    // A job page also lists alumni and network suggestions; recording one of them as the HR
    // contact would write a stranger's personal data onto the record.
    setBody(`
      <p><a href="/jobs/view/4242">Backend Engineer</a></p>
      <a href="/company/acme/">Acme</a>
      <div><a href="/in/some-alum">Some Alum</a></div>`);

    const result = await scrapeJobPosting({ strategy: "linkedin", jobId: "4242" });
    expect(result.hrName).toBeNull();
    expect(result.hrLinkedInUrl).toBeNull();
  });

  // Trimmed from the live split view (2026-09): the umbrella block is keyed by componentkey, with
  // the hiring team as its first sub-block, then the alumni list.
  function peopleBlock(marker, hiringTeam) {
    return `
      <p><a href="/jobs/view/4242">Backend Engineer</a></p>
      <a href="/company/acme/">Acme</a>
      <div ${marker}>
        <p>People you can reach out to</p>
        ${hiringTeam}
        <div><p>Alumni who work here</p><a href="/in/some-alum">Some Alum</a></div>
      </div>`;
  }

  const HIRING_TEAM = `
    <div>
      <p>Meet the hiring team</p>
      <a href="https://www.linkedin.com/in/jane-recruiter/">
        <p>Jane Recruiter</p><p>Recruitment Consultant at Acme</p><p>Job poster</p>
      </a>
    </div>`;

  it.each([
    ["componentkey", 'componentkey="JobDetailsPeopleWhoCanHelpSlot_4242" id="JobDetailsPeopleWhoCanHelpSlot_4242"'],
    ["data-sdui-component", 'data-sdui-component="com.linkedin.sdui.peopleWhoCanHelp"'],
  ])("linkedin reads the job poster from the hiring team block marked by %s", async (_, marker) => {
    setBody(peopleBlock(marker, HIRING_TEAM));

    const result = await scrapeJobPosting({ strategy: "linkedin", jobId: "4242" });
    expect(result.hrLinkedInUrl).toBe("https://www.linkedin.com/in/jane-recruiter/");
    expect(result.hrName).toBe("Jane Recruiter");
  });

  it("linkedin takes no hr contact from the alumni list when there is no hiring team", async () => {
    setBody(peopleBlock('componentkey="JobDetailsPeopleWhoCanHelpSlot_4242"', ""));

    const result = await scrapeJobPosting({ strategy: "linkedin", jobId: "4242" });
    expect(result.hrName).toBeNull();
    expect(result.hrLinkedInUrl).toBeNull();
  });
});
