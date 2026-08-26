import { getTranslations } from "next-intl/server";
import { StatTile } from "@/components/dashboard/StatTile";

// A static, illustrative mock of the real dashboard — reuses the actual
// <StatTile> component (pure/props-only, no fetching) so this visual stays
// pixel-identical to the product instead of drifting from it. Numbers are
// hardcoded demo data, clearly labeled as such (spec §8/§34/§38).
export async function DashboardPreview() {
  const t = await getTranslations("landing.heroPreview");
  const tCommon = await getTranslations("landing.common");
  const tRates = await getTranslations("dashboard.rates");

  const rows = [
    { company: t("row1Company"), status: t("row1Status"), meta: t("row1Meta"), dot: "bg-blue-500" },
    { company: t("row2Company"), status: t("row2Status"), meta: t("row2Meta"), dot: "bg-amber-500" },
    { company: t("row3Company"), status: t("row3Status"), meta: t("row3Meta"), dot: "bg-gray-400" },
  ];

  return (
    <div className="w-full max-w-md rounded-xl border border-gray-200 bg-white p-5 shadow-lg dark:border-gray-800 dark:bg-gray-900">
      <div className="mb-4 flex items-center justify-between">
        <p className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("title")}</p>
        <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-500 dark:bg-gray-800 dark:text-gray-400">
          {tCommon("sampleData")}
        </span>
      </div>

      <div className="mb-4 grid grid-cols-3 gap-2">
        <StatTile label={t("applications")} value={127} />
        <StatTile label={tRates("responseRate")} value={63} suffix="%" />
        <StatTile label={tRates("interviewRate")} value={18} suffix="%" />
      </div>

      <ul className="flex flex-col divide-y divide-gray-100 dark:divide-gray-800">
        {rows.map((row) => (
          <li key={row.company} className="flex items-center justify-between py-2.5 text-sm">
            <div className="flex items-center gap-2">
              <span className={`h-2 w-2 shrink-0 rounded-full ${row.dot}`} aria-hidden="true" />
              <span className="text-gray-900 dark:text-gray-100">{row.company}</span>
            </div>
            <div className="text-right">
              <p className="text-gray-700 dark:text-gray-300">{row.status}</p>
              <p className="text-xs text-gray-500 dark:text-gray-500">{row.meta}</p>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
