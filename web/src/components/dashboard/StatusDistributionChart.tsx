"use client";

import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { APPLICATION_STATUS_LABELS } from "@/lib/constants/applicationStatus";
import type { StatusDistributionItem } from "@/types/api";

export function StatusDistributionChart({ data }: { data: StatusDistributionItem[] }) {
  const chartData = data.map((item) => ({
    status: APPLICATION_STATUS_LABELS[item.status],
    count: item.count,
  }));

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
      <p className="mb-3 text-sm font-medium text-gray-700">Durum Dağılımı</p>
      <ResponsiveContainer width="100%" height={280}>
        <BarChart data={chartData} margin={{ top: 4, right: 8, left: 0, bottom: 32 }}>
          <CartesianGrid strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="status" angle={-35} textAnchor="end" interval={0} height={70} tick={{ fontSize: 11 }} />
          <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
          <Tooltip />
          <Bar dataKey="count" fill="#2563eb" radius={[4, 4, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
