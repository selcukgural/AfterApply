#!/usr/bin/env node
// One-off move of the file-based guides into the blog's editor (DECISIONS.md 2026-09-26).
//
// Reads the ten guides (twenty files) the web app ships today — `src/lib/guide/articles.ts` for
// titles, descriptions, slugs, dates, related links and the sign-up-box flag, `src/content/guide`
// for the prose — and writes each one through the admin API as a guide post, so the database ends
// up holding exactly what the site shows: same slugs, same publish dates, same pairs.
//
// Usage (from web/):
//   AA_API_URL=http://localhost:5080 AA_ADMIN_TOKEN=<access token> node scripts/import-guides.mjs --dry-run
//   AA_API_URL=https://api.ekariyerim.com AA_ADMIN_TOKEN=<access token> node scripts/import-guides.mjs
//
// AA_ADMIN_TOKEN is a signed-in admin's access token (the Bearer value the web app sends; it
// expires in minutes, so copy a fresh one right before a run). It is read from the environment
// only and never printed. --dry-run converts every file and prints the HTML without calling the
// API. A real run refuses to start when the guide table already has posts, so a second run cannot
// create duplicates; after it, check the admin "Rehber" tab before the public pages switch over.

import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { createJiti } from "jiti";

const web = join(dirname(fileURLToPath(import.meta.url)), "..");
const jiti = createJiti(import.meta.url, { alias: { "@": join(web, "src") } });
const { GUIDE_ARTICLES, GUIDE_LOCALES } = await jiti.import(join(web, "src/lib/guide/articles.ts"));
const { markdownToEditor, imagePathsIn } = await jiti.import(join(web, "src/lib/guide/markdownToEditor.ts"));

const dryRun = process.argv.includes("--dry-run");
const apiUrl = (process.env.AA_API_URL ?? "").replace(/\/$/, "");
const token = process.env.AA_ADMIN_TOKEN ?? "";

const bodyOf = (key, locale) => readFileSync(join(web, "src/content/guide", `${key}.${locale}.mdx`), "utf8");

if (dryRun) {
  for (const article of GUIDE_ARTICLES) {
    for (const locale of GUIDE_LOCALES) {
      const markdown = bodyOf(article.key, locale);
      const images = new Map(imagePathsIn(markdown).map((path) => [path, `/api/blog/media/<upload of ${path}>`]));
      const { html } = markdownToEditor(markdown, images);
      console.log(`\n=== ${article.key}.${locale} → /${locale}/guide/${article.copy[locale].slug} (${article.published})`);
      console.log(html);
    }
  }
  process.exit(0);
}

if (!apiUrl || !token) {
  console.error("AA_API_URL and AA_ADMIN_TOKEN are required (or pass --dry-run).");
  process.exit(1);
}

async function api(method, path, body, { multipart = false } = {}) {
  for (let attempt = 1; ; attempt++) {
    const response = await fetch(`${apiUrl}${path}`, {
      method,
      headers: {
        Authorization: `Bearer ${token}`,
        "Accept-Language": "en",
        ...(body !== undefined && !multipart ? { "Content-Type": "application/json" } : {}),
      },
      body: body === undefined ? undefined : multipart ? body : JSON.stringify(body),
    });
    if (response.status === 429 && attempt < 5) {
      const wait = Number(response.headers.get("Retry-After") ?? "5") * 1000;
      console.log(`  429 on ${method} ${path}; waiting ${wait / 1000}s`);
      await new Promise((resolve) => setTimeout(resolve, wait));
      continue;
    }
    if (!response.ok) {
      throw new Error(`${method} ${path} → ${response.status}: ${await response.text()}`);
    }
    return response.status === 204 ? null : response.json();
  }
}

// Refuse to run twice.
const existing = await api("GET", "/api/admin/blog/posts?kind=Guide");
if (existing.totalCount > 0) {
  console.error(`The guide table already has ${existing.totalCount} post(s); nothing was written.`);
  process.exit(1);
}

const EMPTY_DOC = JSON.stringify({ type: "doc", content: [] });
// key → locale → { id, revision, markdown, images }
const created = new Map();

// 1. Every guide as a draft, Turkish first so the English one can link to it at creation.
for (const article of GUIDE_ARTICLES) {
  const perLocale = {};
  for (const locale of GUIDE_LOCALES) {
    const copy = article.copy[locale];
    const markdown = bodyOf(article.key, locale);
    const post = await api("POST", "/api/admin/blog/posts", {
      title: copy.title,
      excerpt: copy.description,
      contentJson: EMPTY_DOC,
      contentHtml: "",
      language: locale,
      slug: copy.slug,
      translationOfPostId: locale === "tr" ? null : (perLocale.tr?.id ?? null),
      kind: "Guide",
    });

    // Images belong to a post, so they go up once the post exists; then the body points at them.
    const images = new Map();
    for (const path of imagePathsIn(markdown)) {
      const bytes = readFileSync(join(web, "public", path));
      const form = new FormData();
      form.append("file", new Blob([bytes]), path.split("/").pop());
      const media = await api("POST", `/api/admin/blog/posts/${post.id}/media`, form, { multipart: true });
      images.set(path, media.url);
    }

    perLocale[locale] = { id: post.id, revision: post.revision, markdown, images, copy };
    console.log(`created ${article.key}.${locale} (${post.id})`);
  }
  created.set(article.key, perLocale);
}

// 2. The body, the related links and the sign-up-box flag — once every draft exists, since a
//    related link may point at a guide further down the list.
for (const article of GUIDE_ARTICLES) {
  for (const locale of GUIDE_LOCALES) {
    const entry = created.get(article.key)[locale];
    const { json, html } = markdownToEditor(entry.markdown, entry.images);
    const saved = await api("PUT", `/api/admin/blog/posts/${entry.id}/draft`, {
      title: entry.copy.title,
      excerpt: entry.copy.description,
      contentJson: JSON.stringify(json),
      contentHtml: html,
      language: locale,
      slug: entry.copy.slug,
      coverMediaId: null,
      translationOfPostId: locale === "tr" ? null : created.get(article.key).tr.id,
      revision: entry.revision,
      seo: { seoTitle: null, primaryKeyword: null, secondaryKeywords: [], coverAlt: null },
      guide: {
        hideRegisterCta: article.hideRegisterCta ?? false,
        relatedPostIds: article.related.slice(0, 2).map((key) => created.get(key)[locale].id),
      },
    });
    entry.revision = saved.revision;
  }
}

// 3. Publish with the date each guide has carried since it first went live.
for (const article of GUIDE_ARTICLES) {
  for (const locale of GUIDE_LOCALES) {
    const entry = created.get(article.key)[locale];
    const published = await api("POST", `/api/admin/blog/posts/${entry.id}/publish`, {
      // Midday UTC: the same calendar day in Istanbul and in every European time zone.
      publishedAt: `${article.published}T09:00:00Z`,
    });
    console.log(`published /${locale}/guide/${published.slug}`);
  }
}

console.log(`\nDone: ${GUIDE_ARTICLES.length * GUIDE_LOCALES.length} guide posts. Check the admin "Rehber" tab.`);
