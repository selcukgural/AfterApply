"use client";

import { useEffect, useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { Link, usePathname } from "@/i18n/navigation";
import { isActivePath, navLinkClassName } from "@/components/layout/navLink";

/** The directory, then the two things a signed-in person can contribute. Both contribute links
 *  open the same page on a different side. */
export const COMPANY_LINKS = [
  { href: "/companies", key: "companyDirectory" },
  { href: "/contribute?tab=review", key: "contributeReview" },
  { href: "/contribute?tab=salary", key: "contributeSalary" },
] as const;

/** Every page under which the "Companies" trigger reads as the current section. */
const COMPANY_SECTION_PATHS = ["/companies", "/contribute", "/my-reviews", "/my-salaries"] as const;

/**
 * "Şirketler ▾" in the signed-in navbar (2026-09-16, the ToolsMenu pattern): the directory and
 * the two contributions behind one trigger, so a review and a salary are one click from
 * anywhere without adding items to a row that is already full at 1024px. The trigger is
 * underlined while any company page — the directory, a company, the contribute page, the two
 * "mine" lists — is open.
 */
export function CompaniesMenu() {
  const t = useTranslations("nav");
  const pathname = usePathname();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    const handlePointerDown = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };

    document.addEventListener("mousedown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  const active = COMPANY_SECTION_PATHS.some((path) => isActivePath(pathname, path));
  // The two contribute links differ only in their query string, which the pathname does not
  // carry, so neither is ever "current" on its own — the underlined trigger says where you are.
  const isCurrent = (href: string) => !href.includes("?") && pathname === href;

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        aria-haspopup="menu"
        className={navLinkClassName("underline", active, "flex items-center gap-1 whitespace-nowrap pb-0.5")}
      >
        {t("companies")}
        <svg viewBox="0 0 24 24" className="h-4 w-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
          <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
        </svg>
      </button>

      {open && (
        <div
          role="menu"
          className="absolute left-0 z-50 mt-2 w-56 rounded-md border border-gray-200 bg-white py-1 shadow-lg dark:border-gray-800 dark:bg-gray-900"
        >
          {COMPANY_LINKS.map((link, index) => (
            <div key={link.href}>
              {index === 1 && <div className="my-1 border-t border-gray-100 dark:border-gray-800" />}
              <Link
                role="menuitem"
                href={link.href}
                onClick={() => setOpen(false)}
                aria-current={isCurrent(link.href) ? "page" : undefined}
                className={`block px-4 py-2 text-sm ${
                  isCurrent(link.href)
                    ? "font-medium text-gray-900 dark:text-gray-100"
                    : "text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800"
                }`}
              >
                {t(link.key)}
              </Link>
            </div>
          ))}
          <div className="my-1 border-t border-gray-100 dark:border-gray-800" />
          <p className="px-4 pt-1 pb-2 text-xs text-gray-500 dark:text-gray-400">{t("companiesNote")}</p>
        </div>
      )}
    </div>
  );
}
