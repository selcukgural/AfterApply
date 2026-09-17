import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";
import { keysFromCSharp, leaves, lookup, type MessageTree } from "@/lib/statements/catalogueParity";
import { CATEGORY_PREFIX, REVIEW_CATEGORIES, STATEMENTS, statementsFor } from "./statementCatalogue";

// Two copies of one list — the C# catalogue the API validates against and this one the form
// offers — and two message catalogues that give every key its wording. `messageUsage.ts`
// cannot follow `t(\`statements.${key}.label\`)`, so this is the test that ties them together.

const CSHARP = fileURLToPath(
  new URL("../../../../src/AfterApply.Domain/CompanyReviews/ReviewStatementCatalogue.cs", import.meta.url),
);

describe("statement catalogue", () => {
  it("has unique keys, each naming its own category and kind", () => {
    const keys = STATEMENTS.map((s) => s.key);
    expect(new Set(keys).size).toBe(keys.length);
    for (const statement of STATEMENTS) {
      const [prefix, segment] = statement.key.split(".");
      expect(prefix).toBe(CATEGORY_PREFIX[statement.category]);
      expect(segment).toBe(statement.kind === "Liked" ? "pos" : "imp");
    }
  });

  it("offers both kinds for every category", () => {
    for (const category of REVIEW_CATEGORIES) {
      expect(statementsFor(category, "Liked").length, category).toBeGreaterThan(0);
      expect(statementsFor(category, "Improve").length, category).toBeGreaterThan(0);
    }
  });

  it("is the same list as the API's", () => {
    expect([...STATEMENTS.map((s) => s.key)].sort()).toEqual(keysFromCSharp(CSHARP, "ReviewCategory", CATEGORY_PREFIX).sort());
  });

  it("has a label and a sentence in both languages for every key", () => {
    for (const locale of [tr, en] as MessageTree[]) {
      for (const statement of STATEMENTS) {
        const entry = lookup(locale, `companyReviews.statements.${statement.key}`);
        expect(entry, statement.key).toBeTypeOf("object");
        const { label, sentence } = entry as { label?: string; sentence?: string };
        expect(label?.trim(), `${statement.key} label`).toBeTruthy();
        expect(sentence?.trim(), `${statement.key} sentence`).toBeTruthy();
      }
    }
  });

  it("has no wording for a statement that does not exist", () => {
    const statements = lookup(en as MessageTree, "companyReviews.statements") as MessageTree;
    const known = new Set(STATEMENTS.map((s) => s.key));
    const orphans = leaves(statements)
      .map((path) => path.replace(/\.(label|sentence)$/, ""))
      .filter((key) => !known.has(key));
    expect([...new Set(orphans)]).toEqual([]);
  });

  it("names every category in both languages", () => {
    for (const locale of [tr, en] as MessageTree[]) {
      for (const category of REVIEW_CATEGORIES) {
        const key = category[0].toLowerCase() + category.slice(1);
        expect(lookup(locale, `companyReviews.categories.${key}`), category).toBeTypeOf("string");
      }
    }
  });
});
