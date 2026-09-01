"use client";

import { Bar, BarChart, CartesianGrid, LabelList, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { useTranslations } from "next-intl";
import type { StatusDistributionItem } from "@/types/api";

export function StatusDistributionChart({ data }: { data: StatusDistributionItem[] }) {
  const t = useTranslations("dashboard.statusDistribution");
  const tStatus = useTranslations("status");
  const chartData = data.map((item) => ({
    status: tStatus(item.status),
    count: item.count,
  }));

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm dark:border-gray-800 dark:bg-gray-900">
      <p className="mb-3 text-sm font-medium text-gray-700 dark:text-gray-300">{t("title")}</p>
      <ResponsiveContainer width="100%" height={280}>
        <BarChart data={chartData} margin={{ top: 4, right: 8, left: 0, bottom: 32 }}>
          <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="var(--chart-grid)" />
          <XAxis
            dataKey="status"
            angle={-35}
            textAnchor="end"
            interval={0}
            height={70}
            tick={{ fontSize: 11, fill: "var(--chart-tick)" }}
          />
          <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: "var(--chart-tick)" }} />
          <Tooltip
            contentStyle={{ backgroundColor: "var(--chart-tooltip-bg)", color: "var(--chart-tooltip-text)", border: "none" }}
            labelStyle={{ color: "var(--chart-tooltip-text)" }}
          />
          <Bar dataKey="count" fill="#2563eb" radius={[4, 4, 0, 0]} minPointSize={3}>
            <LabelList dataKey="count" position="top" style={{ fontSize: 11, fill: "var(--chart-tick)" }} />
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
