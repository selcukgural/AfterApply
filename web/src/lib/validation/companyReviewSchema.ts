import { z } from "zod";
import { RATING_MAX, RATING_MIN } from "@/lib/companyReviews/reviewDraft";
import { MAX_PICKS_PER_KIND, OPTIONAL_CATEGORIES, findStatement } from "@/lib/companyReviews/statementCatalogue";

/** The same rules as StructuredReviewRules on the server, as a zod schema for the request shape.
 *  The form itself validates its draft with validateReviewDraft (per-field keys); this is the
 *  belt to those braces at the point the request is built. */
export function createCompanyReviewSchema(t: (key: string) => string) {
  const rating = z.number().int().min(RATING_MIN, t("ratingRequired")).max(RATING_MAX, t("ratingRequired"));
  const picks = (kind: "Liked" | "Improve") =>
    z
      .array(z.string().refine((key) => findStatement(key)?.kind === kind, t("reviewStatementUnknown")))
      .max(MAX_PICKS_PER_KIND, t("reviewStatementsTooMany"))
      .refine((keys) => new Set(keys).size === keys.length, t("reviewStatementsTooMany"));
  return z.object({
    employmentStatus: z.enum(["CurrentEmployee", "FormerEmployee", "Intern"], { message: t("employmentStatusRequired") }),
    overallRating: rating,
    categoryRatings: z
      .array(z.object({ category: z.enum(OPTIONAL_CATEGORIES as [string, ...string[]]), rating }))
      .refine((rows) => new Set(rows.map((r) => r.category)).size === rows.length, t("ratingRequired")),
    likedStatements: picks("Liked"),
    improvableStatements: picks("Improve"),
  });
}
