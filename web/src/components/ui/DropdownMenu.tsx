"use client";

import { useEffect, useId, useRef, useState, type ReactNode } from "react";

interface DropdownMenuProps {
  /** The trigger's content; the trigger itself is a real button owned here. */
  label: ReactNode;
  triggerClassName: string;
  /** Rendered only while open; `close` is for items that act and should fold the menu. */
  children: (close: () => void) => ReactNode;
  /** A heading inside the panel, also its accessible name. */
  title?: string;
  align?: "left" | "right";
  disabled?: boolean;
}

/**
 * A button that folds out a small panel of actions — the reminders card's "Snooze" and "Add to
 * calendar". Closes on an outside press and on Escape, like the header's menus; the panel is plain
 * buttons and links, so keyboard order is document order.
 */
export function DropdownMenu({ label, triggerClassName, children, title, align = "right", disabled }: DropdownMenuProps) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const panelId = useId();

  useEffect(() => {
    if (!open) return;
    const handlePointerDown = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) setOpen(false);
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

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        aria-controls={panelId}
        disabled={disabled}
        className={triggerClassName}
      >
        {label}
      </button>
      {open && (
        <div
          id={panelId}
          role="group"
          aria-label={title}
          className={`absolute ${align === "right" ? "right-0" : "left-0"} z-30 mt-1.5 flex w-56 flex-col rounded-lg border border-gray-200 bg-white p-1.5 shadow-lg dark:border-gray-800 dark:bg-gray-900`}
        >
          {title ? <span className="px-2 py-1.5 text-[11px] text-gray-500 dark:text-gray-400">{title}</span> : null}
          {children(() => setOpen(false))}
        </div>
      )}
    </div>
  );
}

/** One row of a DropdownMenu panel, as a button. */
export const dropdownItemClassName =
  "flex w-full items-center justify-between gap-3 rounded-md px-2 py-2 text-left text-sm text-gray-900 hover:bg-gray-100 disabled:opacity-50 dark:text-gray-100 dark:hover:bg-gray-800";
