import { describe, expect, it } from "vitest";
import { canSeeProNav } from "./proNav";

describe("canSeeProNav", () => {
  it("needs both the job-sources and the payments flag", () => {
    expect(canSeeProNav({ jobSources: { enabled: true }, payments: { enabled: true } })).toBe(true);
    expect(canSeeProNav({ jobSources: { enabled: true }, payments: { enabled: false } })).toBe(false);
    expect(canSeeProNav({ jobSources: { enabled: false }, payments: { enabled: true } })).toBe(false);
    expect(canSeeProNav({})).toBe(false);
  });
});
