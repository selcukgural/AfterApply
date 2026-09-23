"use client";

import { type ReactNode, useEffect, useRef, useState } from "react";
import { Link, usePathname } from "@/i18n/navigation";
import { isActivePath, navLinkClassName } from "@/components/layout/navLink";
import { isNavItemActive } from "@/components/layout/navGroups";

export interface NavMenuItem {
  href: string;
  label: ReactNode;
  /** A rule between this item and the one before it. */
  dividerBefore?: boolean;
}

interface NavMenuProps {
  /** The trigger's text. */
  label: string;
  items: NavMenuItem[];
  /** Left in the DOM for the mobile menu's flat list; the dropdown itself is desktop-only. */
  className?: string;
  /** Replaces the dropdown's default width and padding — the signed-out Tools menu's items carry a
   *  line of description and need the room. */
  menuClassName?: string;
}

/**
 * One group in the signed-in navbar: a trigger that is underlined like a section link while any
 * page beneath one of its items is open, and a menu of links under it. Grew out of the 2026-09-14
 * "Tools" menu when the row reached eleven items (2026-09-15, option D3 on the header canvas);
 * since 2026-09-17 every group in the row is one of these, fed from navGroups.ts.
 */
export function NavMenu({ label, items, className = "", menuClassName = "w-56 py-1" }: NavMenuProps) {
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

  // The trigger is underlined while any page beneath one of its items is open. A contribute link
  // counts by its path alone, so the Companies group lights up on /contribute even though neither
  // of its two query-string twins can be "current" (isNavItemActive).
  const active = items.some((item) => isActivePath(pathname, item.href.split("?")[0]));

  return (
    <div ref={containerRef} className={`relative ${className}`}>
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        aria-haspopup="menu"
        className={navLinkClassName("underline", active, "flex items-center gap-1 whitespace-nowrap pb-0.5")}
      >
        {label}
        <svg viewBox="0 0 24 24" className="h-4 w-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
          <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
        </svg>
      </button>

      {open && (
        <div
          role="menu"
          className={`absolute left-0 z-50 mt-2 rounded-md border border-gray-200 bg-white shadow-lg dark:border-gray-800 dark:bg-gray-900 ${menuClassName}`}
        >
          {items.map((item) => (
            <div key={item.href}>
              {item.dividerBefore && <div className="my-1 border-t border-gray-100 dark:border-gray-800" />}
              <Link
                role="menuitem"
                href={item.href}
                onClick={() => setOpen(false)}
                aria-current={isNavItemActive(pathname, item.href) ? "page" : undefined}
                className={`flex items-center justify-between gap-3 px-4 py-2 text-sm ${
                  isNavItemActive(pathname, item.href)
                    ? "font-medium text-gray-900 dark:text-gray-100"
                    : "text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800"
                }`}
              >
                {item.label}
              </Link>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
