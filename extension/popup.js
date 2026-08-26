import { getSettings } from "./storage.js";

const content = document.getElementById("content");

// LinkedIn only puts a job on its own /jobs/view/<id> URL when you open it in a dedicated
// tab/page. The far more common path — browsing /jobs/search-results/ (or /jobs/search/,
// /jobs/collections/...) and clicking a result — keeps the URL on that listing page and opens
// the job in a side panel via `?currentJobId=<id>`, never navigating to /jobs/view/ at all.
// Found via manual testing (see DECISIONS.md Sprint 9) — the original /jobs/view/-only pattern
// never matched that far more common case. Both shapes resolve to the same canonical job id, and
// we always submit the canonical https://www.linkedin.com/jobs/view/<id>/ URL to the backend
// (regardless of which LinkedIn page shape the id came from) — LinkedInJobIdExtractor on the
// backend expects that exact shape, and it keeps JobUrl dedup stable across the two entry points.
function extractLinkedInJobId(url) {
  let parsed;
  try {
    parsed = new URL(url);
  } catch {
    return null;
  }

  if (parsed.hostname !== "www.linkedin.com") {
    return null;
  }

  const viewMatch = parsed.pathname.match(/\/jobs\/view\/(\d+)/);
  if (viewMatch) {
    return viewMatch[1];
  }

  const currentJobId = parsed.searchParams.get("currentJobId");
  if (currentJobId && /^\d+$/.test(currentJobId)) {
    return currentJobId;
  }

  return null;
}

// Injected into the LinkedIn tab via chrome.scripting.executeScript — must be fully
// self-contained (no references to anything outside this function body; jobId is passed in via
// executeScript's `args`, not a closure reference).
//
// LinkedIn's CSS classes here are atomic/hashed (e.g. "_59162b76 d68df9b8 ...") and carry zero
// semantic meaning — confirmed by inspecting two live pages during Sprint 9 manual testing
// (DECISIONS.md), not an assumption; no selector written against those exact hashes would
// survive LinkedIn's next deploy. What's stable instead: the job title and company name are each
// wrapped in an <a> whose href LinkedIn needs for actual routing/SEO — /jobs/view/<id> for the
// title, /company/<slug>/ for the company — verified against both the search-results split-view
// panel and the plain job list card. Scoping the title lookup to the exact jobId (rather than
// "first /jobs/view/ link on the page") avoids picking up an unrelated "similar jobs" link
// elsewhere on the page.
//
// Every field this returns is still shown as an editable input in the popup before submitting —
// scraping failure or a wrong guess degrades to "user fills it in by hand", never a silent wrong
// submission on its own.
async function scrapeLinkedInJob(jobId) {
  function textOf(el) {
    return el?.textContent?.trim() || null;
  }

  const titleLink = document.querySelector(`a[href*="/jobs/view/${jobId}"]`);
  const title = textOf(titleLink);

  const companyLink = document.querySelector('a[href*="/company/"]');
  const company = textOf(companyLink);

  // The location is the first segment (before the "·" separator) of a metadata line like
  // "Istanbul, Türkiye · Reposted 5 days ago · Over 100 people clicked apply", rendered as a
  // <p> sibling of the title's own wrapping element. Found via live-page inspection (Sprint 13,
  // DECISIONS.md): a *fixed* hop count from the title link doesn't work because LinkedIn nests
  // this differently depending on job card variant (promoted vs. not) — 2 levels up from the
  // title's <p> for one, 3 for the other, observed on the same search-results page back to back.
  // Instead: walk up from the title's <p>, and at each ancestor level look for a direct-child
  // <p> that isn't the title's own paragraph and contains "·" — level-count-independent, matches
  // both variants tested. Still the least load-bearing of the three fields: if no page layout
  // matches, the user just types the location in by hand.
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

  // LinkedIn doesn't render the full description text into the DOM up front on this layout —
  // only the truncated, visible portion is there until the "…more" button is clicked, at which
  // point React renders the rest. Click every such button (there's one for "About the job" and
  // a separate one for "About the company") and give React a beat to re-render before reading
  // textContent — found via manual testing (a real ilan came back truncated at "…more").
  document.querySelectorAll('[data-testid="expandable-text-button"]').forEach((button) => button.click());
  await new Promise((resolve) => setTimeout(resolve, 150));

  const descriptionBox = document.querySelector('[data-testid="expandable-text-box"]');
  const description = textOf(descriptionBox);

  // Allow-listed HTML snapshot for a formatted display (bold/headers/bullet lists, same as the
  // original listing) — separate from the plain-text `description` above, which stays untouched
  // for the AI Job Matching prompt. This is a best-effort capture-time filter, NOT a security
  // boundary on its own: the backend stores it as-is and the frontend re-sanitizes with DOMPurify
  // before ever rendering it (see DECISIONS.md — untrusted content is untrusted regardless of
  // which side captured it).
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

  const descriptionHtml = descriptionBox ? sanitizeDescriptionHtml(descriptionBox) : null;

  return {
    title: title || "",
    company: company || "",
    location: location || "",
    // Matches CreateFromExtensionRequestValidator's MaximumLength(10_000/20_000) on the backend.
    description: description ? description.slice(0, 10_000) : null,
    descriptionHtml: descriptionHtml ? descriptionHtml.slice(0, 20_000) : null,
  };
}

