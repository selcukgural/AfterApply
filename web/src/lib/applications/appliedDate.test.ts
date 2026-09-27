import { describe, expect, it } from "vitest";
import { appliedAtInputValue } from "./appliedDate";

describe("appliedAtInputValue", () => {
  it("defaults to the local calendar day just after midnight, not the UTC one", () => {
    const justAfterMidnight = new Date(2026, 8, 27, 0, 30);
    expect(appliedAtInputValue(undefined, justAfterMidnight)).toBe("2026-09-27");
    expect(appliedAtInputValue(null, justAfterMidnight)).toBe("2026-09-27");
  });

  it("shows a stored timestamp as the local day the list shows it on", () => {
    const savedAt = new Date(2026, 8, 27, 1, 15);
    expect(appliedAtInputValue(savedAt.toISOString())).toBe("2026-09-27");
  });
});
