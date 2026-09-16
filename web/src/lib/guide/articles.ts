/**
 * The guide articles — the whole content plan as one table.
 *
 * This is deliberately *not* inside the .mdx files. Slugs, dates and the article's own title are
 * routing and metadata concerns: the sitemap, the hreflang pairs, `generateMetadata` and the index
 * page all need them, and none of those should have to compile MDX to find out a title. The .mdx
 * files hold prose and nothing else, which is also the pleasanter thing to author.
 *
 * Titles and descriptions do not live in `messages/*.json` either. Those catalogues are UI strings,
 * checked key-for-key across locales; an article is a piece of content that may exist in one
 * language before the other, and burying prose in them would make both harder to read.
 */

import { routing } from "@/i18n/routing";

export const GUIDE_LOCALES = ["tr", "en"] as const;
export type GuideLocale = (typeof GUIDE_LOCALES)[number];

export type GuideArticleCopy = {
  /** The locale's own slug — this is where the search terms live, so it is never shared. */
  slug: string;
  title: string;
  description: string;
};

export type GuideArticle = {
  /** Stable, locale-independent id. Also the .mdx file's basename: `<key>.<locale>.mdx`. */
  key: string;
  /** ISO date. Shown to the reader and given to Google as `datePublished`. */
  published: string;
  updated?: string;
  copy: Record<GuideLocale, GuideArticleCopy>;
  /** Keys of the articles linked at the foot of this one. */
  related: string[];
  /**
   * Leave out the "start for free" box at the foot of the article. For an article whose reader is
   * already signed in by construction — the review-writing guide is linked from the review form
   * and from a rejected review, both behind login — the box would be asking them to register.
   */
  hideRegisterCta?: true;
};

export const GUIDE_PATH = "/guide";

