"use client";

import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useRouter } from "@/i18n/navigation";
import { applicationsApi } from "@/lib/api/applications";
import { isQuickFindShortcut, moveActive, QUICK_FIND_OPEN_EVENT } from "@/lib/quickFind/quickFind";
import { useQuickFindShortcutLabel } from "@/hooks/useShortcutLabel";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { QuickCard } from "@/components/quickFind/QuickCard";

/** Enough to tell a company's few applications apart; a longer list is the applications page. */
const MAX_RESULTS = 6;

/**
 * Ctrl+K / ⌘K from anywhere in the signed-in app: type a company, see its card (canvas "İnce
 * dokunuşlar — Paket 5", 2A). Built for the moment a recruiter calls and the user has seconds to
 * remember which application it is. Mounted once in the protected layout; renders nothing until
 * the shortcut is pressed.
 */
export function QuickFind() {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (!isQuickFindShortcut(event)) return;
      event.preventDefault();
      setOpen(true);
    };
    const onOpenRequest = () => setOpen(true);
    window.addEventListener("keydown", onKeyDown);
    window.addEventListener(QUICK_FIND_OPEN_EVENT, onOpenRequest);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener(QUICK_FIND_OPEN_EVENT, onOpenRequest);
    };
  }, []);

  return open ? <QuickFindDialog onClose={() => setOpen(false)} /> : null;
}

function QuickFindDialog({ onClose }: { onClose: () => void }) {
  const t = useTranslations("quickFind");
  const tStatus = useTranslations("status");
  const router = useRouter();
  const shortcut = useQuickFindShortcutLabel();
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [text, setText] = useState("");
  const [active, setActive] = useState(0);
  const search = useDebouncedValue(text.trim(), 200);

  // Opens on mount, closes on unmount — the same native-<dialog> handling as Modal: focus trap,
  // inert page behind, Esc and the top layer come from the platform.
  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog || dialog.open) return;
    dialog.showModal();
    dialog.querySelector<HTMLInputElement>("input")?.focus();
    return () => dialog.close();
  }, []);

  const query = { search, page: 1, pageSize: MAX_RESULTS, sortBy: "AppliedAt", sortDirection: "Descending" } as const;
  const { data, isFetching } = useQuery({
    queryKey: ["applications", "list", query],
    queryFn: () => applicationsApi.getAll(query),
    enabled: search !== "",
  });
  const results = search !== "" ? (data?.items ?? []) : [];
  const current = results[Math.min(active, results.length - 1)];

  return (
    <dialog
      ref={dialogRef}
      aria-label={t("title")}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClick={(event) => {
        // The backdrop, or any link in the card: following one is the end of the lookup — even
        // when it only adds "?open=note" to the page already underneath.
        if (event.target === dialogRef.current || (event.target as Element).closest("a")) onClose();
      }}
      className="mx-auto mt-[8vh] w-[min(40rem,calc(100vw-2rem))] rounded-xl border border-gray-200 bg-white p-0 text-gray-900 shadow-2xl backdrop:bg-gray-900/45 dark:border-gray-800 dark:bg-gray-900 dark:text-gray-100"
    >
      <div className="flex max-h-[80dvh] flex-col">
        <div className="flex items-center gap-2.5 border-b border-gray-200 px-4 py-3 dark:border-gray-800">
          <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" className="flex-none text-gray-500">
            <circle cx="11" cy="11" r="7" />
            <path d="m20 20-3.5-3.5" />
          </svg>
          <input
            type="search"
            value={text}
            onChange={(event) => {
              setText(event.target.value);
              setActive(0);
            }}
            onKeyDown={(event) => {
              if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                event.preventDefault();
                setActive((index) => moveActive(index, results.length, event.key as "ArrowUp" | "ArrowDown"));
              } else if (event.key === "Enter" && current) {
                event.preventDefault();
                router.push(`/applications/${current.id}`);
                onClose();
              }
            }}
            placeholder={t("placeholder")}
            aria-label={t("placeholder")}
            className="min-w-0 flex-1 bg-transparent text-base outline-none placeholder:text-gray-400"
          />
          <kbd className="rounded border border-gray-200 px-1.5 py-0.5 font-mono text-[11px] text-gray-500 dark:border-gray-700">Esc</kbd>
        </div>

        <div className="flex-1 overflow-y-auto">
          {results.length > 1 && (
            <ul className="flex flex-col gap-0.5 border-b border-gray-100 p-2 dark:border-gray-800">
              {results.map((result, index) => (
                <li key={result.id}>
                  <button
                    type="button"
                    onClick={() => setActive(index)}
                    aria-current={index === active ? "true" : undefined}
                    className={`flex w-full justify-between gap-3 rounded-md px-2.5 py-2 text-left text-sm ${index === active ? "bg-accent-wash text-accent-ink" : "hover:bg-gray-50 dark:hover:bg-gray-800"}`}
                  >
                    <span className="min-w-0 truncate">
                      <span className="font-medium">{result.companyName}</span> · {result.jobTitle}
                    </span>
                    <span className="flex-none text-gray-600 dark:text-gray-400">{tStatus(result.status)}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}

          <div className="p-3">
            {current ? (
              <QuickCard applicationId={current.id} />
            ) : (
              <p className="px-1 py-6 text-center text-sm text-gray-500 dark:text-gray-400" role="status">
                {search === "" ? t("hint") : isFetching ? t("searching") : t("empty", { search })}
              </p>
            )}
          </div>
        </div>

        <div className="flex flex-wrap gap-x-4 gap-y-1 border-t border-gray-200 px-4 py-2 text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
          <span><kbd className="font-mono">↑↓</kbd> {t("keys.move")}</span>
          <span><kbd className="font-mono">Enter</kbd> {t("keys.open")}</span>
          <span>{t("keys.anywhere", { shortcut })}</span>
        </div>
      </div>
    </dialog>
  );
}
