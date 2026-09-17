import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";
import { keysFromCSharp, leaves, lookup, type MessageTree } from "@/lib/statements/catalogueParity";
import { STATEMENTS as REVIEW_STATEMENTS } from "@/lib/companyReviews/statementCatalogue";
import {
  EXPERIENCE_CATALOGUE,
  EXPERIENCE_CATEGORIES,
  EXPERIENCE_CATEGORY_PREFIX,
  HIRING_OUTCOMES,
  INTERVIEW_TYPES,
  PROCESS_DURATIONS,
  STAGE_COUNTS,
} from "./statementCatalogue";

// Same contract as the review catalogue's test: the C# list the API validates against, this list
// the form offers, and two message catalogues that give every key its wording.

const CSHARP = fileURLToPath(
  new URL("../../../../src/AfterApply.Domain/CandidateExperiences/ExperienceStatementCatalogue.cs", import.meta.url),
);

const STATEMENTS = EXPERIENCE_CATALOGUE.statements;

describe("experience statement catalogue", () => {
  it("has unique keys, each naming its own category and kind", () => {
    const keys = STATEMENTS.map((s) => s.key);
    expect(new Set(keys).size).toBe(keys.length);
    for (const statement of STATEMENTS) {
      const [prefix, segment] = statement.key.split(".");
      expect(prefix).toBe(EXPERIENCE_CATEGORY_PREFIX[statement.category]);
      expect(segment).toBe(statement.kind === "Liked" ? "pos" : "imp");
    }
  });

  it("offers both kinds for every category", () => {
    for (const category of EXPERIENCE_CATEGORIES) {
      expect(EXPERIENCE_CATALOGUE.for(category, "Liked").length, category).toBeGreaterThan(0);
      expect(EXPERIENCE_CATALOGUE.for(category, "Improve").length, category).toBeGreaterThan(0);
    }
  });

  it("is the same list as the API's", () => {
    expect([...STATEMENTS.map((s) => s.key)].sort()).toEqual(keysFromCSharp(CSHARP, "ExperienceCategory", EXPERIENCE_CATEGORY_PREFIX).sort());
  });

  it("shares no key with the review catalogue", () => {
    const reviewKeys = new Set(REVIEW_STATEMENTS.map((s) => s.key));
    expect(STATEMENTS.filter((s) => reviewKeys.has(s.key))).toEqual([]);
  });

  it("has a label and a sentence in both languages for every key", () => {
    for (const locale of [tr, en] as MessageTree[]) {
      for (const statement of STATEMENTS) {
        const entry = lookup(locale, `candidateExperiences.statements.${statement.key}`);
        expect(entry, statement.key).toBeTypeOf("object");
        const { label, sentence } = entry as { label?: string; sentence?: string };
        expect(label?.trim(), `${statement.key} label`).toBeTruthy();
        expect(sentence?.trim(), `${statement.key} sentence`).toBeTruthy();
      }
    }
  });

  it("has no wording for a statement that does not exist", () => {
    const statements = lookup(en as MessageTree, "candidateExperiences.statements") as MessageTree;
    const known = new Set(STATEMENTS.map((s) => s.key));
    const orphans = leaves(statements)
      .map((path) => path.replace(/\.(label|sentence)$/, ""))
      .filter((key) => !known.has(key));
    expect([...new Set(orphans)]).toEqual([]);
  });

  it("names every category and every fact value in both languages", () => {
    for (const locale of [tr, en] as MessageTree[]) {
      for (const category of EXPERIENCE_CATEGORIES) {
        const key = category[0].toLowerCase() + category.slice(1);
        expect(lookup(locale, `candidateExperiences.categories.${key}`), category).toBeTypeOf("string");
      }
      for (const value of HIRING_OUTCOMES) expect(lookup(locale, `hiringOutcome.${value}`), value).toBeTypeOf("string");
      for (const value of PROCESS_DURATIONS) expect(lookup(locale, `processDuration.${value}`), value).toBeTypeOf("string");
      for (const value of STAGE_COUNTS) expect(lookup(locale, `stageCount.${value}`), value).toBeTypeOf("string");
      for (const value of INTERVIEW_TYPES) expect(lookup(locale, `interviewType.${value}`), value).toBeTypeOf("string");
    }
  });
});
