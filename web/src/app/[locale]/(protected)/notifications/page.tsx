"use client";

import { useEffect, useRef, useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { useQueryClient } from "@tanstack/react-query";
import { NOTIFICATION_PAGE_SIZE, notificationsApi } from "@/lib/api/notifications";
import { ApiError } from "@/lib/api/httpClient";
import { Pagination } from "@/components/applications/Pagination";
import { NotificationCard } from "@/components/notifications/NotificationCard";
import { Button, buttonClassName } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { Link } from "@/i18n/navigation";
import { notificationCountQueryKey } from "@/hooks/useNotificationCount";
import { useDismissAllNotifications, useDismissNotification, useNotifications } from "@/hooks/useNotifications";
import { useGmailScanStatus } from "@/hooks/useGmailScanStatus";
import { clampPage } from "@/lib/dashboard/reminders";
import { resolveGmailEmptyState } from "@/lib/emailSuggestions/emptyState";

export default function NotificationsPage() {
  const t = useTranslations("notifications");
  const tCommon = useTranslations("common");
  const locale = useLocale();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const { data, error: loadError } = useNotifications(page);
  const dismiss = useDismissNotification();
  const dismissAll = useDismissAllNotifications();
  const gmailScanStatus = useGmailScanStatus();
  const [error, setError] = useState<string | null>(null);
  const [revertingId, setRevertingId] = useState<string | null>(null);
  const [revertedIds, setRevertedIds] = useState<string[]>([]);
  // Rows swiped away but not yet gone from the server's answer: hidden at once, so the card that
  // just slid off does not pop back while the refetch is in flight.
  const [hiddenIds, setHiddenIds] = useState<string[]>([]);
  const [confirmingClear, setConfirmingClear] = useState(false);
  const markedRead = useRef(false);

  // The user has now seen everything currently in the list — mark it all read in one bulk call
  // and clear the nav badge, once per visit rather than tracking per-row visibility.
  useEffect(() => {
    if (!data || markedRead.current) return;
    markedRead.current = true;
    notificationsApi
      .markAllRead()
      .then(() => queryClient.invalidateQueries({ queryKey: notificationCountQueryKey }))
      .catch(() => {
        // The badge staying lit a minute longer is the whole cost; nothing to tell the user.
      });
  }, [data, queryClient]);

  // Clearing the last row of the last page leaves the page empty: land on the new last page.
  // Adjusted during render rather than in an effect (react.dev, "adjusting state when a prop
  // changes"), same as the reminders card.
  if (data && page > clampPage(page, data.totalCount, NOTIFICATION_PAGE_SIZE)) {
    setPage(clampPage(page, data.totalCount, NOTIFICATION_PAGE_SIZE));
  }

  // Undoing removes the row's reason to offer the button again, but the notification itself stays:
  // "this was applied, then you took it back" is still the honest history of what happened.
  const handleRevert = async (suggestionId: string) => {
    setRevertingId(suggestionId);
    setError(null);
    try {
      await notificationsApi.revertAutoApply(suggestionId);
      setRevertedIds((ids) => [...ids, suggestionId]);
    } catch (err) {
      // 409 is not a failure the user caused — it means they already moved the application on
      // themselves, and saying so is more useful than a generic error.
      setError(err instanceof ApiError && err.status === 409 ? t("revertConflict") : t("revertError"));
    } finally {
      setRevertingId(null);
    }
  };

  const handleDismiss = (suggestionId: string) => {
    setError(null);
    setHiddenIds((ids) => [...ids, suggestionId]);
    dismiss.mutate(suggestionId, {
      onError: () => {
        setHiddenIds((ids) => ids.filter((id) => id !== suggestionId));
        setError(t("dismissError"));
      },
    });
  };

  const handleClearAll = () => {
    setError(null);
    dismissAll.mutate(undefined, {
      onSuccess: () => {
        setConfirmingClear(false);
        setHiddenIds([]);
        setPage(1);
      },
      onError: () => setError(t("clearAllError")),
    });
  };

  const totalCount = data?.totalCount ?? 0;
  const visible = data?.items.filter((n) => !hiddenIds.includes(n.id)) ?? [];
  const loadErrorText = loadError ? (loadError instanceof ApiError ? loadError.message : t("loadError")) : null;

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-col gap-1">
          <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
          <Link href="/settings#notifications" className="text-sm text-blue-600 hover:underline dark:text-blue-400">
            {t("settingsLink")}
          </Link>
        </div>
        {totalCount > 0 &&
          (confirmingClear ? (
            // Inline rather than a dialog: the question is small, and the answer sits where the
            // click just was. There is no undo for a cleared list, hence the question at all.
            <div className="flex flex-wrap items-center gap-2 text-sm text-gray-600 dark:text-gray-400" role="group" aria-label={t("clearAllConfirm")}>
              <span>{t("clearAllConfirm")}</span>
              <Button variant="danger" onClick={handleClearAll} disabled={dismissAll.isPending}>
                {dismissAll.isPending ? t("clearing") : t("clearAllYes")}
              </Button>
              <Button variant="secondary" onClick={() => setConfirmingClear(false)} disabled={dismissAll.isPending}>
                {t("clearAllCancel")}
              </Button>
            </div>
          ) : (
            <Button variant="secondary" onClick={() => setConfirmingClear(true)}>
              {t("clearAll")}
            </Button>
          ))}
      </div>

      {(error ?? loadErrorText) && <p className="text-sm text-red-600 dark:text-red-400">{error ?? loadErrorText}</p>}

      {!data ? (
        !loadErrorText && <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>
      ) : totalCount === 0 ? (
        resolveGmailEmptyState(gmailScanStatus.data) === "waitingForEmail" ? (
          <EmptyState title={t("empty")} body={t("emptyBodyScanning")} />
        ) : (
          <EmptyState
            title={t("empty")}
            body={t("emptyBody")}
            actions={
              <Link href="/help/chrome-extension#gmail" className={buttonClassName("outline")}>
                {t("emptyCta")}
              </Link>
            }
          />
        )
      ) : (
        <>
          <p className="-mt-3 text-xs text-gray-500 dark:text-gray-400">{t("swipeHint")}</p>
          <ul className="flex flex-col gap-4">
            {visible.map((n) => (
              <NotificationCard
                key={n.id}
                item={n}
                locale={locale}
                reverted={revertedIds.includes(n.id)}
                reverting={revertingId === n.id}
                onRevert={handleRevert}
                onDismiss={handleDismiss}
              />
            ))}
          </ul>
          <Pagination
            page={page}
            pageSize={NOTIFICATION_PAGE_SIZE}
            totalCount={totalCount}
            unit="notifications"
            onPageChange={setPage}
          />
        </>
      )}
    </div>
  );
}
