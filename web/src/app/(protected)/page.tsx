"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { applicationsApi } from "@/lib/api/applications";
import { DASHBOARD_TILES } from "@/lib/dashboard/statusMapping";
import { StatTile } from "@/components/dashboard/StatTile";
import { Button } from "@/components/ui/Button";

export default function DashboardPage() {
  const { data: summary, isLoading } = useQuery({
    queryKey: ["applications", "summary"],
    queryFn: applicationsApi.getSummary,
  });

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">Panel</h1>
        <Link href="/applications/new">
          <Button>Yeni Başvuru</Button>
        </Link>
      </div>

      {isLoading || !summary ? (
        <p className="text-sm text-gray-500">Yükleniyor...</p>
      ) : (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
          {DASHBOARD_TILES.map((tile) => (
            <StatTile key={tile.key} label={tile.label} value={summary[tile.key]} />
          ))}
        </div>
      )}
    </div>
  );
}
