import type { ApplicationStatus } from "@/types/api";
import { APPLICATION_STATUS_LABELS } from "@/lib/constants/applicationStatus";

const STATUS_COLORS: Record<ApplicationStatus, string> = {
  Applied: "bg-gray-100 text-gray-700",
  Screening: "bg-blue-100 text-blue-700",
  Interview: "bg-blue-100 text-blue-700",
  TechnicalInterview: "bg-blue-100 text-blue-700",
  FinalInterview: "bg-blue-100 text-blue-700",
  Offer: "bg-amber-100 text-amber-700",
  Accepted: "bg-green-100 text-green-700",
  Rejected: "bg-red-100 text-red-700",
  Withdrawn: "bg-gray-100 text-gray-500",
  Ghosted: "bg-purple-100 text-purple-700",
};

export function StatusBadge({ status }: { status: ApplicationStatus }) {
  return (
    <span className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_COLORS[status]}`}>
      {APPLICATION_STATUS_LABELS[status]}
    </span>
  );
}
