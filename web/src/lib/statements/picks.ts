import type { ReviewStatementKind } from "@/types/api";
import { MAX_PICKS_PER_KIND, SUGGESTION_COUNT, isOnScale, type Statement, type StatementCatalogue } from "./catalogue";

/** The two pick lists every statement-based draft carries: catalogue keys in pick order. */
export interface PickLists {
  liked: string[];
  improvable: string[];
}

export function picksOf(draft: PickLists, kind: ReviewStatementKind): string[] {
  return kind === "Liked" ? draft.liked : draft.improvable;
}

export function isCapReached(draft: PickLists, kind: ReviewStatementKind): boolean {
  return picksOf(draft, kind).length >= MAX_PICKS_PER_KIND;
}

/** Adds or removes one statement in its own list. Refuses a pick past the cap or a key the
 *  catalogue does not know, returning the draft unchanged so the caller can render as is. */
export function togglePick<T extends PickLists>(catalogue: StatementCatalogue<string>, draft: T, key: string): T {
  const statement = catalogue.find(key);
  if (!statement) return draft;
  const list = picksOf(draft, statement.kind);
  const next = list.includes(key)
    ? list.filter((k) => k !== key)
    : list.length >= MAX_PICKS_PER_KIND
      ? list
      : [...list, key];
  if (next === list) return draft;
  return statement.kind === "Liked" ? { ...draft, liked: next } : { ...draft, improvable: next };
}

/** What a rated row offers first: 4–5 stars lead with what was liked, 1–3 with what could be
 *  better. Nothing until there is a rating — the form is star rows until then. */
export function suggestionsFor<C extends string>(catalogue: StatementCatalogue<C>, category: C, rating: number): Statement<C>[] {
  if (!isOnScale(rating)) return [];
  return catalogue.for(category, rating >= 4 ? "Liked" : "Improve").slice(0, SUGGESTION_COUNT);
}

/** The pick-list problems a draft can have, shared by every form that picks statements. */
export function validatePicks(catalogue: StatementCatalogue<string>, draft: PickLists): { liked?: PickProblem; improvable?: PickProblem } {
  const problems: { liked?: PickProblem; improvable?: PickProblem } = {};
  if (draft.liked.length > MAX_PICKS_PER_KIND) problems.liked = "tooManyLiked";
  if (draft.improvable.length > MAX_PICKS_PER_KIND) problems.improvable = "tooManyImprovable";
  if (draft.liked.some((key) => catalogue.find(key)?.kind !== "Liked")) problems.liked = "unknownStatement";
  if (draft.improvable.some((key) => catalogue.find(key)?.kind !== "Improve")) problems.improvable = "unknownStatement";
  return problems;
}

export type PickProblem = "tooManyLiked" | "tooManyImprovable" | "unknownStatement";
