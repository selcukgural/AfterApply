import { Button } from "@/components/ui/Button";

interface PaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ page, pageSize, totalCount, onPageChange }: PaginationProps) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  if (totalPages <= 1) {
    return null;
  }

  return (
    <div className="flex items-center justify-between text-sm text-gray-600">
      <span>
        Sayfa {page} / {totalPages} ({totalCount} başvuru)
      </span>
      <div className="flex gap-2">
        <Button variant="secondary" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
          Önceki
        </Button>
        <Button variant="secondary" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
          Sonraki
        </Button>
      </div>
    </div>
  );
}
