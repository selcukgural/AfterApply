"use client";

import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { useClientConfig } from "@/hooks/useClientConfig";
import { candidateExperiencesApi } from "@/lib/api/candidateExperiences";
import { companySalariesApi } from "@/lib/api/companySalaries";
import { occupationsApi } from "@/lib/api/occupations";
import { ApiError } from "@/lib/api/httpClient";
import { closingMoment } from "@/lib/applications/shareExperience";
import {
  acceptedAt,
  exactOccupationMatch,
  exitExperienceDraft,
  exitSalaryDraft,
  exitSections,
  experienceTouched,
  salaryTouched,
  type ExitSections,
} from "@/lib/applications/acceptedExit";
import {
  buildExperienceRequest,
  toggleInterviewType,
  validateExperienceDraft,
  type ExperienceDraft,
  type ExperienceDraftField,
  type ExperienceDraftProblem,
} from "@/lib/candidateExperiences/experienceDraft";
import { INTERVIEW_TYPES, STAGE_COUNTS } from "@/lib/candidateExperiences/statementCatalogue";
import {
  SALARY_CURRENCIES,
  buildSalaryRequest,
  occupationName,
  occupationOtherName,
  validateSalaryDraft,
  type SalaryDraft,
  type SalaryDraftField,
  type SalaryDraftProblem,
} from "@/lib/companySalaries/salaryDraft";
import type { ApplicationStatus, ApplicationStatusHistoryResponse, EmploymentType, SalaryCurrency } from "@/types/api";
import { Button } from "@/components/ui/Button";
import { Combobox } from "@/components/ui/Combobox";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { RatingInput } from "@/components/companyReviews/RatingInput";
import { FactCheckPills, FactPills } from "@/components/candidateExperiences/FactPills";

interface AcceptedClosingNoteProps {
  status: ApplicationStatus;
  companyId: string;
  companyName: string;
  companySlug: string | null | undefined;
  jobTitle: string;
  employmentType: EmploymentType;
  appliedAt: string;
  /** Undefined while loading: the form waits for it, since the process duration comes from it. */
  statusHistory: readonly ApplicationStatusHistoryResponse[] | undefined;
}

const RADIO_CLASSES = "h-4 w-4 border-gray-300 text-accent focus:ring-accent dark:border-gray-600 dark:bg-gray-900";
const LINK_CLASSES = "font-medium text-gray-900 hover:underline dark:text-gray-100";

/**
 * The card an application carries once its offer is accepted (DEVELOPMENT_PLAN.md, T-series,
 * T7): a congratulation, the hiring process and the salary asked once, right here, for the next
 * candidate (growth research 2026-09-21 item 1.2, canvas "Son hâl — A", 2026-09-23), and the
 * reminder that the data is theirs to download whenever they like while the account waits. Job
 * searching is episodic — this person will search again in a couple of years, and the place
 * they come back to should be the one that let them leave well.
 *
 * The two sections are the ordinary candidate experience and salary entries, cut down to what
 * the application does not already know; the full forms stay one link away. A section the
 * person has already filled for this company is not offered, and once something is saved the
 * card says so and that there is nothing left to enter. No company review here: that form asks
 * whether they work there, and on the day of an accepted offer they do not yet.
 *
 * Nothing here points at account deletion, on purpose. Same tone rules as the rest of the
 * series: no exclamation mark, no accent colour on the card itself.
 */
