import type {
  ApplicationStatusHistoryResponse,
  EmploymentType,
  OccupationRef,
  ProcessDuration,
} from "@/types/api";
import { EMPTY_EXPERIENCE_DRAFT, type ExperienceDraft } from "@/lib/candidateExperiences/experienceDraft";
import { EMPTY_SALARY_DRAFT, type SalaryDraft } from "@/lib/companySalaries/salaryDraft";

// The accepted-offer card's exit contribution (growth research 2026-09-21, item 1.2; design
// canvas "Son hâl — A", 2026-09-23): the hiring process and the salary, asked once, on the card,
// with everything the application already knows filled in. The two sections are the existing
// candidate experience and salary entries — nothing new on the server — so the rules here only
// decide what the card shows and what it fills in for the person.

/** Which parts of the card are on screen. A section is offered while the feature is on, the
 *  company has a public page, the person has not already left that kind of entry for it and has
 *  quota left; `done` is the card after both sides have something on record (or nothing left to
 *  offer), where the only lines left are "saved" and the data download. */
export interface ExitSections {
  experience: boolean;
  salary: boolean;
}

export interface ExitSectionInputs {
  experiencesEnabled: boolean;
  salariesEnabled: boolean;
  hasCompanyPage: boolean;
  /** Still loading reads as "don't offer yet" — the card never offers something the person may
   *  already have. */
  experienceViewer: { hasOwn: boolean; quotaLeft: number } | undefined;
  salaryViewer: { hasOwn: boolean; quotaLeft: number } | undefined;
}

export function exitSections(input: ExitSectionInputs): ExitSections {
  const offer = (enabled: boolean, viewer: { hasOwn: boolean; quotaLeft: number } | undefined) =>
    enabled && input.hasCompanyPage && viewer !== undefined && !viewer.hasOwn && viewer.quotaLeft > 0;
  return {
    experience: offer(input.experiencesEnabled, input.experienceViewer),
    salary: offer(input.salariesEnabled, input.salaryViewer),
  };
}

/** When the offer was accepted: the latest move to Accepted in the history, or null when the
 *  history has none (an application created straight as Accepted). */
export function acceptedAt(history: readonly ApplicationStatusHistoryResponse[]): string | null {
  let latest: string | null = null;
  for (const entry of history) {
    if (entry.toStatus !== "Accepted") continue;
    if (latest === null || Date.parse(entry.changedAt) > Date.parse(latest)) latest = entry.changedAt;
  }
  return latest;
}

const DAY_MS = 24 * 60 * 60 * 1000;

/** The experience form's duration band for the time from applying to accepting. Null when either
 *  end is unknown or the order is impossible — "not said" beats a guessed band. */
export function processDurationBetween(appliedAt: string, accepted: string | null): ProcessDuration | null {
  if (accepted === null) return null;
  const start = Date.parse(appliedAt);
  const end = Date.parse(accepted);
  if (Number.isNaN(start) || Number.isNaN(end) || end < start) return null;
  const days = Math.floor((end - start) / DAY_MS);
  if (days < 7) return "UnderOneWeek";
  if (days < 14) return "OneToTwoWeeks";
  if (days <= 30) return "TwoToFourWeeks";
  if (days <= 61) return "OneToTwoMonths";
  return "OverTwoMonths";
}

/** The experience section's starting point: the outcome is an offer by definition, and the
 *  duration comes from the application's own dates. The rating and the rest are the person's. */
export function exitExperienceDraft(appliedAt: string, accepted: string | null): ExperienceDraft {
  return {
    ...EMPTY_EXPERIENCE_DRAFT,
    outcome: "Offer",
    duration: processDurationBetween(appliedAt, accepted) ?? "",
  };
}

/** The salary section's starting point: the working arrangement from the application, "still
 *  drawing it" since the year the offer was accepted. An accepted offer is recorded as the
 *  salary of a current employee — the entry model has no separate "offer" kind, on purpose
 *  (DECISIONS.md 2026-09-23). The occupation starts as the job title typed in; only an exact
 *  catalogue match is picked for the person (`exactOccupationMatch`). */
export function exitSalaryDraft(
  jobTitle: string,
  employmentType: EmploymentType,
  accepted: string | null,
  currentYear = new Date().getFullYear(),
): SalaryDraft {
  const acceptedYear = accepted === null ? NaN : new Date(accepted).getFullYear();
  return {
    ...EMPTY_SALARY_DRAFT,
    occupationLabel: jobTitle.trim(),
    employmentType,
    employmentStatus: "CurrentEmployee",
    periodStartYear: String(Number.isNaN(acceptedYear) || acceptedYear > currentYear ? currentYear : acceptedYear),
  };
}

function fold(text: string): string {
  return text.trim().replace(/\s+/g, " ").toLocaleLowerCase("tr");
}

/** The catalogue row whose name, in either language, is the job title itself — and only that.
 *  "Backend Developer" picks the curated "Backend Developer"; "Senior Backend Developer" picks
 *  nothing and leaves the choice to the person, because a near match is a guess at their job. */
export function exactOccupationMatch(jobTitle: string, results: readonly OccupationRef[]): OccupationRef | null {
  const wanted = fold(jobTitle);
  if (wanted === "") return null;
  const matches = results.filter((o) => fold(o.nameTr) === wanted || fold(o.nameEn) === wanted);
  return matches.length === 1 ? matches[0] : null;
}

/** Whether the person has put anything into a section. Pre-filled values do not count: a
 *  section they never touched is skipped, not refused for its empty rating. */
export function experienceTouched(draft: ExperienceDraft): boolean {
  return draft.overall > 0 || draft.stages !== "" || draft.interviewTypes.length > 0;
}

export function salaryTouched(draft: SalaryDraft): boolean {
  return draft.monthlyNetAmount.trim() !== "" || draft.yearsOfExperience.trim() !== "" || draft.hasBonus !== null;
}
