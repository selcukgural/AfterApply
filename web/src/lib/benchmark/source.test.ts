import { describe, expect, it } from "vitest";
import { benchmarkSourceFromSearch } from "./source";

describe("benchmarkSourceFromSearch", () => {
  it.each([
    ["?utm_source=x", "X"],
    ["?utm_source=twitter", "X"],
    ["?utm_source=Eksi", "Eksi"],
    ["?utm_source=eksisozluk&utm_medium=entry", "Eksi"],
    ["?utm_source=reddit", "Reddit"],
    ["?utm_source=discord", "Discord"],
    ["?utm_source=LINKEDIN", "LinkedIn"],
    ["?utm_source=whatsapp", "WhatsApp"],
    ["?utm_source=share", "Share"],
  ])("maps %s onto %s", (search, expected) => {
    expect(benchmarkSourceFromSearch(search)).toBe(expected);
  });

  it.each(["", "?utm_medium=x", "?utm_source=", "?utm_source=facebook", "?utm_source=constructor", "?utm_source=__proto__", "?utm_source=%3Cscript%3E"])(
    "sends no channel for %j",
    (search) => {
      expect(benchmarkSourceFromSearch(search)).toBeNull();
    },
  );
});
