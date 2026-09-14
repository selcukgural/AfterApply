"use client";

import { useEffect, useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { Link, usePathname } from "@/i18n/navigation";
import { isActivePath, navLinkClassName } from "@/components/layout/navLink";

/** The things that work without an account, kept reachable after you have one. */
export const TOOL_LINKS = [
  { href: "/cv-tarama", key: "cvScan" },
  { href: "/benchmark", key: "benchmark" },
  { href: "/guide", key: "guide" },
] as const;

/**
 * "Araçlar ▾" in the signed-in navbar: the account-free tools behind one trigger (2026-09-14,
 * option D3 on the header canvas). They were a group inside the user menu before, which kept them
 * reachable but hid them behind the avatar; a nav-level group puts them next to the app's own
 * pages without adding three items to a row that is already full at 1024px. The trigger is
 * underlined like a section link while any tool page is open, so the row still answers "where am I".
 */
export function ToolsMenu() {
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

  const active = TOOL_LINKS.some((link) => isActivePath(pathname, link.href));

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        aria-haspopup="menu"
        className={navLinkClassName("underline", active, "flex items-center gap-1 whitespace-nowrap pb-0.5")}
      >
        {t("toolsMenu")}
        <svg viewBox="0 0 24 24" className="h-4 w-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
          <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
        </svg>
      </button>

      {open && (
        <div
          role="menu"
          className="absolute left-0 z-50 mt-2 w-52 rounded-md border border-gray-200 bg-white py-1 shadow-lg dark:border-gray-800 dark:bg-gray-900"
        >
          {TOOL_LINKS.map((link) => (
            <Link
              key={link.href}
              role="menuitem"
              href={link.href}
              onClick={() => setOpen(false)}
              aria-current={isActivePath(pathname, link.href) ? "page" : undefined}
              className={`block px-4 py-2 text-sm ${
                isActivePath(pathname, link.href)
                  ? "font-medium text-gray-900 dark:text-gray-100"
                  : "text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800"
              }`}
            >
              {t(link.key)}
            </Link>
          ))}
          <div className="my-1 border-t border-gray-100 dark:border-gray-800" />
          <p className="px-4 pt-1 pb-2 text-xs text-gray-500 dark:text-gray-400">{t("toolsNote")}</p>
        </div>
      )}
    </div>
  );
}
