import { describe, expect, it } from "vitest";
import { closingMoment, invitesExperience } from "./shareExperience";

const open = ["Applied", "Screening", "Interview", "TechnicalInterview", "FinalInterview", "Offer"] as const;

describe("closingMoment", () => {
  it("names the two endings the candidate did not choose", () => {
    expect(closingMoment("Rejected")).toBe("ending");
    expect(closingMoment("Ghosted")).toBe("ending");
  });

  it("gives an accepted offer its own moment", () => {
    expect(closingMoment("Accepted")).toBe("accepted");
  });

  it("has nothing to say while the process is open or after a withdrawal", () => {
    for (const status of [...open, "Withdrawn"] as const) {
      expect(closingMoment(status)).toBeNull();
    }
  });
});

describe("invitesExperience", () => {
  it("offers the ending line after a rejection or a ghosting only", () => {
    expect(invitesExperience("Rejected")).toBe(true);
    expect(invitesExperience("Ghosted")).toBe(true);
  });

  it("stays quiet while the process is open, after a withdrawal, and after an accepted offer — which has its own card", () => {
    for (const status of [...open, "Accepted", "Withdrawn"] as const) {
      expect(invitesExperience(status)).toBe(false);
    }
  });
});
