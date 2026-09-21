"use client";

import { useState, type KeyboardEvent } from "react";
import { useTranslations } from "next-intl";
import type { BlogLanguage, BlogSeo, BlogSeoSuggestion } from "@/types/api";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/dashboard/Card";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Textarea } from "@/components/ui/Textarea";
import {
  META_DESCRIPTION_MAX,
  META_DESCRIPTION_MIN,
  SECONDARY_KEYWORDS_MAX,
  SEO_CHECK_SOURCES,
  SEO_TITLE_MAX,
  effectiveTitle,
  seoChecklist,
  seoScore,
  type SeoCheck,
  type SeoScore,
} from "@/lib/blog/seoChecks";
import { SITE_NAME } from "@/lib/seo/routes";

export interface BlogSeoSectionProps {
  language: BlogLanguage;
  title: string;
  excerpt: string;
  onExcerptChange: (value: string) => void;
  seo: BlogSeo;
  onSeoChange: (value: BlogSeo) => void;
  /** The address as it stands: hand-typed, or generated from the title while nothing was typed. */
  slug: string;
  slugLocked: boolean;
  /** Whether the shown slug is the author's own; false means it follows the title. */
  slugTouched: boolean;
  onSlugChange: (value: string) => void;
  onSlugReset: () => void;
  hasCover: boolean;
  /** The body as HTML, for the word count and the keyword checks. */
  contentHtml: string;
  /** Asks the model, after the draft is saved so it reads what is on screen. Null: no post yet. */
  onSuggest: (() => Promise<BlogSeoSuggestion>) | null;
}

const SCORE_CLASS: Record<SeoScore["band"], string> = {
  good: "bg-good-wash text-good-ink",
  fair: "bg-warn-wash text-warn-ink",
  poor: "bg-crit-wash text-crit-ink",
};

const CHECK_DOT: Record<SeoCheck["status"], string> = {
  ok: "bg-good",
  warn: "bg-warn",
  missing: "bg-crit",
};

