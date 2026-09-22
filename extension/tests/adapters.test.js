import { describe, expect, it } from "vitest";
import {
  ADAPTERS,
  detectJob,
  extractAshbyId,
  extractGreenhouseId,
  extractKariyerNetJobId,
  extractLeverId,
  extractLinkedInJobId,
  extractSmartRecruitersId,
  extractWorkableId,
  extractWorkdayId,
  isHost,
} from "../adapters.js";

// Every id case here has a twin in tests/AfterApply.UnitTests/Imports/AtsJobIdExtractorTests.cs.
// The two sides derive Job.ExternalId independently from the same URL; if they disagree, one
// posting becomes two Job rows.
const LEVER_UUID = "8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f";

describe("isHost", () => {
  it.each([
    ["greenhouse.io", "greenhouse.io", true],
    ["job-boards.greenhouse.io", "greenhouse.io", true],
    ["JOB-BOARDS.GREENHOUSE.IO", "greenhouse.io", true],
    // The two shapes a naive includes/endsWith check lets through.
    ["notgreenhouse.io", "greenhouse.io", false],
    ["greenhouse.io.evil.example", "greenhouse.io", false],
  ])("%s vs %s", (hostname, domain, expected) => {
    expect(isHost(hostname, domain)).toBe(expected);
  });
});

describe("LinkedIn and kariyer.net ids", () => {
  it("reads the id from a dedicated job page", () => {
    expect(extractLinkedInJobId("https://www.linkedin.com/jobs/view/4449445627/")).toBe("4449445627");
  });

  it("reads the id from the search-results side panel", () => {
    expect(extractLinkedInJobId("https://www.linkedin.com/jobs/search-results/?currentJobId=4449445627")).toBe("4449445627");
  });

  it("ignores a lookalike host", () => {
    expect(extractLinkedInJobId("https://www.notlinkedin.com/jobs/view/4449445627/")).toBeNull();
  });

  it("reads the trailing ilan id", () => {
    expect(extractKariyerNetJobId("https://www.kariyer.net/is-ilani/acme-backend-engineer-4539310")).toBe("4539310");
  });
});

describe("ATS ids", () => {
  it.each([
    ["https://job-boards.greenhouse.io/stripe/jobs/4512345", "stripe/4512345"],
    ["https://boards.greenhouse.io/stripe/jobs/4512345", "stripe/4512345"],
    ["https://job-boards.greenhouse.io/stripe/jobs/4512345#app", "stripe/4512345"],
    ["https://job-boards.greenhouse.io/stripe/jobs/4512345?gh_src=abc", "stripe/4512345"],
    ["https://job-boards.greenhouse.io/stripe", null],
  ])("greenhouse %s", (url, expected) => expect(extractGreenhouseId(url)).toBe(expected));

  it.each([
    [`https://jobs.lever.co/acme/${LEVER_UUID}`, `acme/${LEVER_UUID}`],
    [`https://jobs.lever.co/acme/${LEVER_UUID}/apply`, `acme/${LEVER_UUID}`],
    ["https://jobs.lever.co/acme", null],
  ])("lever %s", (url, expected) => expect(extractLeverId(url)).toBe(expected));

  it.each([
    [`https://jobs.ashbyhq.com/acme/${LEVER_UUID}`, `acme/${LEVER_UUID}`],
    [`https://jobs.ashbyhq.com/acme/${LEVER_UUID}/application`, `acme/${LEVER_UUID}`],
  ])("ashby %s", (url, expected) => expect(extractAshbyId(url)).toBe(expected));

  it.each([
    ["https://nvidia.wd5.myworkdayjobs.com/en-US/Site/job/US-CA-Santa-Clara/Senior-Engineer_JR1234567", "nvidia/JR1234567"],
    ["https://nvidia.wd5.myworkdayjobs.com/Site/job/Remote/Staff-Engineer_R-98765", "nvidia/R-98765"],
    ["https://nvidia.wd5.myworkdayjobs.com/en-US/Site/job/Remote/Staff-Engineer_R-98765/apply", "nvidia/R-98765"],
    ["https://wd3.myworkdaysite.com/en-US/recruiting/acme/Site/job/Istanbul/Backend-Engineer_R-4242", "acme/R-4242"],
    ["https://nvidia.wd5.myworkdayjobs.com/en-US/Site", null],
  ])("workday %s", (url, expected) => expect(extractWorkdayId(url)).toBe(expected));

  it.each([
    ["https://apply.workable.com/acme/j/A1B2C3D4E5/", "acme/A1B2C3D4E5"],
    ["https://apply.workable.com/acme/j/A1B2C3D4E5", "acme/A1B2C3D4E5"],
    ["https://apply.workable.com/acme/", null],
  ])("workable %s", (url, expected) => expect(extractWorkableId(url)).toBe(expected));

  it.each([
    ["https://jobs.smartrecruiters.com/Acme/743999123456789-backend-engineer", "Acme/743999123456789"],
    ["https://careers.smartrecruiters.com/Acme/743999123456789", "Acme/743999123456789"],
    ["https://jobs.smartrecruiters.com/Acme", null],
  ])("smartrecruiters %s", (url, expected) => expect(extractSmartRecruitersId(url)).toBe(expected));

  it.each([extractGreenhouseId, extractLeverId, extractAshbyId, extractWorkdayId, extractWorkableId, extractSmartRecruitersId])(
    "an unparsable URL yields no id",
    (extract) => {
      expect(extract("not a url")).toBeNull();
      expect(extract("")).toBeNull();
    },
  );
});

