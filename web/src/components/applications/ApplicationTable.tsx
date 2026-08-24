import Link from "next/link";
import type { ApplicationSummaryResponse } from "@/types/api";
import { StatusBadge } from "@/components/applications/StatusBadge";

export function ApplicationTable({ items }: { items: ApplicationSummaryResponse[] }) {
  if (items.length === 0) {
    return <p className="py-8 text-center text-sm text-gray-500">Kayıtlı başvuru bulunamadı.</p>;
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
      <table className="w-full text-left text-sm">
        <thead className="border-b border-gray-200 bg-gray-50 text-xs uppercase text-gray-500">
          <tr>
            <th className="px-4 py-3">Şirket</th>
            <th className="px-4 py-3">Pozisyon</th>
            <th className="px-4 py-3">Durum</th>
            <th className="px-4 py-3">Başvuru Tarihi</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id} className="border-b border-gray-100 last:border-0 hover:bg-gray-50">
              <td className="px-4 py-3">
                <Link href={`/applications/${item.id}`} className="font-medium text-blue-600 hover:underline">
                  {item.companyName}
                </Link>
              </td>
              <td className="px-4 py-3 text-gray-700">{item.jobTitle}</td>
              <td className="px-4 py-3">
                <StatusBadge status={item.status} />
              </td>
              <td className="px-4 py-3 text-gray-500">
                {new Date(item.appliedAt).toLocaleDateString("tr-TR")}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
