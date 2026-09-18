"use client";

import { useEffect } from "react";
import { useTranslations } from "next-intl";
import { useRouter } from "@/i18n/navigation";

/**
 * The address this kind's own list lived at until 2026-09-18, when the three "mine" pages became
 * one contributions list at /my-reviews. Kept as a redirect: it is in help copy, in the profile
 * card's old links and in whatever bookmarks exist. The edit pages beneath it are untouched.
 */
export default function MyListRedirectPage() {
  const t = useTranslations("common");
  const router = useRouter();

  useEffect(() => {
    router.replace("/my-reviews");
  }, [router]);

  return <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>;
}
