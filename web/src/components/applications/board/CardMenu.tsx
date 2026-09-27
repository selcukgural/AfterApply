"use client";

import { type CSSProperties, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { BoardCardResponse } from "@/types/api";

interface CardMenuProps {
  card: BoardCardResponse;
  onOpen: () => void;
  onChangeStage: () => void;
  onMoveToTop: () => void;
  onRemove: () => void;
}

/**
 * The ⋯ menu on a card — every move the board offers by drag, reachable without one (keyboard,
 * screen readers, phones). A disclosure of plain buttons rather than an ARIA menu: four actions do
 * not need arrow-key roving, and buttons get Tab and Enter for free.
 */
export function CardMenu({
  card,
  onOpen,
  onChangeStage,
  onMoveToTop,
  onRemove,
}: CardMenuProps) {
  const t = useTranslations("applications.board.menu");
  // Where the open menu sits, in viewport coordinates; null while closed. The menu is portalled to
  // <body> with position: fixed, because inside the card it was clipped by its column's scroll box
  // whenever the card sat near the bottom.
  const [position, setPosition] = useState<CSSProperties | null>(null);
  const open = position !== null;
  const rootRef = useRef<HTMLDivElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const setOpen = (value: boolean) => {
    if (!value) {
      setPosition(null);
      return;
    }
    const rect = rootRef.current?.getBoundingClientRect();
    if (!rect) return;
    const right = Math.max(8, window.innerWidth - rect.right);
    // Open upwards when there is no room below for the menu (~15rem).
    setPosition(
      window.innerHeight - rect.bottom < 240
        ? { right, bottom: window.innerHeight - rect.top + 4 }
        : { right, top: rect.bottom + 4 },
    );
  };

  useEffect(() => {
    if (!open) return;
    const close = () => setPosition(null);
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node;
      if (
        !rootRef.current?.contains(target) &&
        !menuRef.current?.contains(target)
      )
        close();
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        close();
        buttonRef.current?.focus();
      }
    };
    // The portalled menu sits at the end of <body>, out of the tab order next to its button, so
    // opening it moves focus into it.
    menuRef.current?.querySelector<HTMLElement>("a, button")?.focus();
    // A fixed menu would float away from its card on scroll, so any scroll closes it.
    const onScroll = (event: Event) => {
      if (!menuRef.current?.contains(event.target as Node)) close();
    };
    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    window.addEventListener("scroll", onScroll, true);
    window.addEventListener("resize", close);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("scroll", onScroll, true);
      window.removeEventListener("resize", close);
    };
  }, [open]);

  const run = (action: () => void) => () => {
    setPosition(null);
    action();
  };

  const itemClass =
    "block min-h-11 w-full rounded-md px-3 py-2.5 text-left text-sm md:min-h-0 md:py-2 text-gray-800 hover:bg-gray-100 focus:bg-gray-100 focus:outline-none dark:text-gray-100 dark:hover:bg-gray-800 dark:focus:bg-gray-800";

  return (
    <div ref={rootRef} className="relative">
      <button
        ref={buttonRef}
        type="button"
        aria-haspopup="true"
        aria-expanded={open}
        // Title and company: many cards share a job title, and "actions for Backend Developer" does
        // not say which one to a screen reader.
        aria-label={t("label", {
          title: `${card.jobTitle}, ${card.companyName}`,
        })}
        onClick={() => {
          if (!open) onOpen();
          setOpen(!open);
        }}
        // Keeps the drag sensors from treating a tap on the menu as the start of a drag.
        onMouseDown={(event) => event.stopPropagation()}
        onTouchStart={(event) => event.stopPropagation()}
        className="-mr-2 -mt-1.5 inline-flex h-11 w-11 shrink-0 items-center md:-mr-1 md:mt-0 md:h-7 md:w-7 justify-center rounded-md text-gray-500 hover:bg-gray-100 hover:text-gray-800 focus:outline-none focus-visible:ring-2 focus-visible:ring-accent dark:text-gray-400 dark:hover:bg-gray-800 dark:hover:text-gray-100"
      >
        <svg
          width="16"
          height="16"
          viewBox="0 0 24 24"
          fill="currentColor"
          aria-hidden="true"
        >
          <circle cx="5" cy="12" r="1.8" />
          <circle cx="12" cy="12" r="1.8" />
          <circle cx="19" cy="12" r="1.8" />
        </svg>
      </button>
      {position &&
        createPortal(
          <div
            ref={menuRef}
            style={position}
            onMouseDown={(event) => event.stopPropagation()}
            onTouchStart={(event) => event.stopPropagation()}
            className="fixed z-50 w-56 rounded-lg border border-gray-200 bg-white p-1 shadow-lg dark:border-gray-700 dark:bg-gray-900"
          >
            <Link
              href={
                card.kind === "Application"
                  ? `/applications/${card.itemId}`
                  : "/tracked-jobs"
              }
              className={itemClass}
              onClick={() => setPosition(null)}
            >
              {card.kind === "Application" ? t("open") : t("openPosting")}
            </Link>
            <button
              type="button"
              className={itemClass}
              onClick={run(onChangeStage)}
            >
              {t("changeStage")}
            </button>
            <button
              type="button"
              className={itemClass}
              onClick={run(onMoveToTop)}
            >
              {t("toTop")}
            </button>
            <div className="my-1 h-px bg-gray-100 dark:bg-gray-800" />
            <button
              type="button"
              className={`${itemClass} text-crit-ink dark:text-crit-ink`}
              onClick={run(onRemove)}
            >
              {t("remove")}
            </button>
            <p className="px-3 pb-2 text-xs text-gray-500 dark:text-gray-400">
              {t("removeNote")}
            </p>
          </div>,
          document.body,
        )}
    </div>
  );
}