export const GUIDE_ARTICLES: GuideArticle[] = [
  {
    key: "writing-a-fair-review",
    published: "2026-09-16",
    related: ["reading-employee-reviews", "reapplying-to-the-same-company"],
    hideRegisterCta: true,
    copy: {
      tr: {
        slug: "adil-ve-faydali-bir-degerlendirme-yazmak",
        title: "Adil ve faydalı bir değerlendirme yazmak",
        description:
          "Puanlarını, o şirkete başvurmayı düşünen biri okuyacak. Yazacak bir şey yok: dürüst puan ver, gerçekten yaşadıklarını hazır maddelerden seç — olumsuz olsa bile.",
      },
      en: {
        slug: "writing-a-fair-review",
        title: "Writing a fair and useful review",
        description:
          "Someone weighing that company will read your ratings. Nothing to write: rate honestly and pick the ready-made statements you actually lived — negative ones too.",
      },
    },
  },
  {
    key: "reading-employee-reviews",
    published: "2026-09-16",
    related: ["writing-a-fair-review", "reapplying-to-the-same-company"],
    copy: {
      tr: {
        slug: "calisan-deneyimlerini-nasil-okumali",
        title: "Çalışan deneyimlerini nasıl okumalı",
        description:
          "Buradaki yorumlar sana bir şirketin içini gösterir — ama bir pencereden, bir kişinin gözüyle. Doğru okumak biraz dikkat ister.",
      },
      en: {
        slug: "how-to-read-employee-reviews",
        title: "How to read employee reviews",
        description:
          "The reviews here show you the inside of a company — but through one window, with one person's eyes. Reading them well takes a little care.",
      },
    },
  },
  {
    key: "linkedin-application-history",
    published: "2026-09-08",
    related: ["kariyer-net-application-history", "application-tracker-spreadsheet"],
    copy: {
      tr: {
        slug: "linkedin-basvuru-gecmisi-nasil-indirilir",
        title: "LinkedIn başvuru geçmişini nasıl indirirsin",
        description:
          "LinkedIn başvurularını gösterir ama dışa aktarmaz. Veri arşiviyle tüm geçmişini CSV olarak indirmenin adımları — ve dosyada ne çıkmadığı.",
      },
      en: {
        slug: "export-linkedin-application-history",
        title: "How to export your LinkedIn application history",
        description:
          "LinkedIn shows your applications but will not export them. How to request your data archive — and what the application CSV does not contain.",
      },
    },
  },
  {
    key: "application-tracker-spreadsheet",
    published: "2026-09-08",
    related: ["linkedin-application-history", "how-many-applications"],
    copy: {
      tr: {
        slug: "is-basvuru-takip-excel-sablonu",
        title: "İş başvuru takip Excel şablonu (ücretsiz)",
        description:
          "İş arayan için, İK için değil: bekleme süresini ve geri dönüş oranını kendisi hesaplayan ücretsiz bir takip tablosu — ve elle takibin sınırı.",
      },
      en: {
        slug: "job-application-tracker-spreadsheet",
        title: "A job application tracker spreadsheet (free)",
        description:
          "Built for the candidate, not the recruiter: a free tracker that works out waiting time and reply rate for you — and where a spreadsheet stops working.",
      },
    },
  },
  {
    key: "response-time",
    published: "2026-09-08",
    related: ["rejection-email", "how-many-applications"],
    copy: {
      tr: {
        slug: "is-basvurusundan-sonra-ne-kadar-beklenir",
        title: "İş başvurusundan sonra ne kadar beklenir?",
        description:
          "Dönüş süresi neye bağlı, ne zaman takip maili atılır, bir başvuruyu ne zaman kapatmak gerekir — ve bunu neden tahmine bırakmak zorunda değilsin.",
      },
      en: {
        slug: "how-long-to-wait-after-applying",
        title: "How long should you wait after applying?",
        description:
          "What actually drives reply time, when a follow-up helps, when to write an application off — and why none of it has to be guesswork.",
      },
    },
  },
  {
    key: "rejection-email",
    published: "2026-09-08",
    related: ["response-time", "reapplying-to-the-same-company"],
    copy: {
      tr: {
        slug: "basvurunuz-olumsuz-sonuclandi-ne-demek",
        title: "“Başvurunuz olumsuz sonuçlandı” ne demek?",
        description:
          "Red mailindeki kalıp cümleler ne anlatır, ne anlatmaz; hangisi gerçek bir gerekçe taşır, hangisi otomatik gönderimdir — ve o mailden ne çıkarman gerekir.",
      },
      en: {
        slug: "what-a-rejection-email-means",
        title: "What a rejection email actually tells you",
        description:
          "Which stock phrases carry a real reason, which are automated, what the timing says — and what to take from a rejection beyond the disappointment.",
      },
    },
  },
  {
    key: "kariyer-net-application-history",
    published: "2026-09-08",
    related: ["linkedin-application-history", "application-tracker-spreadsheet"],
    copy: {
      tr: {
        slug: "kariyer-net-basvurularim-nerede",
        title: "kariyer.net başvurularım nerede, neden eksiliyor?",
        description:
          "Başvurularım ekranını nerede bulacağın, eski başvuruların neden görünmez olduğu, ve kaydını kalıcı hâle getirmenin iki yolu — KVKK veri talebi dahil.",
      },
      en: {
        slug: "kariyer-net-application-history",
        title: "Where your kariyer.net applications go",
        description:
          "Where the applications screen is, why older entries stop showing, and two ways to keep a permanent record — including a KVKK data request.",
      },
    },
  },
  {
    key: "reapplying-to-the-same-company",
    published: "2026-09-08",
    related: ["rejection-email", "response-time"],
    copy: {
      tr: {
        slug: "ayni-sirkete-tekrar-basvurmak",
        title: "Aynı şirkete tekrar başvurmak: ne zaman, ne kadar sonra",
        description:
          "Reddedildikten sonra aynı şirkete yeniden başvurmak kaydını bozar mı, ne kadar beklemeli, aynı ilana ikinci kez başvurmak ne işe yarar — ve ne zaman yaramaz.",
      },
      en: {
        slug: "reapplying-to-the-same-company",
        title: "Reapplying to a company that rejected you",
        description:
          "Does a second application hurt your record, how long to wait, when reapplying to the same posting is worth it — and when it is simply noise.",
      },
    },
  },
  {
    key: "how-many-applications",
    published: "2026-09-08",
    related: ["application-tracker-spreadsheet", "response-time"],
    copy: {
      tr: {
        slug: "kac-is-basvurusu-yapmak-gerekir",
        title: "Kaç iş başvurusu yapmak gerekir?",
        description:
          "Dolaşan sayılar neden birbirini tutmuyor, senin için anlamlı olan tek ölçü hangisi, ve düşük geri dönüş oranının gerçekte neyi işaret ettiği.",
      },
      en: {
        slug: "how-many-job-applications",
        title: "How many job applications does it take?",
        description:
          "Why the numbers you find contradict each other, the one measure that means anything for you, and what a low reply rate is actually telling you.",
      },
    },
  },
];

