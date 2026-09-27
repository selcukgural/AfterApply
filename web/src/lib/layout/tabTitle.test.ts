import { describe, expect, it } from "vitest";
import type { UpcomingInterviewResponse } from "@/types/api";
import { pendingCount, titleWithCount } from "./tabTitle";

describe("titleWithCount", () => {
  it("prefixes the count while something is waiting", () => {
    expect(titleWithCount("Başvurularım | e-kariyerim", 2)).toBe("(2) Başvurularım | e-kariyerim");
  });

  it("replaces an existing prefix instead of stacking one", () => {
    expect(titleWithCount("(2) Başvurularım", 3)).toBe("(3) Başvurularım");
    expect(titleWithCount("(99+) Başvurularım", 1)).toBe("(1) Başvurularım");
  });

  it("strips the prefix when nothing is waiting", () => {
    expect(titleWithCount("(2) Başvurularım", 0)).toBe("Başvurularım");
    expect(titleWithCount("Başvurularım", 0)).toBe("Başvurularım");
  });

  it("caps a large count", () => {
    expect(titleWithCount("e-kariyerim", 140)).toBe("(99+) e-kariyerim");
  });

  it("leaves a title that merely starts with a parenthesis alone", () => {
    expect(titleWithCount("(Beta) e-kariyerim", 0)).toBe("(Beta) e-kariyerim");
  });
});

describe("pendingCount", () => {
  const now = new Date(2026, 8, 27, 9, 0);
  const interview = (at: Date): UpcomingInterviewResponse => ({
    applicationId: at.toISOString(),
    companyName: "Acme",
    jobTitle: "Dev",
    status: "Interview",
    interviewAt: at.toISOString(),
    format: "Online",
  });

  it("adds today's interviews to the due reminders", () => {
    const interviews = [interview(new Date(2026, 8, 27, 14, 0)), interview(new Date(2026, 8, 28, 10, 0))];
    expect(pendingCount(2, interviews, now)).toBe(3);
  });

  it("is zero when nothing is due", () => {
    expect(pendingCount(0, [], now)).toBe(0);
  });
});
