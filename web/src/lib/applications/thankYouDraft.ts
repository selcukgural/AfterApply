/**
 * The thank-you note offered after an interview (canvas "İnce dokunuşlar — Paket 6", 6A). A draft
 * to copy, never sent from here. It greets people by the names the user typed, as typed — no
 * "Hanım/Bey", which would mean guessing someone's gender from their name.
 */

export type DraftLanguage = "tr" | "en";

interface DraftInput {
  /** Who the user met, as they wrote it ("Ece Kaya, Burak Demir"); empty greets no one by name. */
  interviewWith: string | null | undefined;
  companyName: string;
  jobTitle: string;
}

export function thankYouDraft(language: DraftLanguage, { interviewWith, companyName, jobTitle }: DraftInput): string {
  const names = interviewWith?.trim();
  if (language === "en") {
    return [
      names ? `Hi ${names},` : "Hi,",
      "",
      `Thank you for taking the time to talk with me about the ${jobTitle} role at ${companyName}. ` +
        "Our conversation made me even more interested in the team and the work. " +
        "If there is anything else you need from me, I am happy to share it.",
      "",
      "Best regards",
    ].join("\n");
  }
  return [
    names ? `Merhaba ${names},` : "Merhaba,",
    "",
    `${companyName} ${jobTitle} görüşmesi için zaman ayırdığınız için teşekkür ederim. ` +
      "Rol ve ekibin çalışma biçimi hakkında konuştuklarımız ilgimi daha da artırdı. " +
      "Süreçle ilgili ihtiyaç duyduğunuz başka bir bilgi olursa memnuniyetle paylaşırım.",
    "",
    "Saygılarımla",
  ].join("\n");
}
