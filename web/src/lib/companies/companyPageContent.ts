import type { CandidateExperiencePublic, CompanyPublicResponse, StageCount } from "@/types/api";

type CompanyCounts = Pick<CompanyPublicResponse, "summary" | "candidateExperienceCount">;

/** The company-page tabs that are switched on. */
export interface CompanyTabFeatures {
  salaries: boolean;
  experiences: boolean;
  intelligence: boolean;
}

/**
 * Which half of a company page opens first, and so which half is in the HTML a crawler reads
 * (2026-10-14). A company with reviews leads with them; one known only for candidate experiences
 * leads with those, so its page does not open on "no reviews yet" — the empty state Google read on
 * every such page while the experience sat one click away.
 */
export type CompanyPageLead = "reviews" | "experiences";

export function companyPageLead(company: CompanyCounts): CompanyPageLead {
  return company.summary.approvedCount === 0 && (company.candidateExperienceCount ?? 0) > 0 ? "experiences" : "reviews";
}

/**
 * A company page asks to be indexed once it has a published review or a public candidate
 * experience — the same rule as the sitemap's slug list. Salaries do not count: they sit behind
 * sign-in, a crawler sees nothing there.
 */
export function isCompanyPageIndexable(company: CompanyCounts): boolean {
  return company.summary.approvedCount > 0 || (company.candidateExperienceCount ?? 0) > 0;
}

/** A translator over the whole message catalogue (no namespace). */
export type RootTranslate = (key: string, values?: Record<string, string | number>) => string;

const STAGE_NUMBER: Record<Exclude<StageCount, "FivePlus">, number> = { One: 1, Two: 2, Three: 3, Four: 4 };

/**
 * The search-snippet facts of one experience — length, rounds, outcome, rating — in the card's
 * own words. Only what the public card already shows; the outcome is lower-cased because it sits
 * mid-sentence.
 */
export function experienceSnippetFacts(experience: CandidateExperiencePublic, t: RootTranslate, locale: string): string[] {
  const stages =
    experience.stages === null
      ? null
      : experience.stages === "FivePlus"
        ? t("candidateExperiences.card.stagesFivePlus")
        : t("candidateExperiences.card.stages", { count: STAGE_NUMBER[experience.stages] });
  return [
    experience.duration ? t(`processDuration.${experience.duration}`) : null,
    stages,
    experience.outcome ? t(`candidateExperiences.summary.outcomeShort.${experience.outcome}`).toLocaleLowerCase(locale) : null,
    t("companies.page.metaRating", { value: experience.overallRating }),
  ].filter((fact): fact is string => fact !== null);
}

/**
 * Title and description of a page that leads with candidate experiences: they say what the page
 * holds instead of promising reviews and salaries it does not show. `latest` is the newest
 * experience (the list is newest first); without it — the server fetch failed — the description
 * names what an experience covers instead of its facts.
 */
export function experiencesPageMeta(
  company: Pick<CompanyPublicResponse, "name" | "candidateExperienceCount">,
  latest: CandidateExperiencePublic | null,
  t: RootTranslate,
  locale: string,
): { title: string; description: string } {
  const values = { company: company.name, count: company.candidateExperienceCount ?? 0 };
  return {
    title: t("companies.page.metaTitleExperiences", { company: company.name }),
    description: latest
      ? t("companies.page.metaDescriptionExperiences", { ...values, facts: experienceSnippetFacts(latest, t, locale).join(", ") })
      : t("companies.page.metaDescriptionExperiencesGeneric", values),
  };
}
