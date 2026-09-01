"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { Link, usePathname } from "@/i18n/navigation";

const TOPICS = [
  { href: "/help", key: "overview" },
  { href: "/help/getting-started", key: "gettingStarted" },
  { href: "/help/dashboard", key: "dashboard" },
  { href: "/help/tracked-jobs", key: "trackedJobs" },
  { href: "/help/applications", key: "applications" },
  { href: "/help/suggestions", key: "suggestions" },
  { href: "/help/import", key: "import" },
  { href: "/help/settings", key: "settings" },
  { href: "/help/chrome-extension", key: "chromeExtension" },
  { href: "/help/faq", key: "faq" },
] as const;

export function HelpSidebar() {
  const t = useTranslations("help.sidebar");
  const pathname = usePathname();
  const [open, setOpen] = useState(false);

  const links = (onNavigate?: () => void) => (
    <nav className="flex flex-col gap-1 text-sm">
      {TOPICS.map((topic) => {
        const active = pathname === topic.href;
        return (
          <Link
            key={topic.href}
            href={topic.href}
            onClick={onNavigate}
            className={`rounded-md px-3 py-2 transition-colors ${
              active
                ? "bg-blue-50 font-medium text-blue-700 dark:bg-blue-950/50 dark:text-blue-300"
                : "text-gray-600 hover:bg-gray-100 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-gray-800 dark:hover:text-gray-100"
            }`}
          >
            {t(topic.key)}
          </Link>
        );
      })}
    </nav>
  );

  return (
    <>
      <aside className="hidden w-56 shrink-0 md:block">{links()}</aside>

      <div className="md:hidden">
        <button
          type="button"
          onClick={() => setOpen((value) => !value)}
          aria-expanded={open}
          aria-controls="help-mobile-menu"
          className="flex w-full items-center justify-between rounded-md border border-gray-200 px-3 py-2 text-sm font-medium text-gray-700 dark:border-gray-800 dark:text-gray-300"
        >
          {TOPICS.find((topic) => topic.href === pathname)?.key ? t(TOPICS.find((topic) => topic.href === pathname)!.key) : t("overview")}
          <svg
            viewBox="0 0 24 24"
            className={`h-4 w-4 transition-transform ${open ? "rotate-180" : ""}`}
            fill="none"
            stroke="currentColor"
            strokeWidth={2}
            aria-hidden="true"
          >
            <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
          </svg>
        </button>
        {open && (
          <div id="help-mobile-menu" className="mt-2 rounded-md border border-gray-200 p-2 dark:border-gray-800">
            {links(() => setOpen(false))}
          </div>
        )}
      </div>
    </>
  );
}
