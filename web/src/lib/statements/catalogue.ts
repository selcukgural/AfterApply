import type { ReviewStatementKind } from "@/types/api";

// The shape shared by every closed vocabulary a form picks from — the company review catalogue
// and the candidate experience catalogue today. Each mirrors a C# class the API validates
// against; a parity test per catalogue reads that source and fails if the two drift. Keys are
// `{prefix}.{pos|imp}.{slug}` and permanent; the wording lives under
// `<namespace>.statements.<key>.{label,sentence}` in the message catalogue, so the legal text
// can change without touching a stored row.

export interface Statement<C extends string = string> {
  key: string;
  category: C;
  kind: ReviewStatementKind;
}

export interface StatementCatalogue<C extends string> {
  /** Overall first (required), then the optional categories in form order. */
  categories: readonly C[];
  optionalCategories: readonly C[];
  prefix: Record<C, string>;
  statements: readonly Statement<C>[];
  find(key: string): Statement<C> | undefined;
  for(category: C, kind: ReviewStatementKind): Statement<C>[];
}

/** Per form, per kind — the server refuses more. */
export const MAX_PICKS_PER_KIND = 5;

/** How many statements a rated row offers before "show more". */
export const SUGGESTION_COUNT = 3;

export const RATING_MIN = 1;
export const RATING_MAX = 5;

export function isOnScale(value: number | undefined): value is number {
  return Number.isInteger(value) && (value as number) >= RATING_MIN && (value as number) <= RATING_MAX;
}

export function buildStatementCatalogue<C extends string>(
  categories: readonly C[],
  prefix: Record<C, string>,
  slugs: Record<C, { pos: readonly string[]; imp: readonly string[] }>,
): StatementCatalogue<C> {
  const statements: readonly Statement<C>[] = categories.flatMap((category) => [
    ...slugs[category].pos.map((slug) => ({ key: `${prefix[category]}.pos.${slug}`, category, kind: "Liked" as const })),
    ...slugs[category].imp.map((slug) => ({ key: `${prefix[category]}.imp.${slug}`, category, kind: "Improve" as const })),
  ]);
  const byKey = new Map(statements.map((s) => [s.key, s]));
  return {
    categories,
    optionalCategories: categories.filter((c) => c !== "Overall"),
    prefix,
    statements,
    find: (key) => byKey.get(key),
    for: (category, kind) => statements.filter((s) => s.category === category && s.kind === kind),
  };
}

/** `<namespace>.categories.<key>` — the enum name with a lower-case first letter. */
export function categoryMessageKey(category: string): string {
  return category[0].toLowerCase() + category.slice(1);
}
