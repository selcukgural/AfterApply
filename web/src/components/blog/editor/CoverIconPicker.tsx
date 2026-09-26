"use client";

import { createElement, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/Button";
import { Modal } from "@/components/ui/Modal";
import type { CoverIconNode } from "@/lib/blog/coverCard";
import { searchCoverIcons } from "@/lib/blog/coverIconSearch";
import type { CoverIconSet } from "@/lib/blog/coverIconSet";

/** One icon from the set, drawn at the size it is shown. */
export function CoverIcon({ node, size = 24, className }: { node: CoverIconNode | null | undefined; size?: number; className?: string }) {
  if (!node) return null;
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      className={className}
    >
      {node.map(([tag, attributes], index) => createElement(tag, { key: index, ...attributes }))}
    </svg>
  );
}

/**
 * The cover icon picker (canvas "Admin: kapak bölümü + simge seçici", 2026-09-27): search box, a
 * grid of results, the picked icon's name and tags, "use". The search is `searchCoverIcons` —
 * Turkish words through the alias list, then Lucide's own English names and tags.
 */
export function CoverIconPicker({
  set,
  value,
  onPick,
  onClose,
}: {
  /** The loaded set; null while it loads, "error" when it could not. */
  set: CoverIconSet | null | "error";
  value: string;
  onPick: (name: string) => void;
  onClose: () => void;
}) {
  const t = useTranslations("adminBlog.iconPicker");
  const [query, setQuery] = useState("");
  const [picked, setPicked] = useState(value);
  const loaded = set && set !== "error" ? set : null;

  const results = useMemo(() => (loaded ? searchCoverIcons(query, loaded) : { names: [], total: 0 }), [loaded, query]);

  const countLine = !query.trim()
    ? t("featured", { count: loaded ? loaded.names.length.toLocaleString() : "…" })
    : results.total > results.names.length
      ? t("resultsCapped", { total: results.total, shown: results.names.length })
      : t("results", { total: results.total });

  return (
    <Modal
      title={t("title")}
      onClose={onClose}
      wide
      footer={
        <>
          <div className="mr-auto flex min-w-0 items-center gap-3">
            <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg border border-[#d6e1f7] bg-[#eef3fd] text-[#2a5fd6]">
              <CoverIcon node={loaded?.nodes[picked]} size={22} />
            </span>
            <span className="flex min-w-0 flex-col">
              <code className="truncate text-sm text-gray-900 dark:text-gray-100">{picked}</code>
              <span className="truncate text-xs text-gray-500 dark:text-gray-400">{(loaded?.tags[picked] ?? []).slice(0, 6).join(", ")}</span>
            </span>
          </div>
          <Button variant="secondary" onClick={onClose}>
            {t("cancel")}
          </Button>
          <Button onClick={() => onPick(picked)} disabled={!loaded || !loaded.nodes[picked]}>
            {t("use")}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-3">
        <h2 className="text-lg font-semibold">{t("title")}</h2>
        <label htmlFor="cover-icon-search" className="text-sm font-medium text-gray-700 dark:text-gray-300">
          {t("search")}
        </label>
        <input
          id="cover-icon-search"
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder={t("searchPlaceholder")}
          autoFocus
          autoComplete="off"
          className="h-10 w-full rounded-md border border-gray-300 bg-white px-3 text-sm text-gray-900 placeholder:text-gray-400 focus:border-accent focus:outline-none dark:border-gray-700 dark:bg-gray-950 dark:text-gray-100"
        />
        <p className="text-xs text-gray-500 dark:text-gray-400" aria-live="polite">
          {set === "error" ? t("loadError") : loaded ? countLine : t("loading")}
        </p>
        <div className="h-80 overflow-y-auto rounded-lg border border-gray-100 p-2 dark:border-gray-800">
          {loaded && results.names.length === 0 && (
            <p className="py-10 text-center text-sm text-gray-500 dark:text-gray-400">{t("empty")}</p>
          )}
          <div className="grid grid-cols-6 gap-1.5 sm:grid-cols-10">
            {loaded &&
              results.names.map((name) => {
                const on = name === picked;
                return (
                  <button
                    key={name}
                    type="button"
                    title={name}
                    aria-label={name}
                    aria-pressed={on}
                    onClick={() => setPicked(name)}
                    onDoubleClick={() => onPick(name)}
                    className={`flex h-12 items-center justify-center rounded-lg border text-gray-800 dark:text-gray-200 ${
                      on
                        ? "border-2 border-[#2a5fd6] bg-[#eef3fd] text-[#2a5fd6] dark:bg-gray-800"
                        : "border-gray-200 hover:border-gray-400 dark:border-gray-800 dark:hover:border-gray-600"
                    }`}
                  >
                    <CoverIcon node={loaded.nodes[name]} />
                  </button>
                );
              })}
          </div>
        </div>
      </div>
    </Modal>
  );
}
