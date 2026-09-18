import { describe, expect, it } from "vitest";
import { invitesExperience } from "./shareExperience";

describe("invitesExperience", () => {
  it("offers the form after the two endings the candidate did not choose", () => {
    expect(invitesExperience("Rejected")).toBe(true);
    expect(invitesExperience("Ghosted")).toBe(true);
  });

  it("stays quiet while the process is open, and after a withdrawal or an accepted offer", () => {
    for (const status of ["Applied", "Screening", "Interview", "TechnicalInterview", "FinalInterview", "Offer", "Accepted", "Withdrawn"] as const) {
      expect(invitesExperience(status)).toBe(false);
    }
  });
});