/** The sidebar's small score pill — the same numbers the section shows, at a glance. */
export function SeoScorePill({ score }: { score: SeoScore }) {
  const t = useTranslations("adminBlog.seo");
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold tabular-nums ${SCORE_CLASS[score.band]}`}>
      {t("score", { ok: score.ok, total: score.total })}
    </span>
  );
}

/** The checklist input, from the section's props — the sidebar card computes the same score from it. */
export function seoInputOf(props: Pick<BlogSeoSectionProps, "title" | "excerpt" | "seo" | "slug" | "hasCover" | "contentHtml">) {
  return {
    title: props.title,
    seoTitle: props.seo.seoTitle ?? "",
    excerpt: props.excerpt,
    slug: props.slug,
    primaryKeyword: props.seo.primaryKeyword ?? "",
    secondaryKeywords: props.seo.secondaryKeywords,
    coverAlt: props.seo.coverAlt ?? "",
    hasCover: props.hasCover,
    contentHtml: props.contentHtml,
  };
}

/**
 * The SEO section under the body (DECISIONS.md 2026-09-21, canvas variant B): the fields on the
 * left, what a search result and a share card would show on the right, and the checklist. Every
 * number here is a guide, not a cap — the store's caps are wider and the validator names the
 * field when one is crossed.
 */
export function BlogSeoSection(props: BlogSeoSectionProps) {
  const { language, title, excerpt, onExcerptChange, seo, onSeoChange, slug, slugLocked, slugTouched, onSlugChange, onSlugReset, hasCover } = props;
  const t = useTranslations("adminBlog.seo");
  const [keywordDraft, setKeywordDraft] = useState("");
  const [suggestion, setSuggestion] = useState<BlogSeoSuggestion | null>(null);
  const [suggesting, setSuggesting] = useState(false);
  const [suggestError, setSuggestError] = useState<string | null>(null);

  const checks = seoChecklist(seoInputOf(props));
  const score = seoScore(checks);
  const shownTitle = effectiveTitle({ title, seoTitle: seo.seoTitle ?? "" });
  const fullTitle = shownTitle ? `${shownTitle} · ${SITE_NAME}` : SITE_NAME;
  const seoTitleLength = (seo.seoTitle ?? "").trim().length || title.trim().length;
  const excerptLength = excerpt.trim().length;

  const setField = <K extends keyof BlogSeo>(key: K, value: BlogSeo[K]) => onSeoChange({ ...seo, [key]: value });

  const addKeyword = (raw: string) => {
    const parts = raw
      .split(",")
      .map((k) => k.trim())
      .filter((k) => k.length > 0);
    if (parts.length === 0) return;
    const existing = new Set(seo.secondaryKeywords.map((k) => k.toLocaleLowerCase("tr")));
    const next = [...seo.secondaryKeywords];
    for (const part of parts) {
      if (next.length >= SECONDARY_KEYWORDS_MAX) break;
      if (existing.has(part.toLocaleLowerCase("tr"))) continue;
      existing.add(part.toLocaleLowerCase("tr"));
      next.push(part);
    }
    setField("secondaryKeywords", next);
    setKeywordDraft("");
  };

  const suggest = async () => {
    if (!props.onSuggest || suggesting) return;
    setSuggesting(true);
    setSuggestError(null);
    try {
      setSuggestion(await props.onSuggest());
    } catch (err) {
      setSuggestError(err instanceof Error && err.message ? err.message : t("suggestError"));
    } finally {
      setSuggesting(false);
    }
  };

  // What a suggestion row offers for a field: the proposal, and what "apply" does with it. A
  // proposal equal to the field's current value is not shown — nothing to apply.
  const proposals = suggestion
    ? {
        seoTitle: suggestion.seoTitle && suggestion.seoTitle !== (seo.seoTitle ?? "") ? suggestion.seoTitle : null,
        excerpt: suggestion.metaDescription && suggestion.metaDescription !== excerpt ? suggestion.metaDescription : null,
        primaryKeyword: suggestion.primaryKeyword && suggestion.primaryKeyword !== (seo.primaryKeyword ?? "") ? suggestion.primaryKeyword : null,
        secondaryKeywords:
          suggestion.secondaryKeywords.length > 0 && suggestion.secondaryKeywords.join("\u0000") !== seo.secondaryKeywords.join("\u0000")
            ? suggestion.secondaryKeywords
            : null,
        coverAlt: hasCover && suggestion.coverAlt && suggestion.coverAlt !== (seo.coverAlt ?? "") ? suggestion.coverAlt : null,
        slug: !slugLocked && suggestion.slug && suggestion.slug !== slug ? suggestion.slug : null,
      }
    : null;

  const applyEmpty = () => {
    if (!proposals) return;
    const next = { ...seo };
    if (!(seo.seoTitle ?? "").trim() && proposals.seoTitle) next.seoTitle = proposals.seoTitle;
    if (!(seo.primaryKeyword ?? "").trim() && proposals.primaryKeyword) next.primaryKeyword = proposals.primaryKeyword;
    if (seo.secondaryKeywords.length === 0 && proposals.secondaryKeywords) next.secondaryKeywords = proposals.secondaryKeywords;
    if (hasCover && !(seo.coverAlt ?? "").trim() && proposals.coverAlt) next.coverAlt = proposals.coverAlt;
    onSeoChange(next);
    if (!excerpt.trim() && proposals.excerpt) onExcerptChange(proposals.excerpt);
  };

  const onKeywordKey = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter" || e.key === ",") {
      e.preventDefault();
      addKeyword(keywordDraft);
    } else if (e.key === "Backspace" && keywordDraft === "" && seo.secondaryKeywords.length > 0) {
      setField("secondaryKeywords", seo.secondaryKeywords.slice(0, -1));
    }
  };

  return (
    <div id="blog-seo" className="scroll-mt-24">
      <Card className="flex flex-col gap-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-col gap-1">
            <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("heading")}</h2>
            <p className="text-xs text-gray-500 dark:text-gray-400">{t("intro")}</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <SeoScorePill score={score} />
            {props.onSuggest && (
              <Button variant="secondary" onClick={() => void suggest()} disabled={suggesting}>
                {suggesting ? t("suggesting") : suggestion ? t("suggestAgain") : t("suggest")}
              </Button>
            )}
            {proposals && Object.values(proposals).some(Boolean) && (
              <Button variant="outline" onClick={applyEmpty}>
                {t("applyEmpty")}
              </Button>
            )}
          </div>
        </div>
        {suggestError && (
          <p role="alert" className="text-sm text-red-600 dark:text-red-400">
            {suggestError}
          </p>
        )}
        {suggestion?.intentNote && (
          <p className="rounded-md border border-dashed border-gray-300 px-3 py-2 text-xs text-gray-600 dark:border-gray-700 dark:text-gray-400">
            <span className="font-semibold">{t("intentNote")}:</span> {suggestion.intentNote}
          </p>
        )}

        <div className="grid gap-6 md:grid-cols-2">
          <div className="flex flex-col gap-4">
            <FormField label={t("seoTitle")} htmlFor="blog-seo-title">
              <Input
                id="blog-seo-title"
                value={seo.seoTitle ?? ""}
                onChange={(e) => setField("seoTitle", e.target.value)}
                placeholder={title || undefined}
                maxLength={120}
                lang={language}
              />
              <LengthMeter value={seoTitleLength} max={SEO_TITLE_MAX} min={1} />
              <Proposal label={t("suggestion")} apply={t("apply")} value={proposals?.seoTitle ?? null} onApply={() => setField("seoTitle", proposals!.seoTitle)} />
              <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{t("seoTitleHint")}</p>
            </FormField>

            <FormField label={t("excerpt")} htmlFor="blog-excerpt">
              <Textarea
                id="blog-excerpt"
                value={excerpt}
                onChange={(e) => onExcerptChange(e.target.value)}
                maxLength={500}
                rows={3}
                lang={language}
                spellCheck
              />
              <LengthMeter value={excerptLength} max={META_DESCRIPTION_MAX} min={META_DESCRIPTION_MIN} />
              <Proposal label={t("suggestion")} apply={t("apply")} value={proposals?.excerpt ?? null} onApply={() => onExcerptChange(proposals!.excerpt!)} />
              <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{t("excerptHint")}</p>
            </FormField>

            <FormField label={t("primaryKeyword")} htmlFor="blog-seo-keyword">
              <Input
                id="blog-seo-keyword"
                value={seo.primaryKeyword ?? ""}
                onChange={(e) => setField("primaryKeyword", e.target.value)}
                placeholder={t("primaryKeywordPlaceholder")}
                maxLength={80}
                lang={language}
              />
              <Proposal label={t("suggestion")} apply={t("apply")} value={proposals?.primaryKeyword ?? null} onApply={() => setField("primaryKeyword", proposals!.primaryKeyword)} />
              <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{t("primaryKeywordHint")}</p>
            </FormField>

            <FormField label={t("secondaryKeywords")} htmlFor="blog-seo-keywords">
              <div className="flex flex-wrap items-center gap-1.5 rounded-md border border-gray-300 bg-white px-2 py-1.5 focus-within:border-accent dark:border-gray-700 dark:bg-gray-950">
                {seo.secondaryKeywords.map((keyword) => (
                  <span
                    key={keyword}
                    className="inline-flex items-center gap-1 rounded-full border border-accent bg-accent/10 px-2 py-0.5 text-xs text-accent-ink"
                  >
                    {keyword}
                    <button
                      type="button"
                      aria-label={t("removeKeyword", { keyword })}
                      className="rounded-full px-0.5 leading-none hover:bg-accent/20"
                      onClick={() =>
                        setField(
                          "secondaryKeywords",
                          seo.secondaryKeywords.filter((k) => k !== keyword),
                        )
                      }
                    >
                      ×
                    </button>
                  </span>
                ))}
                <input
                  id="blog-seo-keywords"
                  type="text"
                  value={keywordDraft}
                  onChange={(e) => setKeywordDraft(e.target.value)}
                  onKeyDown={onKeywordKey}
                  onBlur={() => addKeyword(keywordDraft)}
                  placeholder={seo.secondaryKeywords.length >= SECONDARY_KEYWORDS_MAX ? undefined : t("secondaryKeywordsPlaceholder")}
                  disabled={seo.secondaryKeywords.length >= SECONDARY_KEYWORDS_MAX}
                  maxLength={80}
                  lang={language}
                  className="min-w-[8rem] flex-1 bg-transparent px-1 py-0.5 text-sm text-gray-900 placeholder:text-gray-400 focus:outline-none dark:text-gray-100"
                />
              </div>
              <Proposal
                label={t("suggestion")}
                apply={t("apply")}
                value={proposals?.secondaryKeywords ? proposals.secondaryKeywords.join(" · ") : null}
                onApply={() => setField("secondaryKeywords", proposals!.secondaryKeywords!)}
              />
              <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{t("secondaryKeywordsHint", { max: SECONDARY_KEYWORDS_MAX })}</p>
            </FormField>

            {hasCover && (
              <FormField label={t("coverAlt")} htmlFor="blog-seo-cover-alt">
                <Input
                  id="blog-seo-cover-alt"
                  value={seo.coverAlt ?? ""}
                  onChange={(e) => setField("coverAlt", e.target.value)}
                  placeholder={t("coverAltPlaceholder")}
                  maxLength={300}
                  lang={language}
                />
                <Proposal label={t("suggestion")} apply={t("apply")} value={proposals?.coverAlt ?? null} onApply={() => setField("coverAlt", proposals!.coverAlt)} />
                <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{t("coverAltHint")}</p>
              </FormField>
            )}

            <FormField label={t("slug")} htmlFor="blog-slug">
              <Input
                id="blog-slug"
                value={slug}
                disabled={slugLocked}
                onChange={(e) => onSlugChange(e.target.value.toLowerCase())}
                pattern="[a-z0-9]+(-[a-z0-9]+)*"
              />
              <Proposal label={t("suggestion")} apply={t("apply")} value={proposals?.slug ?? null} onApply={() => onSlugChange(proposals!.slug!)} />
              <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                {slugLocked ? t("slugLocked") : t("slugHint")}
                {!slugLocked && slugTouched && (
                  <>
                    {" "}
                    <button type="button" className="text-accent-ink underline-offset-2 hover:underline" onClick={onSlugReset}>
                      {t("slugFromTitle")}
                    </button>
                  </>
                )}
                {slug && (
                  <>
                    {" "}
                    <code className="text-gray-700 dark:text-gray-300">{t("slugPreview", { language, slug })}</code>
                  </>
                )}
              </p>
            </FormField>
          </div>

          <div className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <span className="text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{t("serpHeading")}</span>
              <div
                className="rounded-lg border border-gray-200 bg-white px-3 py-2.5 dark:border-gray-800 dark:bg-gray-950"
                style={{ fontFamily: "Arial, Helvetica, sans-serif" }}
              >
                <div className="flex items-center gap-1.5 text-xs text-gray-600 dark:text-gray-400">
                  <span aria-hidden="true" className="inline-block h-4 w-4 rounded-full bg-accent" />
                  <span className="truncate">
                    ekariyerim.com › {language} › blog › {slug || "…"}
                  </span>
                </div>
                <div className="mt-0.5 truncate text-[17px] leading-snug text-[#1a0dab] dark:text-[#8ab4f8]">{fullTitle}</div>
                <div className="mt-0.5 line-clamp-2 text-[13px] leading-relaxed text-gray-700 dark:text-gray-300">{excerpt.trim() || "…"}</div>
              </div>
            </div>

            <div className="flex flex-col gap-1.5">
              <span className="text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{t("shareHeading")}</span>
              <div className="overflow-hidden rounded-lg border border-gray-200 dark:border-gray-800">
                <div className="flex aspect-[1200/630] items-end bg-gradient-to-br from-[#1e3a8a] via-[#2a5fd6] to-[#60a5fa] p-3 text-sm font-bold leading-tight text-white">
                  <span className="line-clamp-3">{shownTitle || title || "…"}</span>
                </div>
                <div className="px-2.5 py-2 text-xs">
                  <div className="truncate font-medium text-gray-900 dark:text-gray-100">{fullTitle}</div>
                  <div className="truncate text-gray-500 dark:text-gray-400">{excerpt.trim() || "…"}</div>
                  <div className="text-gray-500 dark:text-gray-400">ekariyerim.com</div>
                </div>
              </div>
            </div>

            <div className="flex flex-col gap-1.5">
              <span className="text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{t("checklistHeading")}</span>
            <p className="text-xs text-gray-500 dark:text-gray-400">{t("checklistIntro")}</p>
              <ul className="flex flex-col gap-1.5 text-[13px] text-gray-700 dark:text-gray-300">
              {checks.map((check) => {
                const source = SEO_CHECK_SOURCES[check.id];
                const label = t(`checks.${check.id}`, { value: check.value ?? 0 });
                return (
                  <li key={check.id} className="flex items-center gap-2">
                    <span aria-hidden="true" className={`h-2 w-2 shrink-0 rounded-full ${CHECK_DOT[check.status]}`} />
                    <span className="min-w-0 flex-1">{label}</span>
                    <a
                      href={source.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      className={`shrink-0 rounded-full px-1.5 py-px text-[10px] font-semibold uppercase tracking-wide underline-offset-2 hover:underline ${
                        source.basis === "google" ? "bg-accent-wash text-accent-ink" : "bg-muted-wash text-muted-ink"
                      }`}
                      aria-label={t("sourceLabel", { item: label })}
                    >
                      {t(`basis.${source.basis}`)}
                    </a>
                  </li>
                );
              })}
            </ul>
            </div>
          </div>
        </div>
      </Card>
    </div>
  );
}

/** One proposal under its field: what the model said, and "apply" to take it. */
function Proposal({ value, label, apply, onApply }: { value: string | null; label: string; apply: string; onApply: () => void }) {
  if (!value) return null;
  return (
    <div className="mt-1.5 flex items-start gap-2 rounded-md bg-accent-wash px-2.5 py-1.5 text-xs text-accent-ink">
      <span className="min-w-0 flex-1">
        <span className="block text-[10px] font-semibold uppercase tracking-wide opacity-80">{label}</span>
        <span className="break-words">{value}</span>
      </span>
      <button
        type="button"
        className="shrink-0 rounded-md border border-accent/40 bg-white px-2 py-0.5 font-medium hover:bg-accent/10 dark:bg-gray-950"
        onClick={onApply}
      >
        {apply}
      </button>
    </div>
  );
}

/** The thin bar under a field: green inside the guide, amber below it, red past it. */
function LengthMeter({ value, max, min }: { value: number; max: number; min: number }) {
  const over = value > max;
  const under = value > 0 && value < min;
  const colour = over ? "bg-crit" : under ? "bg-warn" : "bg-good";
  return (
    <div className="mt-1.5 flex items-center gap-2 text-[11px] tabular-nums text-gray-500 dark:text-gray-400">
      <span className="h-1 flex-1 overflow-hidden rounded-full bg-gray-200 dark:bg-gray-800">
        <span
          className={`block h-full transition-[width] motion-reduce:transition-none ${colour}`}
          style={{ width: `${Math.min(100, (value / max) * 100)}%` }}
        />
      </span>
      <span className={over ? "text-crit-ink" : undefined}>
        {value}/{max}
      </span>
    </div>
  );
}