export function articlePath(article: GuideArticle, locale: GuideLocale): string {
  return `${GUIDE_PATH}/${article.copy[locale].slug}`;
}

/** The article's path in every locale — the hreflang set for a page whose slug is translated. */
export function articlePaths(article: GuideArticle): Record<string, string> {
  return Object.fromEntries(GUIDE_LOCALES.map((locale) => [locale, articlePath(article, locale)]));
}

export function findArticleBySlug(slug: string, locale: GuideLocale): GuideArticle | undefined {
  return GUIDE_ARTICLES.find((article) => article.copy[locale].slug === slug);
}

export function findArticleByKey(key: string): GuideArticle | undefined {
  return GUIDE_ARTICLES.find((article) => article.key === key);
}

export type GuideSlugResolution = {
  article: GuideArticle;
  /**
   * Set when the slug belongs to the article in *another* locale: the page the reader asked for
   * exists, just under this locale's own slug. Search Console showed Google (and the language
   * switcher, which keeps the path and swaps the prefix) asking for `/tr/guide/<english slug>`
   * and getting a 404 — the locale in the URL is what the reader chose, so they get that locale's
   * article at its own address rather than a dead end. Acted on by the proxy, never by the page:
   * the article pages are static, and a redirect issued from inside one turns it dynamic at
   * runtime, which Next refuses with a 500 (seen in production on 2026-09-16).
   */
  redirectTo?: string;
};

/** The article behind a slug in the requested locale, or behind the same slug in any other one. */
export function resolveGuideSlug(slug: string, locale: GuideLocale): GuideSlugResolution | undefined {
  const own = findArticleBySlug(slug, locale);
  if (own) return { article: own };

  for (const other of GUIDE_LOCALES) {
    if (other === locale) continue;
    const article = findArticleBySlug(slug, other);
    if (article) return { article, redirectTo: articlePath(article, locale) };
  }

  return undefined;
}

/**
 * Where a guide URL that names a real article at the wrong address should go, or null when the
 * path is fine (or not a guide article at all) and takes the normal route. Two shapes:
 *
 * - `/<locale>/guide/<slug>` where the slug is the *other* locale's: the locale in the URL wins,
 *   so the reader gets that locale's article at its own slug (see `resolveGuideSlug`).
 * - `/guide/<slug>` with no locale: next-intl would prefix it with the locale it guesses from the
 *   cookie or Accept-Language, and an English slug under `/tr` is the first shape all over again;
 *   the slug itself says which language the reader wants, so that language wins here.
 *
 * Unknown slugs, `/guide` itself and every other path are null.
 */
export function guideRedirectForPath(pathname: string): string | null {
  const prefixed = /^\/(tr|en)\/guide\/([^/]+)\/?$/.exec(pathname);
  if (prefixed) {
    const locale = prefixed[1] as GuideLocale;
    const resolved = resolveGuideSlug(decodeURIComponent(prefixed[2]), locale);
    return resolved?.redirectTo ? `/${locale}${resolved.redirectTo}` : null;
  }

  const unprefixed = /^\/guide\/([^/]+)\/?$/.exec(pathname);
  if (!unprefixed) return null;

  const slug = decodeURIComponent(unprefixed[1]);
  for (const locale of GUIDE_LOCALES) {
    const article = findArticleBySlug(slug, locale);
    if (article) return `/${locale}${articlePath(article, locale)}`;
  }

  return null;
}

export function isGuideLocale(locale: string): locale is GuideLocale {
  return (GUIDE_LOCALES as readonly string[]).includes(locale);
}

/**
 * The path of an article by key, for the product screens that link into the guide (the review
 * form's rules panel, a company page's review list, the help centre). A key nobody registered is
 * a programming error, so it throws rather than rendering a link to nowhere; a locale the guide
 * does not have falls back to the default one, as every other localised path does.
 */
export function guidePath(key: string, locale: string): string {
  const article = findArticleByKey(key);
  if (!article) throw new Error(`No guide article "${key}"`);
  return articlePath(article, isGuideLocale(locale) ? locale : routing.defaultLocale);
}
