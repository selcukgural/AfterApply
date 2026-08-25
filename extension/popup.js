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
function scrapeLinkedInJob(jobId) {
  function textOf(el) {
    return el?.textContent?.trim() || null;
  }

  const titleLink = document.querySelector(`a[href*="/jobs/view/${jobId}"]`);
  const title = textOf(titleLink);

  const companyLink = document.querySelector('a[href*="/company/"]');
  const company = textOf(companyLink);

  // The location text is the first <span> in the metadata line that immediately follows the
  // title's paragraph (e.g. "Lisboa, Lisbon, Portugal · Reposted 1 week ago · ..."), two levels
  // up from the title link's own wrapping <p> — structure captured from a live page in
  // DECISIONS.md Sprint 9. The least load-bearing of the three fields: if this traversal doesn't
  // match on some other page layout, the user just types the location in by hand.
  let location = null;
  const titleParagraphWrapper = titleLink?.closest("p")?.parentElement?.parentElement;
  const metaParagraph = titleParagraphWrapper?.nextElementSibling?.nextElementSibling;
  if (metaParagraph?.tagName === "P") {
    location = textOf(metaParagraph.querySelector("span"));
  }

  const description = textOf(document.querySelector('[data-testid="expandable-text-box"]'));

  return {
    title: title || "",
    company: company || "",
    location: location || "",
    description: description ? description.slice(0, 5000) : null,
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
    renderMessage("Set up your AfterApply access token first.", "Open Settings", () => chrome.runtime.openOptionsPage());
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
    scraped = { title: "", company: "", location: "", description: null };
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
          publishedAt: null,
        }),
      });

      if (!response.ok) {
        throw new Error(`Request failed (${response.status})`);
      }

      const result = await response.json();
      statusEl.textContent = result.wasDuplicate
        ? "Already tracked — opened your existing application."
        : "Added to AfterApply.";
      statusEl.className = "status success";
      statusEl.hidden = false;
    } catch {
      statusEl.textContent = "Could not reach AfterApply. Check your Settings (API base URL/token).";
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
