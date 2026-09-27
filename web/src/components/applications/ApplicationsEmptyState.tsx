"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { Button, buttonClassName } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";

/**
 * The list's empty state. With a filter on, "no applications yet — add one" would be wrong (they
 * have some; the filter hid them), so it says what happened and offers the one button that helps.
 */
export function ApplicationsEmptyState({ onClearFilters }: { onClearFilters?: () => void }) {
  const t = useTranslations("applications.table");

  if (onClearFilters) {
    return (
      <EmptyState
        title={t("noMatch")}
        body={t("noMatchBody")}
        actions={
          <Button variant="secondary" onClick={onClearFilters}>
            {t("clearFilters")}
          </Button>
        }
      />
    );
  }

  return (
    <EmptyState
      title={t("empty")}
      body={t("emptyBody")}
      actions={
        <Link href="/applications/new" className={buttonClassName("primary")}>
          {t("emptyCta")}
        </Link>
      }
    />
  );
}
