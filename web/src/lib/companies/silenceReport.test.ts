import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";
import {
  EMPTY_SILENCE_REPORT_DRAFT,
  SILENCE_STAGES,
  SILENCE_WAITS,
  buildSilenceReportRequest,
  silenceReportProblems,
} from "./silenceReport";

// The same three-way parity as the benchmark's options: the C# enums the API validates against,
// the lists the card renders, and the labels in both catalogues.
const REPO_ROOT = fileURLToPath(new URL("../../../..", import.meta.url));
const SOURCE = readFileSync(path.join(REPO_ROOT, "src/AfterApply.Domain/SilenceReports/SilenceReport.cs"), "utf8");

function csharpEnum(name: string): string[] {
  const block = new RegExp(`public enum ${name}\\s*\\{([^}]*)\\}`).exec(SOURCE);
  expect(block, `enum ${name} not found in SilenceReport.cs`).not.toBeNull();
  return block![1]
    .split(",")
    .map((member) => member.replace(/\/\/.*$/gm, "").trim())
    .filter(Boolean);
}

type Tree = Record<string, unknown>;
const labels = (catalogue: Tree, group: string) =>
  ((catalogue.companies as Tree).silenceReport as Tree)[group] as Record<string, string>;

describe("silence report options", () => {
  it.each([
    { name: "SilenceStage", values: SILENCE_STAGES, group: "stages" },
    { name: "SilenceWait", values: SILENCE_WAITS, group: "waits" },
  ])("$name matches the C# enum in order and is labelled in both languages", ({ name, values, group }) => {
    expect([...values]).toEqual(csharpEnum(name));
    for (const catalogue of [tr, en] as Tree[]) {
      for (const value of values) {
        expect(labels(catalogue, group)[value], `${group}.${value}`).toBeTruthy();
      }
    }
  });
});

describe("silenceReportProblems", () => {
  it("asks for the stage and the wait, and nothing else", () => {
    expect(silenceReportProblems(EMPTY_SILENCE_REPORT_DRAFT)).toEqual(["stageRequired", "waitRequired"]);
    expect(silenceReportProblems({ stage: "AfterHrScreen", wait: "", promise: "" })).toEqual(["waitRequired"]);
    expect(silenceReportProblems({ stage: "AfterHrScreen", wait: "OneToTwoMonths", promise: "" })).toEqual([]);
  });
});

describe("buildSilenceReportRequest", () => {
  const draft = { stage: "AfterTechnicalInterview", wait: "TwoToThreeMonths", promise: "" } as const;

  it("sends 'not said' as null and the honeypot empty", () => {
    expect(buildSilenceReportRequest(draft, "tr", "")).toEqual({
      stage: "AfterTechnicalInterview",
      wait: "TwoToThreeMonths",
      promiseGiven: null,
      locale: "tr",
      website: "",
      source: null,
    });
  });

  it("maps the promise answer and the campaign channel", () => {
    expect(buildSilenceReportRequest({ ...draft, promise: "yes" }, "en", "?utm_source=eksi").promiseGiven).toBe(true);
    expect(buildSilenceReportRequest({ ...draft, promise: "no" }, "en", "").promiseGiven).toBe(false);
    expect(buildSilenceReportRequest(draft, "en", "?utm_source=eksi").source).toBe("Eksi");
    expect(buildSilenceReportRequest(draft, "en", "?utm_source=somewhere").source).toBeNull();
  });

  it("only ever sends a locale the API accepts", () => {
    expect(buildSilenceReportRequest(draft, "de", "").locale).toBe("en");
  });
});
