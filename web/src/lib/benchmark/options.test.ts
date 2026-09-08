import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";
import {
  BENCHMARK_LOCATIONS,
  BENCHMARK_PERIODS,
  BENCHMARK_SECTORS,
  BENCHMARK_SENIORITIES,
} from "./options";

// The benchmark form's options exist in three places that nothing keeps in step: the C# enums the
// API validates against, the lists this app renders, and the two message catalogues that label
// them. A value missing from the first is a 400 the visitor reads as "could not be saved"; one
// missing from the third renders as the raw key. Both are invisible until someone picks that exact
// option, which for a rarely-chosen sector could be weeks.

const REPO_ROOT = fileURLToPath(new URL("../../../..", import.meta.url));
const ENUMS = readFileSync(
  path.join(REPO_ROOT, "src/AfterApply.Domain/Benchmark/BenchmarkEnums.cs"),
  "utf8",
);

/** Members of one `public enum X { ... }` block in the C# source. */
function csharpEnum(name: string): string[] {
  const block = new RegExp(`public enum ${name}\\s*\\{([^}]*)\\}`).exec(ENUMS);
  expect(block, `enum ${name} not found in BenchmarkEnums.cs`).not.toBeNull();
  return block![1]
    .split(",")
    .map((member) => member.replace(/\/\/.*$/gm, "").trim())
    .filter(Boolean);
}

const GROUPS = [
  { name: "BenchmarkSector", values: BENCHMARK_SECTORS, messages: "sectors" },
  { name: "BenchmarkPeriod", values: BENCHMARK_PERIODS, messages: "periods" },
  { name: "BenchmarkSeniority", values: BENCHMARK_SENIORITIES, messages: "seniorities" },
  { name: "BenchmarkLocation", values: BENCHMARK_LOCATIONS, messages: "locations" },
] as const;

describe("benchmark options", () => {
  it.each(GROUPS)("offers exactly what the API accepts for $name", ({ name, values }) => {
    // Both directions. An extra option here is a 400 waiting to happen; a missing one is a choice
    // the API supports that nobody can pick.
    expect([...values].sort()).toEqual([...csharpEnum(name)].sort());
  });

  it.each(GROUPS)("labels every $name option in both languages", ({ values, messages }) => {
    for (const locale of [
      { name: "tr", catalogue: tr.benchmark as unknown as Record<string, Record<string, string>> },
      { name: "en", catalogue: en.benchmark as unknown as Record<string, Record<string, string>> },
    ]) {
      for (const value of values) {
        expect(
          locale.catalogue[messages]?.[value],
          `${messages}.${value} is offered but has no ${locale.name} label`,
        ).toBeTruthy();
      }
    }
  });

  it("keeps the sectors in a deliberate order rather than alphabetical", () => {
    // The list is ordered by how likely a visitor is to want it, with the catch-all last. An
    // alphabetical sort would be the sign someone "tidied" it and buried the beachhead.
    expect(BENCHMARK_SECTORS[0]).toBe("SoftwareAndIt");
    expect(BENCHMARK_SECTORS.at(-1)).toBe("Other");
  });
});
