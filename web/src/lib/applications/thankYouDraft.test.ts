import { describe, expect, it } from "vitest";
import { thankYouDraft } from "./thankYouDraft";

const input = { companyName: "Lodos Yazılım", jobTitle: "Backend Developer" };

describe("thankYouDraft", () => {
  it("greets the people met by the names typed, and names the role", () => {
    const tr = thankYouDraft("tr", { ...input, interviewWith: "  Ece Kaya, Burak Demir " });
    expect(tr.startsWith("Merhaba Ece Kaya, Burak Demir,\n")).toBe(true);
    expect(tr).toContain("Lodos Yazılım Backend Developer görüşmesi");

    const en = thankYouDraft("en", { ...input, interviewWith: "Ece Kaya" });
    expect(en.startsWith("Hi Ece Kaya,\n")).toBe(true);
    expect(en).toContain("the Backend Developer role at Lodos Yazılım");
  });

  it("never guesses a title from a name", () => {
    const tr = thankYouDraft("tr", { ...input, interviewWith: "Ece Kaya" });
    expect(tr).not.toMatch(/Hanım|Bey/);
  });

  it("greets no one by name when no one was recorded", () => {
    expect(thankYouDraft("tr", { ...input, interviewWith: null }).startsWith("Merhaba,\n")).toBe(true);
    expect(thankYouDraft("en", { ...input, interviewWith: "   " }).startsWith("Hi,\n")).toBe(true);
  });
});
