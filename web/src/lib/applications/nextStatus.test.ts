import { describe, expect, it } from "vitest";
import { suggestedNextStatus } from "./nextStatus";

describe("suggestedNextStatus", () => {
  it("opens on the next step forward", () => {
    expect(suggestedNextStatus("Applied")).toBe("Screening");
    expect(suggestedNextStatus("Interview")).toBe("TechnicalInterview");
    expect(suggestedNextStatus("FinalInterview")).toBe("Offer");
    expect(suggestedNextStatus("Offer")).toBe("Accepted");
  });

  it("never opens on the current status", () => {
    for (const status of ["Accepted", "Rejected", "Withdrawn", "Ghosted"] as const) {
      expect(suggestedNextStatus(status)).not.toBe(status);
    }
  });

  it("keeps the old first-other default for an ended process", () => {
    expect(suggestedNextStatus("Rejected")).toBe("Applied");
    expect(suggestedNextStatus("Applied")).not.toBe("Applied");
  });
});
