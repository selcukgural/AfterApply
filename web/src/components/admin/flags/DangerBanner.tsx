"use client";

import { useTranslations } from "next-intl";
import { WarningIcon } from "./FlagBits";

/** The red strip on top of every flag page: what a change here does, before anyone looks for a switch. */
export function DangerBanner() {
  const t = useTranslations("adminFlags.banner");
  return (
    <div role="note" className="flex items-start gap-2.5 rounded-lg bg-red-700 px-4 py-3 text-sm leading-relaxed text-white dark:bg-red-800">
      <WarningIcon className="mt-0.5 h-[18px] w-[18px] shrink-0" />
      <p>
        <b>{t("strong")}</b> {t("body")}
      </p>
    </div>
  );
}
