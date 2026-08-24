import type { ResponseTimeStatsResponse } from "@/types/api";

export function ResponseTimeCard({ stats }: { stats: ResponseTimeStatsResponse }) {
  const hasData = stats.averageDays !== null && stats.medianDays !== null;

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
      <p className="mb-2 text-sm font-medium text-gray-700">Yanıt Süresi</p>
      {hasData ? (
        <div className="flex gap-6">
          <div>
            <p className="text-xs text-gray-500">Ortalama</p>
            <p className="text-xl font-semibold text-gray-900">{stats.averageDays!.toFixed(1)} gün</p>
          </div>
          <div>
            <p className="text-xs text-gray-500">Medyan</p>
            <p className="text-xl font-semibold text-gray-900">{stats.medianDays!.toFixed(1)} gün</p>
          </div>
        </div>
      ) : (
        <p className="text-sm text-gray-500">Henüz yanıt alınan bir başvuru yok.</p>
      )}
    </div>
  );
}