export function AcceptedClosingNote(props: AcceptedClosingNoteProps) {
  const { status, companyId, companyName, companySlug } = props;
  const t = useTranslations("applications.detail.accepted");
  const { config } = useClientConfig();
  const accepted = closingMoment(status) === "accepted";
  const experiencesEnabled = accepted && config.candidateExperiences?.enabled === true && !!companySlug;
  const salariesEnabled = accepted && config.companySalaries?.enabled === true && !!companySlug;

  const experienceViewer = useQuery({
    queryKey: ["companies", companyId, "experienceViewer"],
    queryFn: () => candidateExperiencesApi.viewerState(companyId),
    enabled: experiencesEnabled,
  });
  const salaryViewer = useQuery({
    queryKey: ["companies", companyId, "salaryViewer"],
    queryFn: () => companySalariesApi.viewerState(companyId),
    enabled: salariesEnabled,
  });

  // What this visit saved, for the "saved" line — the viewer queries say what exists, not what
  // just happened.
  const [saved, setSaved] = useState<ExitSections>({ experience: false, salary: false });

  if (!accepted) return null;

  const sections = exitSections({
    experiencesEnabled,
    salariesEnabled,
    hasCompanyPage: !!companySlug,
    experienceViewer: experienceViewer.data && {
      hasOwn: experienceViewer.data.ownEntry !== null,
      quotaLeft: experienceViewer.data.quota.limit - experienceViewer.data.quota.used,
    },
    salaryViewer: salaryViewer.data && {
      hasOwn: salaryViewer.data.ownEntries.length > 0,
      quotaLeft: salaryViewer.data.quota.limit - salaryViewer.data.quota.used,
    },
  });
  const savedAny = saved.experience || saved.salary;
  const offering = sections.experience || sections.salary;
  // A later visit to a card whose entries are already on record says the same as the moment
  // after saving: nothing left to enter here.
  const onRecord = savedAny || !!experienceViewer.data?.ownEntry || (salaryViewer.data?.ownEntries.length ?? 0) > 0;

  return (
    <section className="flex flex-col gap-4 rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex flex-col gap-1.5">
        <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
        {offering && !savedAny && (
          <p className="text-sm text-gray-600 dark:text-gray-400">
            {sections.experience && sections.salary ? t("intro.both") : sections.experience ? t("intro.experience") : t("intro.salary")}
          </p>
        )}
        {savedAny && companySlug && (
          <p className="text-sm text-gray-600 dark:text-gray-400">
            {saved.experience && saved.salary ? t("saved.both") : saved.experience ? t("saved.experience") : t("saved.salary")}{" "}
            <Link href={`/companies/${encodeURIComponent(companySlug)}`} className={LINK_CLASSES}>
              {t("saved.link", { company: companyName })}
            </Link>
          </p>
        )}
      </div>

      {offering && companySlug && props.statusHistory !== undefined && (
        <ExitContributionForm
          {...props}
          companySlug={companySlug}
          statusHistory={props.statusHistory}
          sections={sections}
          onSaved={(which) => setSaved((prev) => ({ ...prev, ...which }))}
        />
      )}

      <p className="text-sm text-gray-600 dark:text-gray-400">
        {onRecord && !offering ? t("keep.doneText") : t("keep.text")}{" "}
        <Link href="/settings#export" className={LINK_CLASSES}>
          {t("keep.link")}
        </Link>
      </p>
    </section>
  );
}

