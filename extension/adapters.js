// Which site the current tab is on, how to read a posting from it, and what to call the posting.
//
// Until 0.9.0 this lived inside popup.js as two if-branches and two nearly identical scraper
// functions. It is a table now because the whole point of 0.9.0 is that a site does not need its
// own code: the six ATS adapters below all use the same generic schema.org reader, and a page on
// no adapter at all falls back to that same reader. Adding a site here is a row, not a function.
//
// Every id extractor in this file has a twin in AtsJobIdExtractor.cs / LinkedInJobIdExtractor.cs /
// KariyerNetJobIdExtractor.cs, and the table itself twins JobPostingSourceResolver.KnownSites.
// The two sides derive Job.ExternalId independently from the same URL, so they have to agree —
// change one, change the other and both tests.

/** Host is the domain itself or one of its subdomains — the twin of HostRules.IsHost. Never
 * `includes`/`endsWith` on the bare domain: `notgreenhouse.io` and `greenhouse.io.evil.com` both
 * pass that, and here it would mean submitting one company's posting under another's identity. */
export function isHost(hostname, domain) {
  const host = (hostname || "").toLowerCase();
  const suffix = domain.toLowerCase();
  return host === suffix || host.endsWith(`.${suffix}`);
}

function parse(url) {
  try {
    return new URL(url);
  } catch {
    return null;
  }
}

/** `{account}/{posting}` from the two capture groups, or null. Every ATS is multi-tenant and
 * numbers postings per customer, so the account segment is what keeps two customers' postings
 * apart on the backend's unique (Source, ExternalId) index. */
function composite(url, pattern) {
  const parsed = parse(url);
  const match = parsed && pattern.exec(parsed.pathname);
  return match ? `${match[1]}/${match[2]}` : null;
}

// LinkedIn only puts a job on its own /jobs/view/<id> URL when it is opened in a dedicated tab.
// The far more common path — browsing /jobs/search-results/ and clicking a result — keeps the URL
// on the listing page and opens the job in a side panel via ?currentJobId=<id>. Both shapes
// resolve to the same canonical id, and the canonical /jobs/view/<id>/ URL is what gets submitted
// either way, which is what keeps JobUrl dedup stable across the two entry points.
export function extractLinkedInJobId(url) {
  const parsed = parse(url);
  if (!parsed || parsed.hostname !== "www.linkedin.com") {
    return null;
  }

  const viewMatch = parsed.pathname.match(/\/jobs\/view\/(\d+)/);
  if (viewMatch) {
    return viewMatch[1];
  }

  const currentJobId = parsed.searchParams.get("currentJobId");
  return currentJobId && /^\d+$/.test(currentJobId) ? currentJobId : null;
}

// kariyer.net renders a job only at its own /is-ilani/<slug>-<id> URL (no side panel to account
// for) and appends the numeric ilan id as the slug's trailing -<digits> segment.
export function extractKariyerNetJobId(url) {
  const parsed = parse(url);
  if (!parsed || parsed.hostname !== "www.kariyer.net") {
    return null;
  }

  const match = parsed.pathname.match(/\/is-ilani\/[^/]*-(\d+)\/?$/);
  return match ? match[1] : null;
}

/** job-boards.greenhouse.io/{board}/jobs/{id} (and the older boards.greenhouse.io host). */
export function extractGreenhouseId(url) {
  return composite(url, /^\/([^/]+)\/jobs\/(\d+)(?:\/|$)/);
}

/** jobs.lever.co/{site}/{uuid}, optionally /apply or /thanks — the post-submit page is the one a
 * candidate most often still has open when they reach for the popup. */
export function extractLeverId(url) {
  return composite(url, /^\/([^/]+)\/([0-9a-fA-F]{8}-[0-9a-fA-F-]{27,})(?:\/|$)/);
}

/** jobs.ashbyhq.com/{org}/{uuid}, optionally /application. */
export function extractAshbyId(url) {
  return composite(url, /^\/([^/]+)\/([0-9a-fA-F]{8}-[0-9a-fA-F-]{27,})(?:\/|$)/);
}

/** Two shapes for the same thing: {tenant}.wd{n}.myworkdayjobs.com/[{locale}/]{site}/job/... and
 * wd{n}.myworkdaysite.com/[{locale}/]recruiting/{tenant}/{site}/job/... The requisition is the
 * trailing _R-12345 / _JR1234567 segment — the only part Workday treats as the posting's identity;
 * the slug before it is SEO text that changes whenever the title is edited. */
