"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { companyReviewsApi } from "@/lib/api/companyReviews";
import { clampPage } from "@/lib/dashboard/reminders";
import { buttonClassName } from "@/components/ui/Button";
import { Pagination } from "@/components/applications/Pagination";
import { MyReviewCard } from "@/components/contributions/MyReviewCard";
import { MySalaryCard } from "@/components/contributions/MySalaryCard";
import { MyExperienceCard } from "@/components/contributions/MyExperienceCard";
import { MyBlogCommentCard } from "@/components/contributions/MyBlogCommentCard";
import type { ContributionFilter, MyBlogComment } from "@/types/api";

/**
 * "My contributions" (2026-09-18): the author's reviews, salary entries and candidate experiences
 * as one newest-first list, ten per page, at the address "My reviews" always had. The three kinds
 * used to be three pages; /my-salaries and /my-experiences now redirect here. A quota line and a
 * call to action per kind, shown only while the server reports that kind's quota — a feature
 * that is off reports none.
 */
export default function MyContributionsPage() {
  const t = useTranslations("companyReviews.mine");
  const tSalaries = useTranslations("companySalaries.mine");
  const tExperiences = useTranslations("candidateExperiences.mine");
  const tComment = useTranslations("companyReviews.mine.blogComment");
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  // The chips (2026-09-20): everything, the blog comments alone, or the company contributions.
  const [filter, setFilter] = useState<ContributionFilter | null>(null);

  const { data, isLoading, error } = useQuery({
    queryKey: ["contributions", "mine", page, filter],
    queryFn: () => companyReviewsApi.listMyContributions(page, filter ?? undefined),
  });

  const onCommentEdited = (edited: MyBlogComment) =>
    queryClient.setQueryData(["contributions", "mine", page, filter], (current: typeof data) =>
      current
        ? { ...current, items: current.items.map((item) => (item.blogComment?.id === edited.id ? { ...item, blogComment: edited } : item)) }
        : current,
    );

  const chip = (value: ContributionFilter | null, label: string) => (
    <button
      type="button"
      aria-pressed={filter === value}
      onClick={() => {
        setFilter(value);
        setPage(1);
      }}
      className={`inline-flex h-8 items-center rounded-full border px-3 text-[13px] font-medium ${
        filter === value
          ? "border-accent bg-accent-wash text-accent-ink"
          : "border-gray-300 text-gray-700 hover:border-gray-400 dark:border-gray-700 dark:text-gray-300 dark:hover:border-gray-500"
      }`}
    >
      {label}
    </button>
  );

  // The per-kind "mine" lists feed the profile card and the edit forms; they change with us.
  const onDeleted = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["contributions", "mine"] }),
      queryClient.invalidateQueries({ queryKey: ["companyReviews", "mine"] }),
      queryClient.invalidateQueries({ queryKey: ["companySalaries", "mine"] }),
      queryClient.invalidateQueries({ queryKey: ["candidateExperiences", "mine"] }),
    ]);
    if (data) {
      setPage((current) => clampPage(current, data.totalCount - 1, data.pageSize));
    }
  };

  const quotaLeft = (quota: { used: number; limit: number } | null) => (quota ? Math.max(0, quota.limit - quota.used) : 0);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
          <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
          {data && (
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              {[
                t("quotaLine", { used: data.reviewQuota.used, limit: data.reviewQuota.limit }),
                data.salaryQuota ? tSalaries("quotaLine", { used: data.salaryQuota.used, limit: data.salaryQuota.limit }) : null,
                data.experienceQuota ? tExperiences("quotaLine", { used: data.experienceQuota.used, limit: data.experienceQuota.limit }) : null,
              ]
                .filter(Boolean)
                .join(" · ")}
            </p>
          )}
        </div>
        {data && (
          <div className="flex flex-col gap-2 sm:items-end">
            {quotaLeft(data.reviewQuota) > 0 && (
              <Link href="/contribute?tab=review" className={buttonClassName("primary")}>
                {t("write")}
              </Link>
            )}
            {quotaLeft(data.salaryQuota) > 0 && (
              <Link href="/contribute?tab=salary" className={buttonClassName("outline")}>
                {tSalaries("share")}
              </Link>
            )}
            {quotaLeft(data.experienceQuota) > 0 && (
              <Link href="/contribute?tab=experience" className={buttonClassName("outline")}>
                {tExperiences("share")}
              </Link>
            )}
          </div>
        )}
      </div>

      <div className="flex flex-wrap gap-2">
        {chip(null, tComment("filterAll"))}
        {chip("BlogComments", tComment("filterComments"))}
        {chip("Company", tComment("filterCompany"))}
      </div>

      {isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
      {error && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {t("error")}
        </p>
      )}

      {data && data.items.length === 0 && (
        <div className="rounded-xl border border-dashed border-gray-300 p-8 text-center text-sm text-gray-600 dark:border-gray-700 dark:text-gray-400">
          <p>{t("empty")}</p>
          <Link href="/companies" className="mt-3 inline-block text-accent-ink underline-offset-2 hover:underline">
            {t("browse")}
          </Link>
        </div>
      )}

      {data && data.items.length > 0 && (
        <ul className="flex flex-col gap-3">
          {data.items.map((item) => {
            if (item.kind === "Salary" && item.salary) {
              return <MySalaryCard key={`salary-${item.salary.id}`} entry={item.salary} onDeleted={onDeleted} />;
            }
            if (item.kind === "Experience" && item.experience) {
              return <MyExperienceCard key={`experience-${item.experience.id}`} entry={item.experience} onDeleted={onDeleted} />;
            }
            if (item.kind === "BlogComment" && item.blogComment) {
              return <MyBlogCommentCard key={`comment-${item.blogComment.id}`} comment={item.blogComment} onEdited={onCommentEdited} />;
            }
            return item.review ? <MyReviewCard key={`review-${item.review.id}`} review={item.review} onDeleted={onDeleted} /> : null;
          })}
        </ul>
      )}

      {data && <p className="text-xs text-gray-500 dark:text-gray-400">{t("footnote")}</p>}

      {data && <Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} unit="contributions" onPageChange={setPage} />}
    </div>
  );
}
