"use client";

import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/Button";

interface PaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ page, pageSize, totalCount, onPageChange }: PaginationProps) {
  const t = useTranslations("applications.pagination");
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  if (totalPages <= 1) {
    return null;
  }

  return (
    <div className="flex items-center justify-between text-sm text-gray-600">
      <span>{t("pageInfo", { page, totalPages, totalCount })}</span>
      <div className="flex gap-2">
        <Button variant="secondary" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
          {t("previous")}
        </Button>
        <Button variant="secondary" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
          {t("next")}
        </Button>
      </div>
    </div>
  );
}