describe("detectJob", () => {
  it("canonicalises a LinkedIn side-panel URL to the posting's own URL", () => {
    const job = detectJob("https://www.linkedin.com/jobs/search-results/?currentJobId=4449445627&trk=x");

    expect(job).toMatchObject({ site: "linkedin", jobId: "4449445627", strategy: "linkedin" });
    expect(job.jobUrl).toBe("https://www.linkedin.com/jobs/view/4449445627/");
  });

  it("rebuilds an ATS URL so the apply page and the posting page dedupe to one application", () => {
    const fromPosting = detectJob(`https://jobs.lever.co/acme/${LEVER_UUID}`);
    const fromApply = detectJob(`https://jobs.lever.co/acme/${LEVER_UUID}/apply?lever-source=x`);

    expect(fromApply.jobUrl).toBe(fromPosting.jobUrl);
    expect(fromApply.jobUrl).toBe(`https://jobs.lever.co/acme/${LEVER_UUID}`);
  });

  it("collapses the two Greenhouse hosts onto one canonical URL", () => {
    expect(detectJob("https://boards.greenhouse.io/stripe/jobs/4512345").jobUrl)
      .toBe("https://job-boards.greenhouse.io/stripe/jobs/4512345");
  });

  it("derives the company's board root from the account segment", () => {
    expect(detectJob("https://job-boards.greenhouse.io/stripe/jobs/4512345").companyAtsUrl)
      .toBe("https://job-boards.greenhouse.io/stripe");
  });

  it("sends no board root for Workday, whose first path segment is not the customer", () => {
    const job = detectJob("https://nvidia.wd5.myworkdayjobs.com/en-US/Site/job/Remote/Staff-Engineer_R-98765");

    expect(job.site).toBe("workday");
    expect(job.companyAtsUrl).toBeNull();
  });

  it("falls back to the generic reader on a site with no adapter", () => {
    const job = detectJob("https://www.seek.com.au/job/12345678?type=standard");

    expect(job).toMatchObject({ site: "generic", strategy: "jsonld", jobId: null });
    expect(job.label).toBe("seek.com.au");
  });

  it("keeps a generic page's query, where the posting id often lives", () => {
    expect(detectJob("https://jobs.example.com/careers?jobId=42&utm=x&ref=y").jobUrl)
      .toContain("jobId=42");
  });

  it("strips tracking parameters from a generic page so one posting is one application", () => {
    const job = detectJob("https://jobs.example.com/careers/42?gclid=abc&source=email#top");

    expect(job.jobUrl).toBe("https://jobs.example.com/careers/42");
  });

  it("treats an adapter's own landing page as having no posting", () => {
    const job = detectJob("https://job-boards.greenhouse.io/stripe");

    expect(job.site).toBe("greenhouse");
    expect(job.jobId).toBeNull();
    expect(job.companyAtsUrl).toBeNull();
  });

  it.each([
    ["http://jobs.example.com/careers/42"],
    ["chrome://extensions"],
    ["not a url"],
  ])("refuses %s, which the popup can never read", (url) => {
    expect(detectJob(url)).toBeNull();
  });
});

describe("the adapter table", () => {
  it("gives every adapter the pieces detectJob depends on", () => {
    for (const adapter of ADAPTERS) {
      expect(adapter.domains.length).toBeGreaterThan(0);
      expect(typeof adapter.externalId).toBe("function");
      expect(typeof adapter.canonicalUrl).toBe("function");
      expect(["linkedin", "kariyer", "jsonld"]).toContain(adapter.strategy);
    }
  });

  it("keeps ids unique, since the popup and the badge both key off them", () => {
    const ids = ADAPTERS.map((adapter) => adapter.id);

    expect(new Set(ids).size).toBe(ids.length);
  });
});
