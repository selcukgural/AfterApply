"use client";

import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import type { BlogGuideSettings, BlogLanguage } from "@/types/api";
import { adminBlogApi } from "@/lib/api/blog";
import { Card } from "@/components/dashboard/Card";
import { FormField } from "@/components/ui/FormField";
import { Select } from "@/components/ui/Select";
import { MAX_RELATED_GUIDES, setRelatedAt } from "@/lib/blog/guideSettings";

/**
 * A guide's own two settings (2026-09-26, the canvas's "Rehber ayarları" card): up to two other
 * guides shown under it as related, and whether its sign-up box is hidden. The picker offers the
 * guides the caller can see in the post's language — a draft of their own too, which the page
 * links only once it is published (the API decides that at read time).
 */
export function GuideOptionsCard({
  postId,
  language,
  value,
  onChange,
}: {
  postId: string | null;
  language: BlogLanguage;
  value: BlogGuideSettings;
  onChange: (value: BlogGuideSettings) => void;
}) {
  const t = useTranslations("adminGuide.options");
  const tBlog = useTranslations("adminBlog");

  const candidates = useQuery({
    queryKey: ["admin", "blog", "related-candidates", language],
    queryFn: () => adminBlogApi.list({ lang: language, kind: "Guide" }),
  });
  const options = (candidates.data?.items ?? []).filter((item) => item.id !== postId);

  return (
    <Card className="flex flex-col gap-4">
      <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>

      <div className="flex flex-col gap-2">
        {Array.from({ length: MAX_RELATED_GUIDES }, (_, index) => (
          <FormField key={index} label={t("relatedLabel", { n: index + 1 })} htmlFor={`guide-related-${index}`}>
            <Select
              id={`guide-related-${index}`}
              value={value.relatedPostIds[index] ?? ""}
              // The second slot opens once the first is filled, so the list never has a gap.
              disabled={index > 0 && !value.relatedPostIds[index - 1]}
              onChange={(e) => onChange({ ...value, relatedPostIds: setRelatedAt(value.relatedPostIds, index, e.target.value || null) })}
            >
              <option value="">{t("relatedNone")}</option>
              {options
                // A guide already picked in the other slot is not offered twice.
                .filter((item) => item.id === value.relatedPostIds[index] || !value.relatedPostIds.includes(item.id))
                .map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.title || tBlog("untitled")}
                    {item.status === "Published" ? "" : ` (${tBlog(`status.${item.status}`)})`}
                  </option>
                ))}
            </Select>
          </FormField>
        ))}
        <p className="text-xs text-gray-500 dark:text-gray-400">{t("relatedHint")}</p>
      </div>

      <div className="flex items-start gap-2">
        <input
          id="guide-hide-cta"
          type="checkbox"
          checked={value.hideRegisterCta}
          onChange={(e) => onChange({ ...value, hideRegisterCta: e.target.checked })}
          className="mt-0.5 h-4 w-4 rounded border-gray-300 text-accent focus:ring-accent dark:border-gray-700"
        />
        <label htmlFor="guide-hide-cta" className="flex flex-col gap-0.5">
          <span className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("hideCta")}</span>
          <span className="text-xs text-gray-500 dark:text-gray-400">{t("hideCtaHint")}</span>
        </label>
      </div>
    </Card>
  );
}
