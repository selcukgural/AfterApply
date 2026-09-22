/**
 * The one function injected into the tab (chrome.scripting.executeScript `func`), for every site.
 *
 * Because it is injected, it must be **fully self-contained**: nothing in here may reference
 * anything outside its own body — no imports, no module-level constants — since only the function
 * source crosses into the page. That constraint is why, until 0.9.0, there were two scrapers with
 * sanitizeDescriptionHtml copy-pasted into both. One function with a `strategy` switch keeps that
 * single copy and means the seventh site costs a table row in adapters.js, not another duplicate.
 *
 * `config` arrives through executeScript's `args`, which structured-clones it — plain data only.
 *
 * Every field returned here is shown as an editable input before anything is submitted. A selector
 * that a redesign broke, or markup a site does not publish, degrades to "the user types it in",
 * never to a silently wrong application.
 */
export async function scrapeJobPosting(config) {
  const EMPTY = {
    title: "",
    company: "",
    location: "",
    description: null,
    descriptionHtml: null,
    publishedAt: null,
    companyLinkedInUrl: null,
    companyKariyerNetUrl: null,
    hrName: null,
    hrLinkedInUrl: null,
    hrEmail: null,
    // "linkedin" | "kariyer" | "jsonld" | "meta" | null — the popup turns this into the badge that
    // tells the user whether the fields were read from structured data or merely guessed from the
    // page title, so an odd-looking prefill is explainable rather than mysterious.
    foundBy: null,
  };

  function textOf(el) {
    return el?.textContent?.trim() || null;
  }

  // Allow-listed HTML snapshot for a formatted display (bold/headers/bullet lists, same as the
  // original listing) — separate from the plain-text description. A capture-time best effort, NOT
  // a security boundary: the backend stores it as-is and the frontend re-sanitizes with DOMPurify
  // before rendering (DECISIONS.md — untrusted content stays untrusted whichever side captured it).
  function sanitizeDescriptionHtml(root) {
    const ALLOWED_TAGS = new Set(["P", "BR", "STRONG", "B", "EM", "I", "UL", "OL", "LI", "H1", "H2", "H3", "H4", "H5", "H6"]);
    const SKIP_TAGS = new Set(["SVG", "BUTTON", "FIGURE", "IMG", "STYLE", "SCRIPT"]);

    function escapeText(text) {
      return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    }

    function walk(node) {
      if (node.nodeType === Node.TEXT_NODE) {
        return escapeText(node.textContent);
      }
      if (node.nodeType !== Node.ELEMENT_NODE) {
        return "";
      }
      const tag = node.tagName;
      if (SKIP_TAGS.has(tag) || node.getAttribute("aria-hidden") === "true") {
        return "";
      }
      const inner = Array.from(node.childNodes).map(walk).join("");
      if (tag === "BR") {
        return "<br>";
      }
      return ALLOWED_TAGS.has(tag) ? `<${tag.toLowerCase()}>${inner}</${tag.toLowerCase()}>` : inner;
    }

    return walk(root).trim();
  }

  function clamp(result) {
    return {
      ...result,
      // Matches CreateFromExtensionRequestValidator's MaximumLength(10_000/20_000).
      description: result.description ? result.description.slice(0, 10_000) : null,
      descriptionHtml: result.descriptionHtml ? result.descriptionHtml.slice(0, 20_000) : null,
    };
  }

  // ---- LinkedIn ------------------------------------------------------------------------------
  // Unchanged from 0.8.0 apart from moving in here: LinkedIn's CSS classes are hashed and carry no
  // meaning, so the hooks are the hrefs LinkedIn needs for its own routing — /jobs/view/<id> for
  // the title, /company/<slug>/ for the company — verified live against both the search-results
  // split view and the plain job card.
  async function scrapeLinkedIn(jobId) {
    const titleLink = document.querySelector(`a[href*="/jobs/view/${jobId}"]`);
    const title = textOf(titleLink);

    const companyLink = document.querySelector('a[href*="/company/"]');
    const company = textOf(companyLink);

    // The job poster ("hirer"), when LinkedIn renders one — it is opt-in, so most postings have
    // none. Never "the first /in/ link on the page": a job page also carries a "people you can
    // reach out to" block of unrelated alumni, and recording one of them as the HR contact would
    // write a stranger's personal data onto the record. Two layouts are live at once, so both are
    // handled; an unrecognised heading yields nothing rather than a guess.
    const HIRING_TEAM_HEADINGS = ["Meet the hiring team", "İşe alım ekibiyle tanışın"];

    // A plain toLowerCase() never matches the Turkish heading: "İ".toLowerCase() is "i" + a
    // combining dot, and "I".toLowerCase() is "i" while the Turkish lowercase is "ı". Folding both
    // sides the same way sidesteps the dotted/dotless mess.
    function normalizeHeading(text) {
      return (text || "").trim().toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/ı/g, "i");
    }

    const normalizedHeadings = HIRING_TEAM_HEADINGS.map(normalizeHeading);

    function findHirerAnchor() {
      const classic = document.querySelector('.hirer-card__hirer-information a[href*="/in/"]');
      if (classic) {
        return classic;
      }

      const peopleBlock = document.querySelector('[data-sdui-component*="peopleWhoCanHelp"]');
      if (!peopleBlock) {
        return null;
      }

      const heading = [...peopleBlock.querySelectorAll("*")].find(
        (el) => el.children.length === 0 && normalizedHeadings.includes(normalizeHeading(el.textContent)),
      );
      if (!heading) {
        return null;
      }

      // Walk up to the sub-block the heading introduces. Reaching the umbrella block means the
      // hiring team has no profile of its own and the links in there belong to the alumni list,
      // so it stops and returns nothing.
      const headingText = normalizeHeading(heading.textContent);
      let block = heading.parentElement;
      while (block && block !== peopleBlock) {
        const startsWithHeading = normalizeHeading(block.innerText).startsWith(headingText);
        const anchor = block.querySelector('a[href*="/in/"]');
        if (startsWithHeading && anchor) {
          return anchor;
        }
        block = block.parentElement;
      }

      return null;
    }

    const hirerLink = findHirerAnchor();
    // innerText, not textContent: the anchor also wraps a "<name> is verified" badge on its own
    // line, and only the first line is the name.
    const hrName = hirerLink?.innerText?.trim().split("\n")[0].trim() || null;

    // The location is the first segment (before "·") of a metadata line rendered as a <p> sibling
    // of the title's wrapper. A fixed hop count does not work — LinkedIn nests this differently
    // for promoted and plain cards — so this walks up and looks for a direct-child <p> that is not
    // the title's own and contains "·".
    let location = null;
    const titleParagraph = titleLink?.closest("p") ?? null;
    let ancestor = titleParagraph?.parentElement ?? null;
    for (let i = 0; i < 6 && ancestor && !location; i++) {
      const metaParagraph = Array.from(ancestor.children).find(
        (el) => el.tagName === "P" && el !== titleParagraph && el.textContent.includes("·"),
      );
      if (metaParagraph) {
        location = textOf(metaParagraph)?.split("·")[0]?.trim() || null;
      }
      ancestor = ancestor.parentElement;
    }

    // LinkedIn renders only the truncated description until "…more" is clicked, at which point
    // React fills in the rest. Click every such button (one for the job, one for the company) and
    // give React a beat before reading.
    document.querySelectorAll('[data-testid="expandable-text-button"]').forEach((button) => button.click());
    await new Promise((resolve) => setTimeout(resolve, 150));

    const descriptionBox = document.querySelector('[data-testid="expandable-text-box"]');

    return {
      ...EMPTY,
      title: title || "",
      company: company || "",
      location: location || "",
      description: textOf(descriptionBox),
      descriptionHtml: descriptionBox ? sanitizeDescriptionHtml(descriptionBox) : null,
      // Raw hrefs; canonicalized back in the popup's own scope, since this function cannot reach
      // the helpers that do it.
      companyLinkedInUrl: companyLink?.href || null,
      hrLinkedInUrl: hirerLink?.href || null,
      hrName,
      foundBy: "linkedin",
    };
  }

  // ---- kariyer.net ---------------------------------------------------------------------------
  // Unchanged from 0.8.0. kariyer.net renders every field up front (no truncated description), and
  // its data-test attributes are its own test hooks — far more stable than the hashed classes.
  function scrapeKariyerNet() {
    const title = textOf(document.querySelector("h1 div.vue-clamp.job-title"));
    const company = textOf(document.querySelector("h1 div.vue-clamp:not(.job-title)"));
    const location = textOf(document.querySelector(".company-location"));
    const companyLink = document.querySelector('h1 a[data-test="company-name"]');
    const descriptionBox = document.querySelector(".job-detail-container-description");

    return {
      ...EMPTY,
      title: title || "",
      company: company || "",
      location: location || "",
      description: textOf(descriptionBox),
      descriptionHtml: descriptionBox ? sanitizeDescriptionHtml(descriptionBox) : null,
      companyKariyerNetUrl: companyLink?.href || null,
      // kariyer.net publishes no contact for the posting at all, which is why the HR fields stay
      // empty here and editable in the form.
      foundBy: "kariyer",
    };
  }

  // ---- Everything else: schema.org/JobPosting ------------------------------------------------
  // The reason 0.9.0 covers hundreds of sites without an adapter each: Google for Jobs made
  // JSON-LD JobPosting markup near-universal on ATS boards (Greenhouse, Lever, Ashby, Workday,
  // Workable, SmartRecruiters and most country boards publish it), so one reader serves them all.
  function scrapeJsonLd() {
    function flatten(node, out) {
      if (Array.isArray(node)) {
        node.forEach((item) => flatten(item, out));
        return out;
      }
      if (node && typeof node === "object") {
        out.push(node);
        // Sites wrap their entities in @graph as often as they list them at the top level.
        if (node["@graph"]) {
          flatten(node["@graph"], out);
        }
      }
      return out;
    }

    function isJobPosting(node) {
      const type = node["@type"];
      return Array.isArray(type) ? type.includes("JobPosting") : type === "JobPosting";
    }

    function firstOf(value) {
      return Array.isArray(value) ? value[0] : value;
    }

    function nameOf(value) {
      const node = firstOf(value);
      if (!node) {
        return null;
      }
      return typeof node === "string" ? node.trim() : (node.name || "").trim() || null;
    }

    // "Istanbul, Türkiye" out of schema.org's nested PostalAddress. Locality and country are what
    // a person would type; the region only helps where it is the only thing given (US states).
    function locationOf(jobLocation) {
      const node = firstOf(jobLocation);
      const address = node?.address ? firstOf(node.address) : null;
      if (!address) {
        return null;
      }
      if (typeof address === "string") {
        return address.trim() || null;
      }
      const parts = [address.addressLocality, address.addressRegion, address.addressCountry]
        .map((part) => (typeof part === "object" && part ? part.name : part))
        .map((part) => (typeof part === "string" ? part.trim() : ""))
        .filter(Boolean);
      // Region is dropped when a locality is present, so "Istanbul, Istanbul, TR" does not happen.
      const trimmed = parts.length === 3 ? [parts[0], parts[2]] : parts;
      return trimmed.join(", ") || null;
    }

    // schema.org's description is an HTML string, written by whoever posted the job. DOMParser
    // reads it because the document it builds is inert — no scripts run, no images or other
    // subresources are fetched — unlike assigning the string to a live node's innerHTML.
    //
    // Deliberately no regex pre-pass over the string. An earlier version cut <script>/<style>
    // blocks out with two `.replace()` calls, and CodeQL was right to flag them: a regular
    // expression cannot filter HTML tags correctly. `</script foo="bar">` is a valid end tag and
    // the pattern missed it, and one pass over `<scr<script>ipt>` leaves a live `<script` behind.
    // Two things already do this job properly: the parser, which does not execute what it parses,
    // and sanitizeDescriptionHtml, which drops SCRIPT and STYLE elements from the output by tag
    // name rather than by text matching. (The tests run on happy-dom, whose parseFromString does
    // evaluate scripts unlike a browser's; vitest.config.js turns that off with
    // disableJavaScriptEvaluation, which is the right place for an environment quirk to be
    // handled — not in shipped code.)
    function descriptionOf(raw) {
      if (typeof raw !== "string" || !raw.trim()) {
        return { text: null, html: null };
      }
      const parsed = new DOMParser().parseFromString(raw, "text/html");
      const text = parsed.body.textContent?.replace(/\n{3,}/g, "\n\n").trim() || null;
      return { text, html: text ? sanitizeDescriptionHtml(parsed.body) : null };
    }

    const nodes = [];
    for (const script of document.querySelectorAll('script[type="application/ld+json"]')) {
      try {
        flatten(JSON.parse(script.textContent), nodes);
      } catch {
        // One malformed block on the page must not cost the ones that parse — several sites ship
        // a broken analytics blob next to a perfectly good JobPosting.
      }
    }

    const posting = nodes.find(isJobPosting);
    if (posting) {
      const { text, html } = descriptionOf(posting.description);
      const datePosted = typeof posting.datePosted === "string" ? posting.datePosted : null;
      return {
        ...EMPTY,
        title: (typeof posting.title === "string" ? posting.title : nameOf(posting.name)) || "",
        company: nameOf(posting.hiringOrganization) || "",
        location: locationOf(posting.jobLocation) || "",
        description: text,
        descriptionHtml: html,
        // Kept as the site wrote it; the backend parses and rejects what it cannot read, rather
        // than this guessing at a format.
        publishedAt: datePosted,
        foundBy: "jsonld",
      };
    }

    // No structured data: Open Graph, then the document title. Enough to save typing the company
    // and title on a plain careers page, and clearly marked as guessed in the popup.
    const ogTitle = document.querySelector('meta[property="og:title"]')?.content?.trim() || null;
    const ogDescription = document.querySelector('meta[property="og:description"]')?.content?.trim() || null;
    const ogSiteName = document.querySelector('meta[property="og:site_name"]')?.content?.trim() || null;
    const title = ogTitle || document.title?.trim() || "";

    return {
      ...EMPTY,
      title,
      company: ogSiteName || "",
      description: ogDescription,
      foundBy: title || ogDescription ? "meta" : null,
    };
  }

  if (config.strategy === "linkedin") {
    return clamp(await scrapeLinkedIn(config.jobId));
  }
  if (config.strategy === "kariyer") {
    return clamp(scrapeKariyerNet());
  }
  return clamp(scrapeJsonLd());
}