function ExitContributionForm({
  companyId,
  companySlug,
  jobTitle,
  employmentType,
  appliedAt,
  statusHistory,
  sections,
  onSaved,
}: AcceptedClosingNoteProps & {
  companySlug: string;
  statusHistory: readonly ApplicationStatusHistoryResponse[];
  sections: ExitSections;
  onSaved: (which: Partial<ExitSections>) => void;
}) {
  const t = useTranslations("applications.detail.accepted");
  const tExperience = useTranslations("candidateExperiences.form");
  const tSalary = useTranslations("companySalaries.form");
  const tStages = useTranslations("stageCount");
  const tTypes = useTranslations("interviewType");
  const tOutcome = useTranslations("hiringOutcome");
  const tDuration = useTranslations("processDuration");
  const tEmploymentType = useTranslations("employmentType");
  const tCurrency = useTranslations("salaryCurrency");
  const locale = useLocale();
  const queryClient = useQueryClient();

  const [experience, setExperience] = useState<ExperienceDraft>(() => exitExperienceDraft(appliedAt, acceptedAt(statusHistory)));
  const [salary, setSalary] = useState<SalaryDraft>(() => exitSalaryDraft(jobTitle, employmentType, acceptedAt(statusHistory)));
  const [experienceProblems, setExperienceProblems] = useState<Partial<Record<ExperienceDraftField, ExperienceDraftProblem>>>({});
  const [salaryProblems, setSalaryProblems] = useState<Partial<Record<SalaryDraftField, SalaryDraftProblem>>>({});
  const [formError, setFormError] = useState<string | null>(null);

  // The job title, searched once: an exact catalogue name is picked for the person, anything
  // else stays typed in the box for them to pick from.
  const titleSearch = useQuery({
    queryKey: ["occupations", "search", jobTitle.trim()],
    queryFn: () => occupationsApi.search(jobTitle.trim()),
    enabled: sections.salary && jobTitle.trim().length >= 2,
    staleTime: Infinity,
  });
  // Until the person touches the box, an exact match stands in for the typed title. Derived, not
  // copied into the draft, so a search that answers late cannot overwrite what they typed.
  const [occupationTouched, setOccupationTouched] = useState(false);
  const match = occupationTouched || !titleSearch.data ? null : exactOccupationMatch(jobTitle, titleSearch.data);
  const effectiveSalary: SalaryDraft = match ? { ...salary, occupationId: match.id, occupationLabel: occupationName(match, locale) } : salary;

  const errorText = (err: unknown) => (err instanceof ApiError ? err.message : t("error"));

  const save = useMutation({
    mutationFn: async ({ sendExperience, sendSalary }: { sendExperience: boolean; sendSalary: boolean }) => {
      // One after the other, so a failure on the second leaves the first on record and said so.
      if (sendExperience) {
        await candidateExperiencesApi.create(companyId, buildExperienceRequest(experience));
        onSaved({ experience: true });
      }
      if (sendSalary) {
        await companySalariesApi.create(companyId, buildSalaryRequest(effectiveSalary));
        onSaved({ salary: true });
      }
    },
    onError: (err) => setFormError(errorText(err)),
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ["contributions", "mine"] }),
        queryClient.invalidateQueries({ queryKey: ["candidateExperiences", "mine"] }),
        queryClient.invalidateQueries({ queryKey: ["companySalaries", "mine"] }),
        queryClient.invalidateQueries({ queryKey: ["companies", companyId, "experienceViewer"] }),
        queryClient.invalidateQueries({ queryKey: ["companies", companyId, "salaryViewer"] }),
        queryClient.invalidateQueries({ queryKey: ["companies", companyId, "salaries"] }),
        queryClient.invalidateQueries({ queryKey: ["companies", companySlug, "experiences"] }),
        queryClient.invalidateQueries({ queryKey: ["companies", "public", companySlug] }),
      ]),
  });

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);
    const sendExperience = sections.experience && experienceTouched(experience);
    const sendSalary = sections.salary && salaryTouched(effectiveSalary);
    const foundExperience = sendExperience ? validateExperienceDraft(experience) : {};
    const foundSalary = sendSalary ? validateSalaryDraft(effectiveSalary) : {};
    setExperienceProblems(foundExperience);
    setSalaryProblems(foundSalary);

    if (!sendExperience && !sendSalary) {
      setFormError(t("nothingToSave"));
      return;
    }
    if (Object.keys(foundExperience).length > 0 || Object.keys(foundSalary).length > 0) return;

    try {
      await save.mutateAsync({ sendExperience, sendSalary });
    } catch {
      // Already on screen through onError.
    }
  };

  const experienceProblem = (field: ExperienceDraftField) => {
    const problem = experienceProblems[field];
    return problem ? tExperience(`problems.${problem}`) : undefined;
  };
  const salaryProblem = (field: SalaryDraftField) => {
    const problem = salaryProblems[field];
    return problem ? tSalary(`problems.${problem}`) : undefined;
  };
  const setSalaryField = <K extends keyof SalaryDraft>(field: K, value: SalaryDraft[K]) => setSalary((prev) => ({ ...prev, [field]: value }));
  const fullForm = (tab: "experience" | "salary") => `/contribute?tab=${tab}&company=${encodeURIComponent(companySlug)}`;

  return (
    <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4">
      {sections.experience && (
        <fieldset className="flex flex-col gap-3 border-t border-gray-100 pt-4 dark:border-gray-800">
          <legend className="sr-only">{t("experience.heading")}</legend>
          <h3 aria-hidden="true" className="text-sm font-semibold text-gray-900 dark:text-gray-100">
            {t("experience.heading")}
          </h3>
          <RatingInput
            label={t("experience.overall")}
            value={experience.overall}
            onChange={(overall) => setExperience((prev) => ({ ...prev, overall }))}
            error={experienceProblem("overall")}
          />
          <div className="grid gap-4 md:grid-cols-2">
            <FactPills
              label={tExperience("stages")}
              options={STAGE_COUNTS}
              value={experience.stages}
              optionLabel={(o) => tStages(o)}
              notSaidLabel={tExperience("notSaid")}
              optionalLabel={tExperience("optional")}
              onChange={(stages) => setExperience((prev) => ({ ...prev, stages }))}
            />
            <FactCheckPills
              label={tExperience("interviewTypes")}
              hint={tExperience("interviewTypesHint")}
              options={INTERVIEW_TYPES}
              values={experience.interviewTypes}
              optionLabel={(o) => tTypes(o)}
              optionalLabel={tExperience("optional")}
              onToggle={(type) => setExperience((prev) => toggleInterviewType(prev, type, INTERVIEW_TYPES))}
            />
          </div>
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {experience.duration === ""
              ? t("experience.prefilledNoDuration", { outcome: tOutcome("Offer") })
              : t("experience.prefilled", { outcome: tOutcome("Offer"), duration: tDuration(experience.duration) })}{" "}
            {t("experience.more")}{" "}
            <Link href={fullForm("experience")} className={LINK_CLASSES}>
              {t("experience.moreLink")}
            </Link>
          </p>
        </fieldset>
      )}

      {sections.salary && (
        <fieldset className="flex flex-col gap-3 border-t border-gray-100 pt-4 dark:border-gray-800">
          <legend className="sr-only">{t("salary.heading")}</legend>
          <h3 aria-hidden="true" className="text-sm font-semibold text-gray-900 dark:text-gray-100">
            {t("salary.heading")}
          </h3>
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label={tSalary("occupation")} htmlFor="exit-salary-occupation" error={salaryProblem("occupation")}>
              <Combobox
                id="exit-salary-occupation"
                value={effectiveSalary.occupationLabel}
                // Typing again is not the pick any more: the id goes until a row is chosen.
                onChange={(value) => {
                  setOccupationTouched(true);
                  setSalary((prev) => ({ ...prev, occupationLabel: value, occupationId: null }));
                }}
                onSelect={(option) => {
                  setOccupationTouched(true);
                  setSalary((prev) => ({ ...prev, occupationLabel: option.label, occupationId: option.id }));
                }}
                onSearch={async (q) =>
                  (await occupationsApi.search(q)).map((o) => ({
                    id: o.id,
                    label: occupationName(o, locale),
                    hint: occupationOtherName(o, locale),
                  }))
                }
                placeholder={tSalary("occupationPlaceholder")}
                loadingText={tSalary("occupationSearching")}
                emptyText={tSalary("occupationNoMatch")}
              />
              <p className="text-xs text-gray-500 dark:text-gray-400">
                {match ? t("salary.occupationMatched") : t("salary.occupationPick")}
              </p>
            </FormField>
            <FormField label={t("salary.years")} htmlFor="exit-salary-years" error={salaryProblem("yearsOfExperience")}>
              <Input
                id="exit-salary-years"
                type="number"
                inputMode="numeric"
                min={0}
                max={50}
                step={1}
                value={salary.yearsOfExperience}
                onChange={(e) => setSalaryField("yearsOfExperience", e.target.value)}
                className="sm:max-w-32"
              />
            </FormField>
            <div className="flex flex-col gap-1">
              <label htmlFor="exit-salary-amount" className="text-sm font-medium text-gray-700 dark:text-gray-300">
                {tSalary("monthlyNet")}
              </label>
              <div className="grid grid-cols-[minmax(0,1fr)_6rem] gap-2">
                <Input
                  id="exit-salary-amount"
                  inputMode="decimal"
                  value={salary.monthlyNetAmount}
                  onChange={(e) => setSalaryField("monthlyNetAmount", e.target.value)}
                  placeholder={tSalary("monthlyNetPlaceholder")}
                />
                <Select
                  id="exit-salary-currency"
                  aria-label={tSalary("currency")}
                  value={salary.currency}
                  onChange={(e) => setSalaryField("currency", e.target.value as SalaryCurrency)}
                >
                  {SALARY_CURRENCIES.map((currency) => (
                    <option key={currency} value={currency}>
                      {tCurrency(currency)}
                    </option>
                  ))}
                </Select>
              </div>
              {salaryProblem("monthlyNetAmount") && (
                <p className="text-sm text-red-600 dark:text-red-400">{salaryProblem("monthlyNetAmount")}</p>
              )}
            </div>
            <fieldset className="flex flex-col gap-1">
              <legend className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("salary.bonus")}</legend>
              <div className="flex flex-wrap gap-x-6 gap-y-1">
                {([true, false] as const).map((answer) => (
                  <label key={String(answer)} className="flex min-h-11 items-center gap-2 text-sm text-gray-900 dark:text-gray-100">
                    <input
                      type="radio"
                      name="exit-salary-has-bonus"
                      value={String(answer)}
                      checked={salary.hasBonus === answer}
                      onChange={() => setSalaryField("hasBonus", answer)}
                      className={RADIO_CLASSES}
                    />
                    {answer ? t("salary.bonusYes") : t("salary.bonusNo")}
                  </label>
                ))}
              </div>
              {salaryProblem("hasBonus") && <p className="text-sm text-red-600 dark:text-red-400">{salaryProblem("hasBonus")}</p>}
            </fieldset>
          </div>
          {salary.hasBonus && (
            <div className="flex flex-col gap-1 sm:max-w-md">
              <label htmlFor="exit-salary-bonus-amount" className="text-sm font-medium text-gray-700 dark:text-gray-300">
                {tSalary("bonusAmount")}
              </label>
              <div className="grid grid-cols-[minmax(0,1fr)_8rem] gap-2">
                <Input
                  id="exit-salary-bonus-amount"
                  inputMode="decimal"
                  value={salary.annualBonusAmount}
                  onChange={(e) => setSalaryField("annualBonusAmount", e.target.value)}
                  placeholder={tSalary("bonusAmountPlaceholder")}
                />
                <div className="flex items-center rounded-md border border-gray-300 bg-gray-50 px-3 py-2 text-sm text-gray-500 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-400">
                  {tSalary("bonusUnit", { currency: tCurrency(salary.currency) })}
                </div>
              </div>
              {salaryProblem("annualBonusAmount") && (
                <p className="text-sm text-red-600 dark:text-red-400">{salaryProblem("annualBonusAmount")}</p>
              )}
            </div>
          )}
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {t("salary.prefilled", { employmentType: tEmploymentType(employmentType), year: salary.periodStartYear })}{" "}
            <Link href={fullForm("salary")} className={LINK_CLASSES}>
              {t("salary.fullForm")}
            </Link>
          </p>
        </fieldset>
      )}

      <div className="flex flex-wrap items-center gap-3 border-t border-gray-100 pt-4 dark:border-gray-800">
        <Button type="submit" disabled={save.isPending}>
          {save.isPending ? t("saving") : t("save")}
        </Button>
        {sections.experience && sections.salary && <span className="text-xs text-gray-500 dark:text-gray-400">{t("saveNote")}</span>}
        {formError && (
          <p role="alert" className="basis-full text-sm text-red-600 dark:text-red-400">
            {formError}
          </p>
        )}
      </div>
    </form>
  );
}
