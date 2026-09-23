"use client";

import { useEffect, useRef, useState, type CSSProperties, type PointerEvent } from "react";
import { useTranslations } from "next-intl";
import type { NotificationFeedItemResponse } from "@/types/api";
import { StatusBadge } from "@/components/applications/StatusBadge";
import { ContributionNotificationText, HelpfulIcon } from "@/components/notifications/ContributionNotificationText";
import { Link } from "@/i18n/navigation";
import { contributionTarget, formatNotificationTime } from "@/lib/notifications/feed";
import { isHorizontalIntent, resolveSwipeGesture, swipeStyle } from "@/lib/notifications/swipe";

interface NotificationCardProps {
  item: NotificationFeedItemResponse;
  locale: string;
  reverted: boolean;
  reverting: boolean;
  onRevert: (id: string) => void;
  onDismiss: (id: string) => void;
}

/** How long the card takes to slide off once released past the threshold; matches the CSS below. */
const LEAVE_MS = 200;

/**
 * One row of the notifications list. Drag it sideways — mouse or finger, pointer events cover both
 * — past a third of its width and it slides off and is cleared; let go earlier and it springs back.
 * The ✕ does the same for a keyboard, a screen reader, or anyone who would rather click. The card
 * only tracks the pointer once the movement is clearly horizontal (`touch-action: pan-y` leaves
 * vertical scrolling to the browser), and never from a button or link inside it.
 */
