import type { ApplicationStatus } from "@/types/api";

export const APPLICATION_STATUSES: ApplicationStatus[] = [
  "Applied",
  "Screening",
  "Interview",
  "TechnicalInterview",
  "FinalInterview",
  "Offer",
  "Accepted",
  "Rejected",
  "Withdrawn",
  "Ghosted",
];

export const APPLICATION_STATUS_LABELS: Record<ApplicationStatus, string> = {
  Applied: "Başvuruldu",
  Screening: "Ön Değerlendirme",
  Interview: "Mülakat",
  TechnicalInterview: "Teknik Mülakat",
  FinalInterview: "Son Mülakat",
  Offer: "Teklif",
  Accepted: "Kabul Edildi",
  Rejected: "Reddedildi",
  Withdrawn: "Geri Çekildi",
  Ghosted: "Kayboldu",
};
