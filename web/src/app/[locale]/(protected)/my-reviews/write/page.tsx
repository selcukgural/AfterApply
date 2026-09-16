"use client";

import { Suspense, useEffect } from "react";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { useRouter } from "@/i18n/navigation";
import { contributeHref } from "@/lib/contribute/contributeState";

/**
 * The address "write a review" lived at until 2026-09-16, when the form moved onto the contribute
 * page next to the salary form. Kept as a redirect: it is in the sign-in return allowlist, in
 * help articles, and in whatever bookmarks exist. The company slug rides along.
 */
export default function WriteReviewPage() {
  return (
    <Suspense fallback={null}>
      <WriteReviewRedirect />
    </Suspense>
  );
}

function WriteReviewRedirect() {
  const t = useTranslations("common");
  const router = useRouter();
  const slug = useSearchParams().get("company");

  useEffect(() => {
    router.replace(contributeHref("review", slug));
  }, [router, slug]);

  return <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>;
}
