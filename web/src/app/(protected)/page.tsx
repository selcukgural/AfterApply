"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { analyticsApi } from "@/lib/api/analytics";
import { applicationsApi } from "@/lib/api/applications";
import { ANALYTICS_RATE_TILES } from "@/lib/dashboard/analyticsLabels";
import { DASHBOARD_TILES } from "@/lib/dashboard/statusMapping";
import { ResponseTimeCard } from "@/components/dashboard/ResponseTimeCard";
import { StatTile } from "@/components/dashboard/StatTile";
import { StatusDistributionChart } from "@/components/dashboard/StatusDistributionChart";
import { Button } from "@/components/ui/Button";

export default function DashboardPage() {
  const { data: summary, isLoading: summaryLoading } = useQuery({
    queryKey: ["applications", "summary"],
    queryFn: applicationsApi.getSummary,
  });

  const { data: overview, isLoading: overviewLoading } = useQuery({
    queryKey: ["analytics", "overview"],
    queryFn: analyticsApi.getOverview,
  });

  return (
    <div className="flex flex-col gap-8">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">Panel</h1>
        <Link href="/applications/new">
          <Button>Yeni Başvuru</Button>
        </Link>
      </div>

      {summaryLoading || !summary ? (
        <p className="text-sm text-gray-500">Yükleniyor...</p>
      ) : (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
          {DASHBOARD_TILES.map((tile) => (
            <StatTile key={tile.key} label={tile.label} value={summary[tile.key]} />
          ))}
        </div>
      )}

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900">Kişisel Analitik</h2>

        {overviewLoading || !overview ? (
          <p className="text-sm text-gray-500">Yükleniyor...</p>
        ) : (
          <>
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
              {ANALYTICS_RATE_TILES.map((tile) => (
                <StatTile
                  key={tile.key}
                  label={tile.label}
                  value={Math.round(overview.rates[tile.key])}
                  suffix="%"
                />
              ))}
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <ResponseTimeCard stats={overview.responseTime} />
              <StatusDistributionChart data={overview.statusDistribution} />
            </div>
          </>
        )}
      </section>
    </div>
  );
}
