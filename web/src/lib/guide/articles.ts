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
};

export const GUIDE_PATH = "/guide";

export const GUIDE_ARTICLES: GuideArticle[] = [
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

export function isGuideLocale(locale: string): locale is GuideLocale {
  return (GUIDE_LOCALES as readonly string[]).includes(locale);
}
