import { describe, expect, it } from "vitest";
import { buildStatementCatalogue } from "./catalogue";
import { isCapReached, suggestionsFor, togglePick, validatePicks } from "./picks";

const catalogue = buildStatementCatalogue(
  ["Overall", "Speed"] as const,
  { Overall: "general", Speed: "speed" },
  { Overall: { pos: ["a", "b"], imp: ["c"] }, Speed: { pos: ["d", "e", "f", "g"], imp: ["h", "i", "j", "k", "l", "m"] } },
);

describe("statement picks", () => {
  it("builds keys from prefix, kind and slug", () => {
    expect(catalogue.statements.map((s) => s.key)).toEqual([
      "general.pos.a", "general.pos.b", "general.imp.c",
      "speed.pos.d", "speed.pos.e", "speed.pos.f", "speed.pos.g",
      "speed.imp.h", "speed.imp.i", "speed.imp.j", "speed.imp.k", "speed.imp.l", "speed.imp.m",
    ]);
    expect(catalogue.optionalCategories).toEqual(["Speed"]);
    expect(catalogue.find("speed.imp.h")?.kind).toBe("Improve");
  });

  it("caps each list at five and refuses unknown keys", () => {
    let draft = { liked: [] as string[], improvable: [] as string[] };
    for (const slug of ["h", "i", "j", "k", "l"]) draft = togglePick(catalogue, draft, `speed.imp.${slug}`);
    expect(isCapReached(draft, "Improve")).toBe(true);
    expect(togglePick(catalogue, draft, "speed.imp.m")).toBe(draft);
    expect(togglePick(catalogue, draft, "speed.imp.zzz")).toBe(draft);
    expect(togglePick(catalogue, draft, "speed.imp.h").improvable).toEqual(["speed.imp.i", "speed.imp.j", "speed.imp.k", "speed.imp.l"]);
  });

  it("suggests three of the matching kind, none without a rating", () => {
    expect(suggestionsFor(catalogue, "Speed", 4).map((s) => s.key)).toEqual(["speed.pos.d", "speed.pos.e", "speed.pos.f"]);
    expect(suggestionsFor(catalogue, "Speed", 1).map((s) => s.key)).toEqual(["speed.imp.h", "speed.imp.i", "speed.imp.j"]);
    expect(suggestionsFor(catalogue, "Speed", 0)).toEqual([]);
  });

  it("reports too many, then unknown, per list", () => {
    expect(validatePicks(catalogue, { liked: ["general.pos.a", "general.pos.b", "speed.pos.d", "speed.pos.e", "speed.pos.f", "speed.pos.g"], improvable: [] }))
      .toEqual({ liked: "tooManyLiked" });
    expect(validatePicks(catalogue, { liked: ["general.imp.c"], improvable: ["nope"] }))
      .toEqual({ liked: "unknownStatement", improvable: "unknownStatement" });
    expect(validatePicks(catalogue, { liked: [], improvable: [] })).toEqual({});
  });
});
