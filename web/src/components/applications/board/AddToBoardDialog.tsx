"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Modal } from "@/components/ui/Modal";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { StatusBadge } from "@/components/applications/StatusBadge";
import { applicationsApi } from "@/lib/api/applications";
import { CompanyMark } from "./CompanyMark";

const PAGE_SIZE = 8;

/**
 * Picks applications that are not on the board and puts them there. Reads the list endpoint with
 * onBoard=false, so it pages and searches exactly like the list does; the selection survives
 * paging, and the server skips anything that got onto the board meanwhile.
 */
export function AddToBoardDialog({
  isSubmitting,
  error,
  onAdd,
  onClose,
}: {
  isSubmitting: boolean;
  error: string | null;
  onAdd: (applicationIds: string[]) => void;
  onClose: () => void;
}) {
  const t = useTranslations("applications.board.addDialog");
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string[]>([]);

  useEffect(() => {
    const timer = setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 250);
    return () => clearTimeout(timer);
  }, [searchInput]);

  const query = useQuery({
    queryKey: ["applications", "list", { onBoard: false, search, page, pageSize: PAGE_SIZE, sortBy: "UpdatedAt" }],
    queryFn: () =>
      applicationsApi.getAll({
        onBoard: false,
        search: search || undefined,
        page,
        pageSize: PAGE_SIZE,
        sortBy: "UpdatedAt",
        sortDirection: "Descending",
      }),
  });

  const items = query.data?.items ?? [];
  const total = query.data?.totalCount ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const toggle = (id: string) =>
    setSelected((current) => (current.includes(id) ? current.filter((value) => value !== id) : [...current, id]));

  return (
    <Modal
      title={t("title")}
      onClose={onClose}
      busy={isSubmitting}
      wide
      footer={
        <>
          <span className="mr-auto text-sm text-gray-600 dark:text-gray-400">{t("selected", { count: selected.length })}</span>
          <Button type="button" variant="secondary" onClick={onClose} disabled={isSubmitting}>
            {t("cancel")}
          </Button>
          <Button type="button" onClick={() => onAdd(selected)} disabled={selected.length === 0 || isSubmitting}>
            {t("add")}
          </Button>
        </>
      }
    >
      <h2 className="text-base font-semibold">{t("title")}</h2>
      {query.data && !search && (
        <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("intro", { count: total })}</p>
      )}
      <label className="mt-4 block">
        <span className="sr-only">{t("search")}</span>
        <Input
          type="search"
          value={searchInput}
          onChange={(event) => setSearchInput(event.target.value)}
          placeholder={t("search")}
        />
      </label>
      {error && <p className="mt-3 text-sm text-crit-ink">{error}</p>}

      <ul className="mt-3 divide-y divide-gray-100 rounded-lg border border-gray-200 dark:divide-gray-800 dark:border-gray-800">
        {items.map((item) => (
          <li key={item.id}>
            <label className="flex min-h-11 cursor-pointer items-center gap-3 px-3 py-2 text-sm hover:bg-gray-50 dark:hover:bg-gray-800/60">
              <input
                type="checkbox"
                checked={selected.includes(item.id)}
                onChange={() => toggle(item.id)}
                className="h-4 w-4 rounded border-gray-300 text-accent focus:ring-accent"
              />
              <CompanyMark companyId={item.companyId} companyName={item.companyName} />
              <span className="min-w-0 flex-1 truncate">
                <strong className="font-semibold">{item.companyName}</strong> · {item.jobTitle}
              </span>
              <StatusBadge status={item.status} />
            </label>
          </li>
        ))}
        {query.data && items.length === 0 && (
          <li className="px-3 py-6 text-center text-sm text-gray-500 dark:text-gray-400">{search ? t("noMatch") : t("intro", { count: 0 })}</li>
        )}
      </ul>

      {pageCount > 1 && (
        <div className="mt-3 flex items-center justify-between text-sm">
          <Button type="button" variant="secondary" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>
            {t("previous")}
          </Button>
          <span className="text-gray-600 dark:text-gray-400">
            {page} / {pageCount}
          </span>
          <Button type="button" variant="secondary" disabled={page >= pageCount} onClick={() => setPage((value) => value + 1)}>
            {t("next")}
          </Button>
        </div>
      )}
    </Modal>
  );
}
