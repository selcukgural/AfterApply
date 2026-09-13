import { z } from "zod";
import {
  RATING_MAX,
  RATING_MIN,
  REVIEW_TEXT_MAX_LENGTH,
  REVIEW_TEXT_MIN_LENGTH,
  REVIEW_TITLE_MAX_LENGTH,
  REVIEW_TITLE_MIN_LENGTH,
} from "@/lib/companyReviews/reviewDraft";

/** The same rules as ReviewContentRules on the server, as a zod schema for the request shape. The
 *  form itself validates its draft with validateReviewDraft (per-field keys); this is the belt to
 *  those braces at the point the request is built. */
export function createCompanyReviewSchema(t: (key: string) => string) {
  const rating = z.number().int().min(RATING_MIN, t("ratingRequired")).max(RATING_MAX, t("ratingRequired"));
  return z.object({
    employmentStatus: z.enum(["CurrentEmployee", "FormerEmployee", "Intern"], { message: t("employmentStatusRequired") }),
    title: z.string().trim().min(REVIEW_TITLE_MIN_LENGTH, t("reviewTitleTooShort")).max(REVIEW_TITLE_MAX_LENGTH),
    pros: z.string().trim().min(REVIEW_TEXT_MIN_LENGTH, t("reviewTextTooShort")).max(REVIEW_TEXT_MAX_LENGTH),
    cons: z.string().trim().min(REVIEW_TEXT_MIN_LENGTH, t("reviewTextTooShort")).max(REVIEW_TEXT_MAX_LENGTH),
    overallRating: rating,
    managementRating: rating,
    workEnvironmentRating: rating,
    salaryAndBenefitsRating: rating,
    careerAndDevelopmentRating: rating,
  });
}
