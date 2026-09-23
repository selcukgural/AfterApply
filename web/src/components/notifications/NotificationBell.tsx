"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import { useLocale, useTranslations } from "next-intl";
import { useQueryClient } from "@tanstack/react-query";
import { Link } from "@/i18n/navigation";
import { StatusBadge } from "@/components/applications/StatusBadge";
import { ContributionNotificationText, HelpfulIcon, MailIcon } from "@/components/notifications/ContributionNotificationText";
import { notificationCountQueryKey, useNotificationCount } from "@/hooks/useNotificationCount";
import { notificationsQueryKey, useNotifications } from "@/hooks/useNotifications";
import { NOTIFICATION_PANEL_SIZE, notificationsApi } from "@/lib/api/notifications";
import { contributionTarget, emailTarget, formatNotificationTime } from "@/lib/notifications/feed";
import type { NotificationFeedItemResponse } from "@/types/api";

interface NotificationBellProps {
  /** The badge renderer NavBar already uses for its other icon, so the two counts look alike. */
  badge: (count: number | undefined) => ReactNode;
  icon: ReactNode;
}

/**
 * The bell as a panel (canvas variant A, DECISIONS.md 2026-09-23): the latest rows, each with its
 * date and time, the settings one click away, and the full page behind "see all". Opening it is
 * reading it: the rows are marked read once the panel has shown them, and the badge clears. The
 * dots stay on the rows for as long as this panel stays open, so what was new is still visible.
 */
export function NotificationBell({ badge, icon }: NotificationBellProps) {
  const t = useTranslations("notifications");
  const locale = useLocale();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const markedFor = useRef(false);
  const { data: count } = useNotificationCount();
  const { data, error } = useNotifications(1, NOTIFICATION_PANEL_SIZE, open);

  useEffect(() => {
    if (!open) return;

    const handlePointerDown = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) close();
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") close();
    };

    document.addEventListener("mousedown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
    // close only touches state and the query client, both stable.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  useEffect(() => {
    if (!open || !data || markedFor.current) return;
    markedFor.current = true;
    if (!data.items.some((item) => !item.isRead)) return;
    notificationsApi
      .markAllRead()
      .then(() => queryClient.invalidateQueries({ queryKey: notificationCountQueryKey }))
      .catch(() => {
        // The badge staying lit a minute longer is the whole cost; nothing to tell the user.
      });
  }, [open, data, queryClient]);

  function close() {
    setOpen(false);
    markedFor.current = false;
    // Next time the panel opens it shows the rows as read, and anything that arrived meanwhile.
    void queryClient.invalidateQueries({ queryKey: notificationsQueryKey });
  }

  const label = count ? `${t("title")} (${count})` : t("title");
  const items = data?.items ?? [];

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        onClick={() => (open ? close() : setOpen(true))}
        aria-expanded={open}
        aria-haspopup="dialog"
        aria-label={label}
        title={t("title")}
        className={`relative flex h-9 w-9 items-center justify-center rounded-md transition-colors ${
          open
            ? "bg-gray-100 text-gray-900 dark:bg-gray-800 dark:text-gray-100"
            : "text-gray-600 hover:bg-gray-100 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-gray-800 dark:hover:text-gray-100"
        }`}
      >
        {icon}
        {count ? <span className="absolute -top-1 -right-1">{badge(count)}</span> : null}
      </button>

      {open && (
        <section
          role="dialog"
          aria-label={t("title")}
          className="absolute right-0 z-50 mt-2 flex w-[25rem] max-w-[calc(100vw-2rem)] flex-col overflow-hidden rounded-xl border border-gray-200 bg-white shadow-lg dark:border-gray-800 dark:bg-gray-900"
        >
          <header className="flex items-center justify-between border-b border-gray-100 px-4 py-3 dark:border-gray-800">
            <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
            <Link
              href="/settings#notifications"
              onClick={close}
              aria-label={t("settingsLink")}
              title={t("settingsLink")}
              className="flex h-8 w-8 items-center justify-center rounded-md text-gray-500 hover:bg-gray-100 hover:text-gray-800 dark:text-gray-400 dark:hover:bg-gray-800 dark:hover:text-gray-100"
            >
              <svg viewBox="0 0 24 24" className="h-[1.1rem] w-[1.1rem]" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <circle cx="12" cy="12" r="3" />
                <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
              </svg>
            </Link>
          </header>

          {error ? (
            <p className="px-4 py-6 text-sm text-red-600 dark:text-red-400">{t("loadError")}</p>
          ) : !data ? (
            <p className="px-4 py-6 text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>
          ) : items.length === 0 ? (
            <p className="px-4 py-6 text-sm text-gray-500 dark:text-gray-400">{t("panelEmpty")}</p>
          ) : (
            <ul className="max-h-[28rem] overflow-y-auto">
              {items.map((item) => (
                <PanelRow key={item.id} item={item} locale={locale} onNavigate={close} />
              ))}
            </ul>
          )}

          <Link
            href="/notifications"
            onClick={close}
            className="border-t border-gray-100 px-4 py-3 text-center text-sm font-medium text-blue-600 hover:bg-gray-50 dark:border-gray-800 dark:text-blue-400 dark:hover:bg-gray-800"
          >
            {t("seeAll")}
          </Link>
        </section>
      )}
    </div>
  );
}

function PanelRow({ item, locale, onNavigate }: { item: NotificationFeedItemResponse; locale: string; onNavigate: () => void }) {
  const t = useTranslations("notifications");
  const c = item.kind === "Contribution" ? item.contribution : null;
  const e = item.kind === "Email" ? item.email : null;
  const contribution = c ? contributionTarget(c) : null;
  const href = contribution?.href ?? emailTarget(item) ?? "/notifications";

  return (
    <li className="border-b border-gray-100 last:border-b-0 dark:border-gray-800">
      <Link
        href={href}
        locale={contribution?.locale}
        onClick={onNavigate}
        className={`flex items-start gap-3 px-4 py-3 hover:bg-gray-50 dark:hover:bg-gray-800 ${item.isRead ? "" : "bg-blue-50/60 dark:bg-blue-950/30"}`}
      >
        <span
          className={`mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full ${
            c ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-300" : "bg-blue-50 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300"
          }`}
        >
          {c ? <HelpfulIcon /> : <MailIcon />}
        </span>
        <span className="flex min-w-0 flex-1 flex-col gap-1">
          <span className="text-sm leading-snug text-gray-900 dark:text-gray-100">
            {c ? (
              <ContributionNotificationText contribution={c} />
            ) : e ? (
              <>
                <strong className="font-semibold">{e.companyName}</strong> — {e.jobTitle}
              </>
            ) : null}
          </span>
          <span className="flex flex-wrap items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
            {formatNotificationTime(item.occurredAt, locale)}
            {e?.status && <StatusBadge status={e.status} />}
          </span>
        </span>
        {!item.isRead && <span className="mt-2 h-2 w-2 shrink-0 rounded-full bg-blue-600" aria-label={t("unread")} />}
      </Link>
    </li>
  );
}
