import { describe, expect, it } from "vitest";
import {
  timeInZone,
  zoneCity,
  zonedTimeToInstant,
  zoneQuestionNeeded,
  zonesForCountry,
} from "./timeZones";

describe("zonedTimeToInstant", () => {
  it("reads a wall-clock time in the company's zone, summer and winter", () => {
    expect(zonedTimeToInstant("2026-10-14", "14:00", "Europe/Berlin")).toBe("2026-10-14T12:00:00.000Z");
    expect(zonedTimeToInstant("2026-11-02", "14:00", "Europe/Berlin")).toBe("2026-11-02T13:00:00.000Z");
    expect(zonedTimeToInstant("2026-10-14", "15:00", "Europe/Istanbul")).toBe("2026-10-14T12:00:00.000Z");
  });

  it("lands right on the day the clocks change", () => {
    // New York moves to daylight time at 02:00 on 8 March 2026; noon that day is EDT (UTC-4).
    expect(zonedTimeToInstant("2026-03-08", "12:00", "America/New_York")).toBe("2026-03-08T16:00:00.000Z");
  });

  it("waits for both inputs", () => {
    expect(zonedTimeToInstant("", "14:00", "Europe/Berlin")).toBeNull();
    expect(zonedTimeToInstant("2026-10-14", "", "Europe/Berlin")).toBeNull();
  });
});

describe("timeInZone", () => {
  it("names the time on the company's clocks", () => {
    expect(timeInZone("2026-10-14T12:00:00Z", "Europe/Berlin", "tr")).toBe("14:00");
    expect(timeInZone("2026-10-14T12:00:00Z", "Europe/Istanbul", "tr")).toBe("15:00");
  });
});

describe("zoneQuestionNeeded", () => {
  const october = new Date("2026-10-14T12:00:00Z");

  it("stays quiet when the company keeps the reader's own clock", () => {
    expect(zoneQuestionNeeded(["Europe/Istanbul"], "Europe/Istanbul", october)).toBe(false);
    expect(zoneQuestionNeeded(["Asia/Riyadh"], "Europe/Istanbul", october)).toBe(false); // both UTC+3
  });

  it("asks when the clocks differ, or when the country has several", () => {
    expect(zoneQuestionNeeded(["Europe/Berlin"], "Europe/Istanbul", october)).toBe(true);
    expect(zoneQuestionNeeded(zonesForCountry("US"), "Europe/Istanbul", october)).toBe(true);
  });

  it("has nothing to ask about an unknown country", () => {
    expect(zoneQuestionNeeded([], "Europe/Istanbul", october)).toBe(false);
  });
});

describe("zonesForCountry and zoneCity", () => {
  it("maps a country code to its zones, in any case", () => {
    expect(zonesForCountry("de")).toEqual(["Europe/Berlin"]);
    expect(zonesForCountry("US").length).toBeGreaterThan(1);
    expect(zonesForCountry("XX")).toEqual([]);
    expect(zonesForCountry(null)).toEqual([]);
  });

  it("labels a zone by its city", () => {
    expect(zoneCity("Europe/Berlin")).toBe("Berlin");
    expect(zoneCity("America/New_York")).toBe("New York");
    expect(zoneCity("America/Argentina/Buenos_Aires")).toBe("Buenos Aires");
    expect(zoneCity("Europe/Istanbul", "tr")).toBe("İstanbul");
    expect(zoneCity("Europe/Istanbul", "en")).toBe("Istanbul");
  });
});