export function extractWorkdayId(url) {
  const parsed = parse(url);
  if (!parsed) {
    return null;
  }

  const requisition = parsed.pathname.match(/\/job\/[^/]+\/[^/]*_([A-Za-z]{0,3}-?\d[A-Za-z0-9-]*)(?:\/|$)/);
  if (!requisition) {
    return null;
  }

  // myworkdaysite.com carries the tenant in the path, myworkdayjobs.com in the first host label.
  const fromPath = parsed.pathname.match(/\/recruiting\/([^/]+)\//);
  const tenant = fromPath ? fromPath[1] : parsed.hostname.split(".")[0];

  return !tenant || /^wd/i.test(tenant) ? null : `${tenant}/${requisition[1]}`;
}

/** apply.workable.com/{company}/j/{TOKEN}/ — the token is Workable's own uppercase hex shortcode,
 * stable across the apply and confirmation steps. */
export function extractWorkableId(url) {
  return composite(url, /^\/([^/]+)\/j\/([0-9A-Fa-f]{8,})(?:\/|$)/);
}

/** jobs.smartrecruiters.com/{company}/{id}-{slug} (also served from careers.smartrecruiters.com).
 * The id is the long leading number; everything after the first hyphen is title text. */
export function extractSmartRecruitersId(url) {
  return composite(url, /^\/([^/]+)\/(\d{6,})(?:-|\/|$)/);
}

// Query parameters that identify where a click came from rather than which posting it opened.
// Stripped from the canonical URL so the same posting reached from an e-mail, a search result and
// a bookmark dedupes to one application instead of three.
const TRACKING_PARAMS = [
  "gh_src", "gh_jid", "lever-source", "lever-origin", "ref", "referrer", "source", "src",
  "trk", "trackingId", "refId", "originalSubdomain", "fbclid", "gclid", "mc_cid", "mc_eid",
];

/** Origin + path, with tracking parameters and the fragment removed. */
function pathOnlyUrl(parsed) {
  return `${parsed.origin}${parsed.pathname.replace(/\/$/, "")}`;
}

/** Rebuilds the posting's canonical URL from the id the extractor just read, the way the LinkedIn
 * adapter does. This is what makes JobUrl dedupe hold across the places one posting is reachable
 * from: /apply and /thanks on Lever, /application on Ashby, the old boards.greenhouse.io host
 * beside the current one, careers. beside jobs. on SmartRecruiters, and the SEO slug that changes
 * whenever a title is edited. Without it the same job saved from the apply page and from the
 * posting page are two applications. */
function rebuild(template) {
  return (_parsed, externalId) => {
    const [account, posting] = externalId.split("/");
    return template.replace("{account}", account).replace("{posting}", posting);
  };
}

/** The company's own board on the ATS, derived from the first path segment rather than scraped:
 * every one of these hosts puts the customer account there (the id extractors above read the same
 * segment), and a link read off the page could point anywhere. The backend fetches this URL, so
 * the narrower and more predictable it is, the better. */
function boardRootUrl(parsed) {
  const account = parsed.pathname.split("/")[1];
  return account ? `${parsed.origin}/${account}` : null;
}

/** The generic case keeps the query: on a board we have no adapter for, the posting id is as
 * likely to sit in ?jobId= as in the path, and dropping it would collapse every posting on that
 * board into one "already tracked" application. Only the known tracking parameters and the
 * fragment go. */
function cleanedUrl(parsed) {
  const url = new URL(parsed.href);
  url.hash = "";
  for (const param of TRACKING_PARAMS) {
    url.searchParams.delete(param);
  }
  // Keep a trailing "?" from surviving as part of the dedup key.
  return url.searchParams.size === 0 ? `${url.origin}${url.pathname.replace(/\/$/, "")}` : url.href;
}

/**
 * The capture targets, in match order. `label` is what the popup's badge shows; `strategy` picks
 * the reader inside scrapeJobPosting; `externalId` exists so the popup can tell "this really is a
 * posting page" from "this is the board's landing page", which is the difference between a filled
 * form and the manual one.
 *
 * `domains` is what the permission request is built from, and it is also the reason LinkedIn and
 * kariyer.net keep working with no prompt at all: those two are still in manifest host_permissions.
 */
export const ADAPTERS = [
  {
    id: "linkedin",
    label: "LinkedIn",
    domains: ["linkedin.com"],
    strategy: "linkedin",
    externalId: extractLinkedInJobId,
    canonicalUrl: (parsed, jobId) => `https://www.linkedin.com/jobs/view/${jobId}/`,
  },
  {
    id: "kariyer",
    label: "kariyer.net",
    domains: ["kariyer.net"],
    strategy: "kariyer",
    externalId: extractKariyerNetJobId,
    canonicalUrl: pathOnlyUrl,
  },
  {
    id: "greenhouse",
    label: "Greenhouse",
    domains: ["greenhouse.io"],
    strategy: "jsonld",
    externalId: extractGreenhouseId,
    canonicalUrl: rebuild("https://job-boards.greenhouse.io/{account}/jobs/{posting}"),
    companyAtsUrl: boardRootUrl,
  },
  {
    id: "lever",
    label: "Lever",
    domains: ["lever.co"],
    strategy: "jsonld",
    externalId: extractLeverId,
    canonicalUrl: rebuild("https://jobs.lever.co/{account}/{posting}"),
    companyAtsUrl: boardRootUrl,
  },
  {
    id: "ashby",
    label: "Ashby",
    domains: ["ashbyhq.com"],
    strategy: "jsonld",
    externalId: extractAshbyId,
    canonicalUrl: rebuild("https://jobs.ashbyhq.com/{account}/{posting}"),
    companyAtsUrl: boardRootUrl,
  },
  {
    id: "workday",
    label: "Workday",
    domains: ["myworkdayjobs.com", "myworkdaysite.com"],
    strategy: "jsonld",
    externalId: extractWorkdayId,
    // Not rebuilt: a Workday URL carries an optional locale segment and a per-customer site name
    // that are not in the id, so there is nothing to rebuild it from. The path as served is the
    // canonical form, minus tracking parameters.
    canonicalUrl: pathOnlyUrl,
    // No board root: Workday's path starts with an optional locale segment ("/en-US/Site/...")
    // and the tenant lives in the host or behind /recruiting/, so "first segment" is not the
    // customer here. Guessing wrong would hand the enrichment job somebody else's board.
    companyAtsUrl: () => null,
  },
  {
    id: "workable",
    label: "Workable",
    domains: ["workable.com"],
    strategy: "jsonld",
    externalId: extractWorkableId,
    canonicalUrl: rebuild("https://apply.workable.com/{account}/j/{posting}"),
    companyAtsUrl: boardRootUrl,
  },
  {
    id: "smartrecruiters",
    label: "SmartRecruiters",
    domains: ["smartrecruiters.com"],
    strategy: "jsonld",
    externalId: extractSmartRecruitersId,
    canonicalUrl: rebuild("https://jobs.smartrecruiters.com/{account}/{posting}"),
    companyAtsUrl: boardRootUrl,
  },
];

/**
 * What the popup is looking at. Never null since 0.9.0: a page on no adapter still gets a
 * `generic` descriptor, because the popup can now read schema.org markup from any page the user
 * grants it — and offer a blank form when there is nothing to read. That is the difference
 * between "this extension does not work here" and "type two fields and it is tracked".
 *
 * `jobId` null on an adapter page means the adapter matched the site but not a posting URL (a
 * board's landing page, a search result list): the popup treats it exactly like `generic`.
 */
export function detectJob(url) {
  const parsed = parse(url);
  if (!parsed || parsed.protocol !== "https:") {
    return null;
  }

  for (const adapter of ADAPTERS) {
    if (!adapter.domains.some((domain) => isHost(parsed.hostname, domain))) {
      continue;
    }

    const jobId = adapter.externalId(url);
    return {
      site: adapter.id,
      label: adapter.label,
      strategy: adapter.strategy,
      jobId,
      jobUrl: jobId ? adapter.canonicalUrl(parsed, jobId) : cleanedUrl(parsed),
      companyAtsUrl: jobId ? (adapter.companyAtsUrl?.(parsed) ?? null) : null,
      origin: parsed.origin,
    };
  }

  return {
    site: "generic",
    label: parsed.hostname.replace(/^www\./, ""),
    strategy: "jsonld",
    jobId: null,
    jobUrl: cleanedUrl(parsed),
    companyAtsUrl: null,
    origin: parsed.origin,
  };
}

/** The config object handed to the injected scraper. Must stay JSON-serializable: it crosses into
 * the page through executeScript's `args`, which structured-clones it. */
export function scrapeConfigFor(job) {
  return { strategy: job.strategy, jobId: job.jobId };
}