export function NotificationCard({ item, locale, reverted, reverting, onRevert, onDismiss }: NotificationCardProps) {
  const t = useTranslations("notifications");
  const n = item;
  const cardRef = useRef<HTMLLIElement>(null);
  const gesture = useRef<{ pointerId: number; startX: number; startY: number; committed: boolean } | null>(null);
  const leaveTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  // Width travels with the offset so render never reads the DOM: it is measured in the handler
  // that commits the gesture, on the element the pointer is actually over.
  const [drag, setDrag] = useState<{ dx: number; width: number } | null>(null);
  const [leaving, setLeaving] = useState<"left" | "right" | null>(null);

  useEffect(() => () => {
    if (leaveTimer.current) clearTimeout(leaveTimer.current);
  }, []);

  const leave = (direction: "left" | "right") => {
    setDrag(null);
    setLeaving(direction);
    // A timer rather than transitionend: with reduced motion there is no transition to end.
    leaveTimer.current = setTimeout(() => onDismiss(n.id), LEAVE_MS);
  };

  const handlePointerDown = (event: PointerEvent<HTMLLIElement>) => {
    if (leaving || event.button !== 0) return;
    if ((event.target as HTMLElement).closest("button, a")) return;
    gesture.current = { pointerId: event.pointerId, startX: event.clientX, startY: event.clientY, committed: false };
  };

  const handlePointerMove = (event: PointerEvent<HTMLLIElement>) => {
    const g = gesture.current;
    if (!g || g.pointerId !== event.pointerId) return;
    const moveX = event.clientX - g.startX;
    const moveY = event.clientY - g.startY;
    if (!g.committed) {
      if (!isHorizontalIntent(moveX, moveY)) {
        // Vertical first: this is a scroll. Stop watching so a later sideways wobble cannot turn
        // it into a swipe halfway down the page.
        if (Math.abs(moveY) > Math.abs(moveX)) gesture.current = null;
        return;
      }
      g.committed = true;
      cardRef.current?.setPointerCapture(event.pointerId);
    }
    setDrag({ dx: moveX, width: cardRef.current?.offsetWidth ?? 0 });
  };

  const release = (event: PointerEvent<HTMLLIElement>, cancelled: boolean) => {
    const g = gesture.current;
    if (!g || g.pointerId !== event.pointerId) return;
    gesture.current = null;
    if (!g.committed) return;
    if (cardRef.current?.hasPointerCapture(event.pointerId)) cardRef.current.releasePointerCapture(event.pointerId);
    const moveX = event.clientX - g.startX;
    const outcome = cancelled
      ? "reset"
      : resolveSwipeGesture({ dx: moveX, dy: event.clientY - g.startY, width: cardRef.current?.offsetWidth ?? 0 });
    if (outcome === "dismiss") {
      leave(moveX > 0 ? "right" : "left");
    } else {
      setDrag(null);
    }
  };

  const c = item.kind === "Contribution" ? item.contribution : null;
  const e = item.kind === "Email" ? item.email : null;
  const target = c ? contributionTarget(c) : null;

  const dragging = drag !== null;
  const style: CSSProperties = drag
    ? swipeStyle(drag.dx, drag.width)
    : leaving
      ? { transform: `translateX(${leaving === "right" ? "110%" : "-110%"})`, opacity: 0 }
      : {};

  return (
    <li
      ref={cardRef}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={(event) => release(event, false)}
      onPointerCancel={(event) => release(event, true)}
      style={style}
      // touch-pan-y, not touch-none: the browser keeps vertical scrolling for itself and only a
      // horizontal movement reaches the handlers above.
      className={`touch-pan-y rounded-lg border border-gray-200 bg-white p-4 shadow-sm dark:border-gray-800 dark:bg-gray-900 ${
        dragging ? "select-none" : "transition-[transform,opacity] duration-200 ease-out motion-reduce:transition-none"
      }`}
      aria-busy={leaving !== null}
    >
      {c ? (
        <>
      <div className="flex items-start gap-3">
        <span className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-emerald-50 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-300">
          <HelpfulIcon />
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-sm leading-relaxed text-gray-900 dark:text-gray-100">
            <ContributionNotificationText contribution={c} />
          </p>
          <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{formatNotificationTime(item.occurredAt, locale)}</p>
          {target && (
            <Link
              href={target.href}
              locale={target.locale}
              className="mt-2 inline-block text-xs font-medium text-blue-600 hover:underline dark:text-blue-400"
            >
              {t(`contributionLink.${c.type}`)}
            </Link>
          )}
        </div>
        <button
          type="button"
          onClick={() => leave("right")}
          aria-label={t("dismiss")}
          title={t("dismiss")}
          className="-mr-1 rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 dark:hover:bg-gray-800 dark:hover:text-gray-200"
        >
          <svg aria-hidden="true" className="h-4 w-4" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
            <path d="M5 5l10 10M15 5L5 15" />
          </svg>
        </button>
      </div>
        </>
      ) : e ? (
        <>
      <div className="mb-2 flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="font-medium text-gray-900 dark:text-gray-100">
            {e.companyName} — {e.jobTitle}
          </p>
          <p className="text-xs text-gray-500 dark:text-gray-400">{formatNotificationTime(e.createdAt, locale)}</p>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          {e.status && <StatusBadge status={e.status} />}
          <button
            type="button"
            onClick={() => leave("right")}
            aria-label={t("dismiss")}
            title={t("dismiss")}
            className="-mr-1 rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 dark:hover:bg-gray-800 dark:hover:text-gray-200"
          >
            <svg aria-hidden="true" className="h-4 w-4" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
              <path d="M5 5l10 10M15 5L5 15" />
            </svg>
          </button>
        </div>
      </div>
      <div className="mb-2 flex flex-wrap gap-2">
        <span
          className={
            e.wasAutoApplied
              ? "inline-block rounded-full bg-blue-100 px-2.5 py-0.5 text-xs font-medium text-blue-700 dark:bg-blue-900/40 dark:text-blue-300"
              : "inline-block rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium text-gray-600 dark:bg-gray-800 dark:text-gray-400"
          }
        >
          {e.wasAutoApplied ? t("autoApplied") : t("confirmed")}
        </span>
        {e.isNewApplicationSuggestion && (
          <span className="inline-block rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-medium text-amber-700 dark:bg-amber-900/40 dark:text-amber-300">
            {t("newJobBadge")}
          </span>
        )}
      </div>
      {e.wasAutoApplied && (
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5">
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {t("confidence")}: {Math.round(e.confidenceScore * 100)}%
            {e.matchType && ` · ${t(`matchType.${e.matchType}`)}`}
          </p>
          {reverted ? (
            <span className="text-xs font-medium text-gray-600 dark:text-gray-400">{t("reverted")}</span>
          ) : (
            <button
              type="button"
              onClick={() => onRevert(n.id)}
              disabled={reverting}
              className="rounded text-xs font-medium text-blue-600 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 disabled:opacity-50 dark:text-blue-400"
            >
              {reverting ? t("reverting") : t("revert")}
            </button>
          )}
        </div>
      )}
        </>
      ) : null}
    </li>
  );
}
