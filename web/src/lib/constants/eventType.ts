import type { ApplicationEventType } from "@/types/api";

export const EVENT_TYPE_LABELS: Record<ApplicationEventType, string> = {
  ApplicationCreated: "Başvuru Oluşturuldu",
  ApplicationSubmitted: "Başvuru Gönderildi",
  RecruiterContacted: "İşe Alım Uzmanı İletişime Geçti",
  ScreeningStarted: "Ön Değerlendirme Başladı",
  InterviewScheduled: "Mülakat Planlandı",
  InterviewCompleted: "Mülakat Tamamlandı",
  OfferReceived: "Teklif Alındı",
  FollowUpSent: "Takip Mesajı Gönderildi",
  StatusChanged: "Durum Değişti",
};