function render(html) {
  content.innerHTML = html;
}

function renderMessage(text, linkText, onLinkClick) {
  render(`<p class="muted">${text}</p>`);
  if (linkText) {
    const button = document.createElement("button");
    button.className = "secondary";
    button.textContent = linkText;
    button.addEventListener("click", onLinkClick);
    content.appendChild(button);
  }
}

async function main() {
  const settings = await getSettings();

  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  const jobId = tab?.url ? extractLinkedInJobId(tab.url) : null;
  if (!jobId) {
    renderMessage("Open a LinkedIn job posting (a /jobs/view/ page, or a job selected in search results) to track it here.");
    return;
  }
  const jobUrl = `https://www.linkedin.com/jobs/view/${jobId}/`;

  if (!settings.token) {
    renderMessage("Set up your e-kariyerim access token first.", "Open Settings", () => chrome.runtime.openOptionsPage());
    return;
  }

  let scraped;
  let scrapeError = null;
  try {
    const [{ result }] = await chrome.scripting.executeScript({
      target: { tabId: tab.id },
      func: scrapeLinkedInJob,
      args: [jobId],
    });
    scraped = result;
  } catch (error) {
    scraped = { title: "", company: "", location: "", description: null, descriptionHtml: null };
    // Surfaced inline (not just console.error) so a manual tester doesn't need DevTools open to
    // see why fields came back empty — found necessary in Sprint 9 manual testing, where the
    // silently-swallowed error made an actual scrape failure look identical to "nothing found".
    scrapeError = error?.message || String(error);
  }

  render(`
    ${scrapeError ? `<p class="status error">Auto-fill failed: ${escapeHtml(scrapeError)}</p>` : ""}
    <label for="companyName">Company</label>
    <input id="companyName" type="text" value="${escapeHtml(scraped.company)}" />

    <label for="jobTitle">Job title</label>
    <input id="jobTitle" type="text" value="${escapeHtml(scraped.title)}" />

    <label for="location">Location</label>
    <input id="location" type="text" value="${escapeHtml(scraped.location)}" />

    <button id="submit">I Applied</button>
    <p id="status" class="status" hidden></p>
  `);

  document.getElementById("submit").addEventListener("click", async () => {
    const submitButton = document.getElementById("submit");
    const statusEl = document.getElementById("status");
    submitButton.disabled = true;
    statusEl.hidden = true;

    const companyName = document.getElementById("companyName").value.trim();
    const jobTitle = document.getElementById("jobTitle").value.trim();
    const location = document.getElementById("location").value.trim();

    if (!companyName || !jobTitle) {
      statusEl.textContent = "Company and job title are required.";
      statusEl.className = "status error";
      statusEl.hidden = false;
      submitButton.disabled = false;
      return;
    }

    try {
      const response = await fetch(`${settings.apiBaseUrl}/api/applications/from-extension`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${settings.token}`,
        },
        body: JSON.stringify({
          companyName,
          jobTitle,
          jobUrl,
          location: location || null,
          description: scraped.description,
          descriptionHtml: scraped.descriptionHtml,
          publishedAt: null,
        }),
      });

      if (!response.ok) {
        throw new Error(`Request failed (${response.status})`);
      }

      const result = await response.json();
      statusEl.textContent = result.wasDuplicate
        ? "Already tracked — opened your existing application."
        : "Added to e-kariyerim.";
      statusEl.className = "status success";
      statusEl.hidden = false;
    } catch {
      statusEl.textContent = "Could not reach e-kariyerim. Check your Settings (API base URL/token).";
      statusEl.className = "status error";
      statusEl.hidden = false;
      submitButton.disabled = false;
    }
  });
}

function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value;
  return div.innerHTML;
}

main();
