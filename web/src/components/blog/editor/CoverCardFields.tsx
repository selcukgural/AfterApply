"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import type { BlogCoverCard as BlogCoverCardValue } from "@/types/api";
import { COVER_HOOK_MAX, DEFAULT_COVER_ICON, resolveCoverIcon } from "@/lib/blog/coverCard";
import type { CoverIconSet } from "@/lib/blog/coverIconSet";
import { CoverIcon, CoverIconPicker } from "./CoverIconPicker";
import { Proposal } from "./BlogSeoSection";

/**
 * The generated cover's two fields in the editor's cover block (DECISIONS.md 2026-09-27): the line
 * and the icon (the block draws the cover itself, above, while no image is uploaded). Blog posts
 * only — a guide has no generated cover.
 */
export function CoverCardFields({
  value,
  onChange,
  iconSet,
  proposal,
}: {
  value: BlogCoverCardValue;
  onChange: (value: BlogCoverCardValue) => void;
  iconSet: CoverIconSet | null | "error";
  /** What the SEO suggestion proposed for these two fields, when it did. */
  proposal: BlogCoverCardValue | null;
}) {
  const tEditor = useTranslations("adminBlog.editor");
  const tSeo = useTranslations("adminBlog.seo");
  const [picking, setPicking] = useState(false);
  const loaded = iconSet && iconSet !== "error" ? iconSet : null;
  const iconName = value.icon ?? DEFAULT_COVER_ICON;
  const hook = value.hook ?? "";

  const hookProposal = proposal?.hook && proposal.hook !== hook ? proposal.hook : null;
  const iconProposal = proposal?.icon && proposal.icon !== value.icon ? proposal.icon : null;

  return (
    <div className="flex flex-col gap-2">
      <label htmlFor="blog-cover-hook" className="mt-1 text-sm font-medium text-gray-700 dark:text-gray-300">
        {tEditor("coverHook")}
      </label>
      <input
        id="blog-cover-hook"
        type="text"
        value={hook}
        onChange={(e) => onChange({ ...value, hook: e.target.value })}
        placeholder={tEditor("coverHookPlaceholder")}
        maxLength={COVER_HOOK_MAX}
        className="h-9 w-full rounded-md border border-gray-300 bg-white px-2.5 text-sm text-gray-900 placeholder:text-gray-400 focus:border-accent focus:outline-none dark:border-gray-700 dark:bg-gray-950 dark:text-gray-100"
      />
      <p className="text-xs text-gray-500 dark:text-gray-400">
        <span className="tabular-nums">
          {hook.length}/{COVER_HOOK_MAX}
        </span>{" "}
        · {tEditor("coverHookHint", { max: COVER_HOOK_MAX })}
      </p>
      <Proposal
        label={tSeo("suggestion")}
        apply={tSeo("apply")}
        value={hookProposal}
        onApply={() => onChange({ ...value, hook: hookProposal })}
      />

      <span className="mt-1 text-sm font-medium text-gray-700 dark:text-gray-300">{tEditor("coverIcon")}</span>
      <div className="flex items-center gap-2.5">
        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg border border-[#d6e1f7] bg-[#eef3fd] text-[#2a5fd6]">
          <CoverIcon node={loaded ? resolveCoverIcon(loaded.nodes, value.icon) : null} size={22} />
        </span>
        <span className="flex min-w-0 flex-1 flex-col">
          <code className="truncate text-xs text-gray-700 dark:text-gray-300">{iconName}</code>
          {value.icon === null && <span className="text-[11px] text-gray-500 dark:text-gray-400">{tEditor("coverIconDefault")}</span>}
        </span>
        <button
          type="button"
          onClick={() => setPicking(true)}
          className="h-9 shrink-0 rounded-md border border-gray-300 bg-white px-3 text-sm font-medium text-gray-900 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-950 dark:text-gray-100 dark:hover:bg-gray-900"
        >
          {tEditor("coverIconChange")}
        </button>
      </div>
      <Proposal
        label={tSeo("suggestion")}
        apply={tSeo("apply")}
        value={iconProposal}
        onApply={() => onChange({ ...value, icon: iconProposal })}
      />

      {picking && (
        <CoverIconPicker
          set={iconSet}
          value={iconName}
          onClose={() => setPicking(false)}
          onPick={(name) => {
            onChange({ ...value, icon: name });
            setPicking(false);
          }}
        />
      )}
    </div>
  );
}
