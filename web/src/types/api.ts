export type ApplicationStatus =
  | "Applied"
  | "Screening"
  | "Interview"
  | "TechnicalInterview"
  | "FinalInterview"
  | "Offer"
  | "Accepted"
  | "Rejected"
  | "Withdrawn"
  | "Ghosted";

export type EmploymentType =
  | "FullTime"
  | "PartTime"
  | "Contract"
  | "Internship"
  | "Freelance"
  | "Temporary";

export type Source =
  | "Manual"
  | "LinkedIn"
  | "KariyerNet"
  | "LinkedInImport"
  | "CsvImport"
  | "CompanyWebsite"
  | "Referral"
  | "BrowserExtension"
  | "Email"
  | "System"
  | "Other";

export type ApplicationEventType =
  | "ApplicationCreated"
  | "ApplicationSubmitted"
  | "RecruiterContacted"
  | "ScreeningStarted"
  | "InterviewScheduled"
  | "InterviewCompleted"
  | "OfferReceived"
  | "FollowUpSent"
  | "StatusChanged";

export type ApplicationListSortBy = "AppliedAt" | "CompanyName" | "JobTitle" | "Status" | "UpdatedAt";
export type SortDirection = "Ascending" | "Descending";
/** The company view orders whole groups, so it cannot reuse ApplicationListSortBy — half of that
 *  union names a property a company holding several applications does not have. */
export type CompanyGroupSortBy = "LastActivity" | "ApplicationCount" | "CompanyName";

export interface UserProfileResponse {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  createdAt: string;
  consentAcceptedAt: string;
  preferredLanguage: string;
  preferredTheme: string;
  // false for an account created with Sign in with Google that never set a password — the
  // settings page skips the "re-enter your password" step on deletion for those.
  hasPassword: boolean;
  // Whether this account carries Users.IsAdmin server-side. It decides whether the navigation
  // shows the admin link and nothing else: the /api/admin endpoints check the column themselves on
  // every request, so editing this in devtools buys a link that answers 403.
  isAdmin: boolean;
}

/** GET /api/users/me/plan — the caller's own Pro status, readable whether or not the checkout or
 *  the weekly job matching is switched on. `activeUntil` stays set after the period ends so the
 *  profile page can say "Pro ended on …". */
export interface UserPlanResponse {
  isActive: boolean;
  activeUntil: string | null;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: UserProfileResponse;
}

export interface ApplicationSummaryResponse {
  id: string;
  companyId: string;
  companyName: string;
  jobTitle: string;
  status: ApplicationStatus;
  appliedAt: string;
  updatedAt: string;
}

export interface ApplicationDetailResponse {
  id: string;
  companyId: string;
  companyName: string;
  companyWebsite: string | null;
  companyLinkedInUrl: string | null;
  jobTitle: string;
  jobUrl: string | null;
  location: string | null;
  employmentType: EmploymentType;
  appliedAt: string;
  status: ApplicationStatus;
  source: Source;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
  jobDescriptionHtml: string | null;
  hrName: string | null;
  hrEmail: string | null;
  hrLinkedInUrl: string | null;
  hrEmailSource: HrEmailSource | null;
  // The CV recorded for this application. Both go back to null on their own if that CV is later
  // deleted — the server clears the reference rather than deleting the application.
  cvDocumentId: string | null;
  cvDocumentFileName: string | null;
  // The rest of what is known about the company. Industry and Country are filled in the background
  // from the company's LinkedIn page; kariyer.net comes from the extension. Any of them can stay
  // null indefinitely — render nothing rather than a placeholder.
  companyKariyerNetUrl: string | null;
  companyIndustry: string | null;
  companyCountry: string | null;
  // The company page's slug, for linking a closed application into `/contribute?company=`.
  // Optional rather than nullable-only: during a rolling deploy the API may predate the field.
  companySlug?: string | null;
}

export type HrEmailSource = "Manual" | "IncomingEmail";

export type CvFileFormat = "Pdf" | "Doc" | "Docx";

export interface CvDocumentResponse {
  id: string;
  fileName: string;
  format: CvFileFormat;
  sizeBytes: number;
  isDefault: boolean;
  uploadedAt: string;
  usedByApplicationCount: number;
  /** The last ATS-readability scan of this file; null until the user asks for one. */
  scan: CvDocumentScanSummary | null;
}

export interface CvDocumentScanSummary {
  score: number;
  scannedAt: string;
}

/** The stored report of one CV — the public scan's response without the content notes. */
export interface CvDocumentScanReport {
  score: number;
  categories: CvScanCategoryScore[];
  findings: CvScanFinding[];
  document: CvScanDocumentSummary;
  extractedTextPreview: string;
  extractedTextTruncated: boolean;
  scannedAt: string;
}

export interface CvDocumentListResponse {
  items: CvDocumentResponse[];
  /** The server's own per-user cap. Read from the response rather than duplicated here, so the
   *  quota reading and the disabled upload button can never disagree with what the server does. */
  maxCount: number;
}

export type StatusChangeOrigin =
  | "Manual"
  | "EmailSuggestionConfirmed"
  | "EmailAutoApplied"
  | "EmailAutoApplyReverted"
  | "BulkEdit"
  | "BulkEditReverted"
  | "Import"
  | "Extension"
  | "System";

export interface ApplicationStatusHistoryResponse {
  id: string;
  fromStatus: ApplicationStatus | null;
  toStatus: ApplicationStatus;
  changedAt: string;
  note: string | null;
  origin: StatusChangeOrigin;
  source: Source;
  emailSuggestionId: string | null;
  rejectionReasonCategory: RejectionReasonCategory | null;
  rejectionReasonDetail: string | null;
  emailSubject: string | null;
  emailSnippet: string | null;
}

export interface ApplicationEventResponse {
  id: string;
  type: ApplicationEventType;
  occurredAt: string;
  source: Source;
  metadata: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ApplicationSummaryCountsResponse {
  total: number;
  active: number;
  waiting: number;
  interviews: number;
  offers: number;
  rejected: number;
  ghosted: number;
}

export interface AnalyticsRatesResponse {
  totalApplications: number;
  respondedCount: number;
  responseRate: number;
  interviewCount: number;
  interviewRate: number;
  offerCount: number;
  offerRate: number;
  rejectedCount: number;
  rejectionRate: number;
  ghostedCount: number;
  ghostingRate: number;
}

export interface ResponseTimeStatsResponse {
  sampleSize: number;
  averageDays: number | null;
  medianDays: number | null;
}

export interface StatusDistributionItem {
  status: ApplicationStatus;
  count: number;
}

export interface ApplicationsPerWeekItem {
  /** Monday (UTC) the bucket opens on, as `yyyy-MM-dd`. */
  weekStart: string;
  count: number;
}

export interface AnalyticsOverviewResponse {
  rates: AnalyticsRatesResponse;
  responseTime: ResponseTimeStatsResponse;
  statusDistribution: StatusDistributionItem[];
  applicationsPerWeek: ApplicationsPerWeekItem[];
}

export interface CreateApplicationRequest {
  companyName: string;
  jobTitle: string;
  jobUrl: string | null;
  location: string | null;
  employmentType: EmploymentType;
  appliedAt: string;
  source: Source | null;
  notes: string | null;
  hrName: string | null;
  hrEmail: string | null;
  hrLinkedInUrl: string | null;
  cvDocumentId: string | null;
}

export interface UpdateApplicationRequest {
  jobTitle: string;
  jobUrl: string | null;
  location: string | null;
  employmentType: EmploymentType;
  appliedAt: string;
  notes: string | null;
  hrName: string | null;
  hrEmail: string | null;
  hrLinkedInUrl: string | null;
  cvDocumentId: string | null;
}

export interface ChangeStatusRequest {
  newStatus: ApplicationStatus;
  note: string | null;
  changedAt: string | null;
}

export interface ApplicationListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: ApplicationStatus;
  companyId?: string;
  sortBy?: ApplicationListSortBy;
  sortDirection?: SortDirection;
}

export interface GroupedApplicationsQuery {
  page?: number;
  /** Counts companies, not applications — the company view pages over groups. */
  pageSize?: number;
  search?: string;
  status?: ApplicationStatus;
  sortBy?: CompanyGroupSortBy;
  sortDirection?: SortDirection;
}

/** Only statuses the company actually holds appear. */
export interface CompanyGroupStatusCount {
  status: ApplicationStatus;
  count: number;
}

export interface CompanyGroupResponse {
  companyId: string;
  companyName: string;
  /** Every matching application at this company, which is more than `applications.length` when
   *  `hasMore` is set. */
  applicationCount: number;
  lastActivityAt: string;
  statusCounts: CompanyGroupStatusCount[];
  applications: ApplicationSummaryResponse[];
  /** The company holds more matching applications than this response carries; the view links the
   *  rest out to the flat list filtered to the company. */
  hasMore: boolean;
}

export interface GroupedApplicationsResponse {
  items: CompanyGroupResponse[];
  /** Matching companies — what the pager counts. */
  totalCount: number;
  page: number;
  pageSize: number;
  /** Matching applications across every page. What "select all N matching" acts on; counting
   *  companies there would promise one number and act on another. */
  totalApplicationCount: number;
}

export interface EmailSuggestionResponse {
  id: string;
  applicationId: string | null;
  companyName: string;
  jobTitle: string;
  suggestedStatus: ApplicationStatus | null;
  confidenceScore: number;
  subject: string;
  snippet: string;
  emailReceivedAt: string;
  // True when applicationId is null: this email matched no existing Application — companyName/
  // jobTitle/location/description were extracted from the email itself, and confirming this
  // suggestion creates the Company/Application from them.
  isNewApplicationSuggestion: boolean;
  location: string | null;
  description: string | null;
  // Only set when suggestedStatus is Rejected. "NotStated" (not null) is the expected majority
  // value — most rejection emails don't state a reason at all.
  rejectionReasonCategory: RejectionReasonCategory | null;
  rejectionReasonDetail: string | null;
}

export type RejectionReasonCategory =
  | "NotStated"
  | "LanguageRequirement"
  | "LocationOrRelocation"
  | "ExperienceLevelMismatch"
  | "SalaryExpectationMismatch"
  | "SkillOrTechStackGap"
  | "PositionCancelledOrFilled"
  | "CultureOrTeamFit"
  | "Other";

export interface SuggestionCountResponse {
  count: number;
}

// Whether the extension's Gmail content script has ever delivered a signal for this account —
// the server never sees the extension's own Gmail Scanning toggle, so this is the closest it gets
// to "scanning is on". See GmailScanStatusResponse on the API side.
export interface GmailScanStatusResponse {
  hasReceivedSignal: boolean;
}

export type EmailApplicationMatchType = "DomainMatch" | "NameFallbackMatch";

export interface EmailNotificationResponse {
  id: string;
  applicationId: string | null;
  companyName: string;
  jobTitle: string;
  status: ApplicationStatus | null;
  wasAutoApplied: boolean;
  // True when this event started as a "new job" suggestion — confirming it created the
  // Application, rather than changing an existing one's status.
  isNewApplicationSuggestion: boolean;
  matchType: EmailApplicationMatchType | null;
  confidenceScore: number;
  isRead: boolean;
  createdAt: string;
  resolvedAt: string | null;
}

export interface NotificationCountResponse {
  unreadCount: number;
}

export type PersonalAccessTokenScope = "Full" | "Extension";

export interface PersonalAccessTokenResponse {
  id: string;
  name: string;
  scope: PersonalAccessTokenScope;
  createdAt: string;
  expiresAt: string;
  lastUsedAt: string | null;
}

export interface CreatedPersonalAccessTokenResponse {
  id: string;
  name: string;
  token: string;
  scope: PersonalAccessTokenScope;
  createdAt: string;
  expiresAt: string;
}

/** Where a pairing stands, as ExtensionPairingStatus on the server. */
export type ExtensionPairingStatus =
  | "Pending"
  | "Approved"
  | "Completed"
  | "Denied"
  | "Expired"
  | "TokenLimitReached";

export interface ExtensionPairingReviewResponse {
  code: string;
  expiresAt: string;
  status: ExtensionPairingStatus;
}

export interface ExtensionPairingReviewStatusResponse {
  status: ExtensionPairingStatus;
}

export interface ExtensionApplicationResponse {
  application: ApplicationDetailResponse;
  wasDuplicate: boolean;
}

export interface CompanySearchResult {
  id: string;
  name: string;
  website: string | null;
}

export interface ImportRowErrorResponse {
  rowNumber: number;
  rawRow: string;
  errorMessage: string;
}

export type ImportBatchStatus = "Pending" | "Processing" | "Completed" | "Failed";

export interface ImportAcceptedResponse {
  id: string;
}

export interface ImportSummaryResponse {
  id: string;
  source: Source;
  fileName: string;
  status: ImportBatchStatus;
  processedRows: number;
  totalRows: number | null;
  totalRecords: number;
  newApplications: number;
  duplicateRecords: number;
  invalidRecords: number;
  completedAt: string | null;
  errorMessage: string | null;
  errors: ImportRowErrorResponse[];
}

export interface TrackedJobResponse {
  id: string;
  companyId: string;
  companyName: string;
  companyWebsite: string | null;
  companyLinkedInUrl: string | null;
  jobTitle: string;
  jobUrl: string | null;
  location: string | null;
  notes: string | null;
  addedAt: string;
  hrName: string | null;
  hrEmail: string | null;
  hrLinkedInUrl: string | null;
}

export interface CreateTrackedJobRequest {
  companyName: string;
  jobTitle: string;
  jobUrl: string | null;
  location: string | null;
  notes: string | null;
  hrName: string | null;
  hrEmail: string | null;
  hrLinkedInUrl: string | null;
}

export interface ConvertTrackedJobRequest {
  employmentType: EmploymentType;
  appliedAt: string;
  notes: string | null;
}

// GET /api/config — server-side limits the UI states up front (all still enforced server-side).
export interface PasswordPolicy {
  requiredLength: number;
  requiredUniqueChars: number;
  requireDigit: boolean;
  requireLowercase: boolean;
  requireUppercase: boolean;
  requireNonAlphanumeric: boolean;
}

export interface PersonalAccessTokenLimits {
  maxActiveTokens: number;
  lifetimeDays: number;
}

export interface GoogleAuthConfig {
  enabled: boolean;
  // Public OAuth client id (it is visible in the redirect to accounts.google.com anyway);
  // null whenever enabled is false.
  clientId: string | null;
}

export interface LinkedInAuthConfig {
  enabled: boolean;
  // Public OAuth client id (it is visible in the redirect to linkedin.com anyway); null whenever
  // enabled is false.
  clientId: string | null;
}

export interface GitHubAuthConfig {
  enabled: boolean;
  // Public OAuth client id (it is visible in the redirect to github.com); null whenever enabled is
  // false.
  clientId: string | null;
}

/** Whether the CV scan can offer its optional content-notes consent. False means the checkbox is
 *  not rendered at all — a box for something that cannot happen is a promise the page cannot
 *  keep. The scan itself does not depend on this. */
export interface CvScanConfig {
  /** The CvScan:Enabled flag: the stored-CV scan on /cv is offered only while the routes exist. */
  enabled: boolean;
  contentNotesAvailable: boolean;
}

/** Whether company reviews are switched on, the quota the form counts down from, and the two
 *  numbers the scoring page prints so its formula quotes the live configuration. */
export interface CompanyReviewsConfig {
  enabled: boolean;
  maxReviewsPerUser: number;
  minimumReviewsForScore: number;
  priorWeight: number;
}

/** Whether the paid weekly job matching is switched on. Off means every /api/job-sources route
 *  answers 404 and the app shows no trace of it — no nav link, no page. */
export interface JobSourcesConfig {
  enabled: boolean;
}

/** Whether the Pro plan can be bought (PayTR switched on and configured). Prices are not here —
 *  this response is public and cached; they come from GET /api/payments/plans. */
export interface PaymentsConfig {
  enabled: boolean;
}

/** Whether salary entries are switched on, the quota the form counts down from, and the
 *  per-currency threshold under which the company page shows no median. */
export interface CompanySalariesConfig {
  enabled: boolean;
  maxEntriesPerUser: number;
  minimumEntriesForStats: number;
}

/** Whether candidate experiences are switched on, the quota the form counts down from, the
 *  threshold under which the company page shows no aggregate, and the score's prior weight. */
export interface CandidateExperiencesConfig {
  enabled: boolean;
  maxEntriesPerUser: number;
  minimumEntriesForStats: number;
  priorWeight: number;
}

export interface ClientConfigResponse {
  passwordPolicy: PasswordPolicy;
  personalAccessTokens: PersonalAccessTokenLimits;
  googleAuth: GoogleAuthConfig;
  linkedInAuth: LinkedInAuthConfig;
  gitHubAuth: GitHubAuthConfig;
  cvScan: CvScanConfig;
  // Optional: an API deployed before the reviews feature answers without it.
  companyReviews?: CompanyReviewsConfig;
  // Optional for the same reason.
  jobSources?: JobSourcesConfig;
  payments?: PaymentsConfig;
  companySalaries?: CompanySalariesConfig;
  candidateExperiences?: CandidateExperiencesConfig;
}

// POST /api/auth/google: exactly one of the two is set.
export interface GoogleSignupPrefill {
  signupToken: string;
  email: string;
  firstName: string;
  lastName: string;
}

export interface GoogleSignInResponse {
  auth: AuthResponse | null;
  pendingSignup: GoogleSignupPrefill | null;
}

// POST /api/auth/linkedin: exactly one of the two is set. `email` is LinkedIn's verified address
// (shown read-only), or null when LinkedIn provided none — LinkedIn's OpenID Connect response makes
// it optional — in which case the complete-your-sign-up form must collect and require one.
export interface LinkedInSignupPrefill {
  signupToken: string;
  email: string | null;
  firstName: string;
  lastName: string;
}

export interface LinkedInSignInResponse {
  auth: AuthResponse | null;
  pendingSignup: LinkedInSignupPrefill | null;
}

// POST /api/auth/github: exactly one of the two is set. `email` is a GitHub-verified address (shown
// read-only), or null when GitHub exposed none we can both verify and deliver to — a private-email
// account, a noreply-only one, or a grant without the user:email scope — in which case the
// complete-your-sign-up form must collect and require one. The two names are a best-effort split of
// GitHub's single free-text profile name and are meant to be corrected.
export interface GitHubSignupPrefill {
  signupToken: string;
  email: string | null;
  firstName: string;
  lastName: string;
}

export interface GitHubSignInResponse {
  auth: AuthResponse | null;
  pendingSignup: GitHubSignupPrefill | null;
}

export type FeedbackCategory = "Bug" | "Idea" | "Question";
export type FeedbackMood = "Struggling" | "Okay" | "Good";

export interface SubmitFeedbackRequest {
  category: FeedbackCategory;
  message: string;
  mood?: FeedbackMood | null;
  replyEmail?: string | null;
  /** The in-app path the panel was opened from — path only, never the query string. Disclosed to
   *  the user in the panel before they send. */
  pagePath?: string | null;
  locale?: string | null;
  theme?: string | null;
}

/** Just the receipt: the panel only needs to know the message landed. */
export interface FeedbackResponse {
  id: string;
  submittedAt: string;
}

/** One stored day of internal product metrics. Aggregate counts across the whole product — never
 *  per-user data. Only readable by accounts flagged Users.IsAdmin server-side. */
export interface ProductMetricsDayResponse {
  /** The UTC day these numbers describe, as `YYYY-MM-DD`. */
  snapshotDate: string;
  totalUsers: number;
  activatedUsers: number;
  activationRate: number;
  weeklyActiveUsers: number;
  applicationsTrackedLast30Days: number;
  statusUpdatesLast30Days: number;
  /** Null when no cohort is old enough yet — not zero. */
  d7RetentionRate: number | null;
  d30RetentionRate: number | null;
  d90RetentionRate: number | null;
  totalApplications: number;
  uniqueCompanies: number;
  uniqueJobs: number;
  applicationsWithOutcome: number;
  applicationsWithResponseTime: number;
  computedAt: string;
}

/** The field someone is applying in, as offered by the public benchmark form. A fixed list, not
 *  `Company.Industry` — that column holds uncontrolled free text scraped from LinkedIn. */
export type BenchmarkSector =
  | "SoftwareAndIt"
  | "FinanceAndInsurance"
  | "EcommerceAndRetail"
  | "ManufacturingAndIndustry"
  | "Telecom"
  | "HealthAndPharma"
  | "Education"
  | "ConsultingAndProfessionalServices"
  | "MediaAndMarketing"
  | "LogisticsAndTransport"
  | "ConstructionAndRealEstate"
  | "PublicAndNonProfit"
  | "Other";

export type BenchmarkPeriod = "LastThreeMonths" | "LastSixMonths" | "LastTwelveMonths" | "Longer";

export type BenchmarkSeniority = "StudentOrIntern" | "Junior" | "Mid" | "Senior" | "LeadOrAbove";

export type BenchmarkLocation = "Istanbul" | "Ankara" | "Izmir" | "TurkeyOther" | "Abroad" | "Remote";

export interface SubmitBenchmarkRequest {
  applicationCount: number;
  replyCount: number;
  sector: BenchmarkSector;
  period: BenchmarkPeriod;
  seniority?: BenchmarkSeniority | null;
  location?: BenchmarkLocation | null;
  locale: string;
  /** Honeypot — always sent empty. See the API contract for why a CAPTCHA is not an option here. */
  website: string;
}

/** Which pool the median shown was drawn from. `Overall` is the fallback for a sector that has not
 *  reached the threshold yet, and the page must label it as not being about the reader's field. */
export type BenchmarkComparisonScope = "None" | "Overall" | "Sector";

/** What one person is told back. `medianRate` and `shareBelowYou` are null only when neither the
 *  sector nor the whole pool has enough answers — the normal opening state, not an error. */
export interface BenchmarkResultResponse {
  sector: BenchmarkSector;
  /** The two counts the rate came from, echoed back so the result renders without the form state. */
  applicationCount: number;
  replyCount: number;
  yourRate: number;
  /** The answerer's own sector, whatever the scope — it is what says how far off a sector median is. */
  sampleSize: number;
  totalSubmissions: number;
  /** The bar a pool has to clear. Present so a withheld comparison can say how far off it is. */
  minimumSampleSize: number;
  scope: BenchmarkComparisonScope;
  /** How many answers are behind the median shown, or null when there is none. */
  comparedAgainstCount: number | null;
  medianRate: number | null;
  shareBelowYou: number | null;
}

export interface BenchmarkSectorCount {
  sector: BenchmarkSector;
  count: number;
}

export interface BenchmarkSummaryResponse {
  totalSubmissions: number;
  minimumSampleSize: number;
  bySector: BenchmarkSectorCount[];
}

/** One day's count of one (event, page, language, referring host) combination on the public site.
 *  Aggregate only: there is no visitor id behind these rows, so two visits by one person and one
 *  visit by two people are the same number, and a row can never be joined back to an account. */
export interface SiteTrafficCounterResponse {
  /** The UTC day counted, as `YYYY-MM-DD`. */
  day: string;
  /** `PageView`, `CtaGetStarted`, `RegisterStarted` or `RegisterCompleted`. */
  event: string;
  /** Public path with the language prefix removed, e.g. `/guide/how-many-applications`. */
  path: string;
  locale: string;
  /** Referring host, or an empty string when the visit had no referrer. */
  referrerHost: string;
  count: number;
}

/** One confidence band's evidence about whether auto-apply can be trusted there. */
export interface AutoApprovalCalibrationBucket {
  lowerBound: number;
  upperBound: number;
  total: number;
  confirmed: number;
  dismissed: number;
  autoApplied: number;
  reverted: number;
  pending: number;
  /** Agreement with a suggestion the user was *shown*. Flatters auto-apply — see revertRate. */
  agreementRate: number | null;
  /** Share of unattended applies the user took back. The number that actually settles the
   *  threshold; null until auto-apply has acted in this band. */
  revertRate: number | null;
}

export interface AutoApprovalCalibrationResponse {
  currentThreshold: number;
  autoApplyEnabled: boolean;
  shadowModeEnabled: boolean;
  qualifyingTotal: number;
  buckets: AutoApprovalCalibrationBucket[];
}

/** Which applications a bulk operation covers. Exactly one of the two is set: an explicit list of
 *  ids the user ticked, or the list's own filter, whose matches the server resolves — including
 *  rows on pages the user never opened. */
export type BulkSelection =
  | { ids: string[]; allMatching?: undefined }
  | {
      ids?: undefined;
      allMatching: {
        search: string | null;
        status: ApplicationStatus | null;
        /** Set when the list was narrowed to one company, so "all matching" means the rows on
         *  screen and not every company's. */
        companyId: string | null;
      };
    };

export interface BulkChangeStatusRequest {
  selection: BulkSelection;
  newStatus: ApplicationStatus;
  note: string | null;
  /** How many applications the user was told they were acting on. Required for an `allMatching`
   *  selection — the server refuses with 409 when the count no longer holds. */
  expectedCount: number | null;
}

export interface BulkStatusChange {
  applicationId: string;
  fromStatus: ApplicationStatus;
  toStatus: ApplicationStatus;
}

export interface BulkChangeStatusResponse {
  updated: number;
  /** Already in the target status, so left alone. Shown on screen rather than folded into
   *  `updated`: "12 selected, 10 changed" needs an explanation. */
  skippedAlreadyInStatus: number;
  /** What moved and from where — the material the undo is built out of. */
  changes: BulkStatusChange[];
}

export interface UndoBulkStatusEntry {
  applicationId: string;
  /** The status the client last saw. Anything that has moved on since is left alone. */
  expectedStatus: ApplicationStatus;
  revertTo: ApplicationStatus;
}

export interface UndoBulkStatusResponse {
  reverted: number;
  skipped: number;
}

export interface BulkDeleteRequest {
  selection: BulkSelection;
  expectedCount: number | null;
}

export interface BulkDeleteResponse {
  deleted: number;
}

/** The 409 body when an all-matching selection no longer matches the count the user was shown.
 *  Nothing was changed when this comes back. */
export interface BulkCountMismatchProblem {
  errorCode: "BULK_COUNT_MISMATCH";
  expectedCount: number;
  actualCount: number;
}

/** The four things the CV scan's score is made of. Their weights come from the response rather
 *  than being repeated here, so the page can never disagree with the server about them. */
export type CvScanCategory = "MachineReadability" | "SectionsAndDates" | "Contact" | "FormatAndLength";

/** Every problem the scan can report. Closed, because the page carries a title, an explanation and
 *  a fix for each one in both languages — a code with no copy behind it would render as a blank
 *  accusation. */
export type CvScanFindingCode =
  | "NoTextLayer"
  | "BrokenTurkishCharacters"
  | "MultiColumnOrTableLayout"
  | "SectionsOrDatesUnreadable"
  | "ContactUnreadable"
  | "LengthOutOfRange"
  | "InconsistentFormatting";

/** Where the finding can be seen in the reader's own file. At least one of the two is always
 *  present — a finding that can point at nothing is dropped server-side. */
export interface CvScanEvidence {
  page: number | null;
  quote: string | null;
}

/** `pointCost` is what the finding actually cost, so a fix list adds up to exactly the points the
 *  score is missing. `metrics` are the raw numbers behind it, named rather than pre-rendered, so
 *  the sentence can be written in the reader's language. */
export interface CvScanFinding {
  code: CvScanFindingCode;
  category: CvScanCategory;
  pointCost: number;
  evidence: CvScanEvidence[];
  metrics: Record<string, number>;
}

export interface CvScanCategoryScore {
  category: CvScanCategory;
  weight: number;
  score: number;
}

/** `pageCount` is null for .docx, which has no pagination until something renders it. */
export interface CvScanDocumentSummary {
  format: CvFileFormat;
  pageCount: number | null;
  wordCount: number;
}

/** One scan. `score` is always the sum of `categories`, and no model contributes to it.
 *  `extractedTextPreview` is the CV as a machine reads it — the part of this page that does the
 *  arguing. */
export interface CvScanResponse {
  score: number;
  categories: CvScanCategoryScore[];
  findings: CvScanFinding[];
  document: CvScanDocumentSummary;
  extractedTextPreview: string;
  extractedTextTruncated: boolean;
  /** Layer B. Never part of `score` — the page badges it as such. */
  reviewStatus: CvReviewStatus;
  contentNotes: CvContentNote[];
}

/** Layer B of the CV scan: what a model said about the writing. A closed set, because the page
 *  carries copy for each kind in both languages. */
export type CvContentNoteKind =
  | "UnquantifiedAchievement"
  | "WeakVerb"
  | "RepeatedVerb"
  | "LanguageInconsistency";

/** `quote` is verbatim from the reader's own CV — the server drops any note whose quote it cannot
 *  find in the extracted text, so a note always points at a line that really exists. */
export interface CvContentNote {
  kind: CvContentNoteKind;
  quote: string;
  suggestion: string;
}

/** Why the notes section looks the way it does. `Disabled` means the feature is off and the page
 *  says nothing about it; `NotRequested` is the ordinary case of an unticked optional box;
 *  `Unavailable` is asked-for-but-not-delivered, which never affects the score. */
export type CvReviewStatus = "Disabled" | "NotRequested" | "Unavailable" | "Ready";

// ---- Company reviews -------------------------------------------------------------------------
// Three shapes, mirrored from the API and never mixed: what anyone on the internet sees (no
// author, month-precision date), what an author sees of their own rows, and what an admin sees.

export type EmploymentStatus = "CurrentEmployee" | "FormerEmployee" | "Intern";
export type ReviewModerationStatus = "Pending" | "Approved" | "Rejected";
export type ReviewReportReason =
  | "Insult"
  | "Profanity"
  | "PersonalInformation"
  | "MisleadingInformation"
  | "Advertising"
  | "Spam"
  | "Other";
export type ReviewReportStatus = "Open" | "Resolved";
export type ReviewReportResolution = "Dismissed" | "ChangesRequested" | "Removed";
export type PublicReviewSort = "Newest" | "MostHelpful";

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** The ten optional categories plus Overall, in the order the form shows them. Mirrors
 *  AfterApply.Domain.CompanyReviews.ReviewCategory. */
export type ReviewCategory =
  | "Overall"
  | "WorkEnvironment"
  | "Management"
  | "CareerGrowth"
  | "WorkLifeBalance"
  | "Pay"
  | "Benefits"
  | "RemoteWork"
  | "Tooling"
  | "Hiring"
  | "Onboarding";
export type ReviewStatementKind = "Liked" | "Improve";
/** Legacy rows were written before 2026-09-16 with free text; their text is never on the public
 *  wire, only their ratings. */
export type ReviewFormat = "Legacy" | "Structured";

export interface ReviewCategoryRating {
  category: ReviewCategory;
  rating: number;
}

export interface ReviewCategoryAverage {
  category: ReviewCategory;
  /** How many published reviews rated this category. */
  count: number;
  /** Null while `count` is under the site minimum. */
  average: number | null;
}

export interface ReviewStatementCount {
  key: string;
  count: number;
}

export interface CompanyReviewSummary {
  approvedCount: number;
  /** Null until `minimumForScore` reviews are published. */
  score: number | null;
  minimumForScore: number;
  priorWeight: number;
  averageOverall: number | null;
  /** Always the ten optional categories, in `ReviewCategory` order. */
  categories: ReviewCategoryAverage[];
  /** Approved reviews per Overall star, index 0 = 1 star. */
  distribution: number[];
  /** Empty under the minimum; at most three each. */
  topLiked: ReviewStatementCount[];
  topImprovable: ReviewStatementCount[];
}

export interface CompanyPublicResponse {
  id: string;
  slug: string;
  name: string;
  website: string | null;
  summary: CompanyReviewSummary;
  /** How many salary entries a signed-in reader would find. Optional: an API deployed before the
   *  salary feature answers without it. */
  salaryCount?: number;
  /** How many candidate experiences the third tab holds; zero while that feature is off. */
  candidateExperienceCount?: number;
}

export interface CompanyPublicListItem {
  id: string;
  slug: string;
  name: string;
  approvedCount: number;
  score: number | null;
}

/** What every review shape carries besides its identity. `categoryRatings` holds only the
 *  categories the author rated; for a legacy row it is the three fixed ratings that map onto a
 *  current category, with the fourth (salary & benefits) kept apart because it maps onto none. */
export interface StructuredReviewFields {
  format: ReviewFormat;
  employmentStatus: EmploymentStatus;
  overallRating: number;
  categoryRatings: ReviewCategoryRating[];
  legacySalaryAndBenefitsRating: number | null;
  /** Catalogue keys — see statementCatalogue.ts; the wording is in the message catalogue. */
  likedStatements: string[];
  improvableStatements: string[];
}

/** No free text, by design: a legacy review's title, pros and cons are not on this record. */
export interface CompanyReviewPublic extends StructuredReviewFields {
  id: string;
  /** yyyy-MM — month precision on purpose. */
  submittedMonth: string;
  helpfulCount: number;
}

export interface ReviewedCompanySlug {
  slug: string;
  lastApprovedAt: string;
}

export interface ResolvedCompany {
  id: string;
  slug: string;
  name: string;
}

/** The legacy text fields are null on a structured review and still present on a legacy one:
 *  the author may read what they wrote until they convert it by editing. */
export interface MyCompanyReview extends StructuredReviewFields {
  id: string;
  companyId: string;
  companySlug: string;
  companyName: string;
  title: string | null;
  pros: string | null;
  cons: string | null;
  status: ReviewModerationStatus;
  rejectionReason: string | null;
  submittedAt: string;
  moderatedAt: string | null;
}

export interface ReviewQuota {
  used: number;
  limit: number;
}

export interface MyReviewsResponse {
  items: MyCompanyReview[];
  quota: ReviewQuota;
}

export interface CompanyReviewViewerState {
  ownReview: MyCompanyReview | null;
  helpfulMarkedReviewIds: string[];
  quota: ReviewQuota;
}

export interface HelpfulToggleResponse {
  marked: boolean;
  helpfulCount: number;
}

export interface CompanyReviewRequest {
  employmentStatus: EmploymentStatus;
  overallRating: number;
  categoryRatings: ReviewCategoryRating[];
  likedStatements: string[];
  improvableStatements: string[];
}

export interface ReportCompanyReviewRequest {
  reason: ReviewReportReason;
  note: string | null;
}

export interface AdminCompanyReviewListItem {
  id: string;
  companyId: string;
  companyName: string;
  companySlug: string | null;
  format: ReviewFormat;
  title: string | null;
  overallRating: number;
  employmentStatus: EmploymentStatus;
  status: ReviewModerationStatus;
  submittedAt: string;
  moderatedAt: string | null;
  openReportCount: number;
}

export interface AdminReviewReport {
  id: string;
  reviewId: string;
  reviewFormat: ReviewFormat;
  reviewTitle: string | null;
  companyId: string;
  companyName: string;
  reporterUserId: string;
  reporterEmail: string;
  reason: ReviewReportReason;
  note: string | null;
  status: ReviewReportStatus;
  resolution: ReviewReportResolution | null;
  resolutionReason: string | null;
  reportedAt: string;
  resolvedAt: string | null;
}

export interface AdminCompanyReview extends StructuredReviewFields {
  id: string;
  companyId: string;
  companyName: string;
  companySlug: string | null;
  authorUserId: string;
  authorEmail: string;
  title: string | null;
  pros: string | null;
  cons: string | null;
  status: ReviewModerationStatus;
  rejectionReason: string | null;
  submittedAt: string;
  moderatedAt: string | null;
  helpfulCount: number;
  reports: AdminReviewReport[];
}

export interface ModerationCounts {
  pendingReviews: number;
  openReports: number;
}

export interface UserReviewQuota {
  userId: string;
  reviewQuotaOverride: number | null;
  effectiveLimit: number;
  used: number;
}

/** Mirrors AfterApply.Application.Applications.Contracts.StaleApplicationsSummaryResponse. */
export interface StaleApplicationsSummaryResponse {
  count: number;
  oldestDays: number;
  thresholdDays: number;
  /** False once the user answered "not now", until a later import adds stale rows they have not
   *  been asked about. */
  suggest: boolean;
}

// --- Reminders ---------------------------------------------------------------------------------

/** Mirrors AfterApply.Domain.Notifications.ReminderType. */
export type ReminderType = "FollowUp" | "PossiblyGhosted";

/** Mirrors AfterApply.Application.Notifications.Contracts.ReminderResponse. */
export interface ReminderResponse {
  id: string;
  applicationId: string;
  companyName: string;
  jobTitle: string;
  type: ReminderType;
  daysElapsed: number;
  createdAt: string;
  /** This user's own median first-reply time in days — the norm a "possibly ghosted" row is read
   *  against. Absent or null when too few of their applications have been answered, and on an API
   *  instance that predates the field. */
  userMedianResponseDays?: number | null;
}

/** Mirrors AfterApply.Application.Notifications.Contracts.ReminderPauseState. */
export type ReminderPauseState = "None" | "Paused" | "Returned";

/** Mirrors AfterApply.Application.Notifications.Contracts.ReminderPauseResponse — where the
 *  user's break from reminders stands (T5). `silencedCount` is only meaningful in "Returned". */
export interface ReminderPauseResponse {
  state: ReminderPauseState;
  pausedFrom: string | null;
  pausedUntil: string | null;
  silencedCount: number;
}

/** Mirrors AfterApply.Application.Notifications.Contracts.ReminderSelection: the ticked ids, or
 *  "all" — every active reminder, resolved on the server, which is why `all` travels with an
 *  `expectedCount` on the request. */
export type ReminderSelection = { ids: string[] } | { all: true };

/** Mirrors AfterApply.Application.Notifications.Contracts.BulkReminderRequest. */
export interface BulkReminderRequest {
  selection: ReminderSelection;
  /** Required with `all`; the server refuses the request (409, BulkCountMismatchProblem) when its
   *  count of active reminders no longer matches. */
  expectedCount?: number | null;
}

/** Mirrors AfterApply.Application.Notifications.Contracts.BulkReminderResponse. */
export interface BulkReminderResponse {
  affected: number;
}

// ---- Weekly job matching (/api/job-sources) ----------------------------------------------------

export interface JobSourceStatusResponse {
  isPro: boolean;
  proActiveUntil: string | null;
  hasCv: boolean;
  cvFileName: string | null;
  hasProfile: boolean;
  /** Whether the user closed the dashboard's announcement of this feature. */
  announcementDismissed: boolean;
}

export interface JobSourceProfileResponse {
  titles: string[];
  location: string;
  remoteOnly: boolean;
  enabled: boolean;
  minScore: number;
  aiScoringConsentAcceptedAt: string | null;
  emailDigest: boolean;
  updatedAt: string;
}

export interface UpsertJobSourceProfileRequest {
  titles: string[];
  location: string;
  remoteOnly: boolean;
  enabled: boolean;
  minScore: number;
  acceptAiScoring: boolean;
  emailDigest: boolean;
}

export interface JobSourcePostingSummaryResponse {
  id: string;
  // Which site the posting came from: "LinkedIn" or "KariyerNet" (the Source enum's names).
  source: string;
  title: string;
  companyName: string;
  companyProfileUrl: string | null;
  location: string | null;
  postedAt: string | null;
  url: string;
  seniority: string | null;
  employmentType: string | null;
  deliveredAt: string;
  weekKey: number;
  // Null until scored: no description yet, no consent, or the scorer has not reached it.
  score: number | null;
  scoreSummary: string | null;
}

export interface JobSourcePostingDetailResponse extends JobSourcePostingSummaryResponse {
  description: string | null;
  jobFunction: string | null;
  industries: string | null;
  matchedCriteria: string[];
  missingCriteria: string[];
  requiredSkills: string[];
  scoredAt: string | null;
}

export interface JobSourceRunResponse {
  weekKey: number;
  ranAt: string;
  candidateCount: number;
  deliveredCount: number;
  excludedAppliedCount: number;
  excludedRecentlyShownCount: number;
  scoredCount: number;
  hiddenBelowMinScoreCount: number;
}

export interface JobSourceDeliveriesResponse {
  items: JobSourcePostingSummaryResponse[];
  run: JobSourceRunResponse | null;
}

// ---------------------------------------------------------------------------------------------
// Payments (PayTR iFrame) — /api/payments/* and /api/admin/payments/*
// ---------------------------------------------------------------------------------------------

export type ProPlan = "Monthly" | "Yearly";

export type PaymentOrderStatus =
  | "Pending"
  | "Paid"
  | "Failed"
  | "Expired"
  | "Cancelled"
  | "RefundRequested"
  | "Refunded"
  | "PartiallyRefunded";

export interface PaymentPlanResponse {
  plan: ProPlan;
  /** Kuruş, KDV included. */
  amountMinor: number;
  months: number;
}

export interface PaymentEntitlementResponse {
  isActive: boolean;
  activeUntil: string | null;
}

export interface PaymentPlansResponse {
  currency: string;
  termsVersion: string;
  plans: PaymentPlanResponse[];
  entitlement: PaymentEntitlementResponse;
}

export interface StartCheckoutRequest {
  plan: ProPlan;
  billingName: string;
  billingAddress: string;
  billingPhone: string;
  acceptTerms: boolean;
}

export interface CheckoutResponse {
  orderId: string;
  merchantOid: string;
  iframeUrl: string;
  expiresAt: string;
  plan: ProPlan;
  amountMinor: number;
  currency: string;
}

export interface PaymentOrderResponse {
  id: string;
  plan: ProPlan;
  amountMinor: number;
  currency: string;
  status: PaymentOrderStatus;
  createdAt: string;
  paidAt: string | null;
  tokenExpiresAt: string | null;
  /** PayTR's failed_reason_code; -1 means our own get-token call was refused. */
  failedReasonCode: number | null;
  failedReasonMsg: string | null;
  refundedAmountMinor: number;
  refundRequestedAt: string | null;
  entitlementActiveUntil: string | null;
  canRequestRefund: boolean;
  /** What PayTR actually charged, once paid — differs from amountMinor only on a mismatch. */
  chargedAmountMinor: number | null;
}

/** The billing details from the user's newest order; all null before their first checkout. */
export interface BillingDefaultsResponse {
  billingName: string | null;
  billingAddress: string | null;
  billingPhone: string | null;
}

export interface RequestRefundRequest {
  reason: string;
}

export interface AdminPaymentOrderResponse {
  id: string;
  userId: string | null;
  email: string;
  merchantOid: string;
  plan: ProPlan;
  amountMinor: number;
  totalAmountMinor: number | null;
  currency: string;
  status: PaymentOrderStatus;
  billingName: string;
  billingAddress: string;
  billingPhone: string;
  locale: string;
  termsVersion: string;
  termsAcceptedAt: string;
  paymentType: string | null;
  testMode: boolean;
  amountMismatch: boolean;
  failedReasonCode: number | null;
  failedReasonMsg: string | null;
  createdAt: string;
  updatedAt: string;
  tokenExpiresAt: string | null;
  paidAt: string | null;
  failedAt: string | null;
  cancelledAt: string | null;
  cancelledByUserId: string | null;
  entitlementActiveUntilBefore: string | null;
  entitlementActiveUntilAfter: string | null;
  refundRequestedAt: string | null;
  refundReason: string | null;
  refundedAt: string | null;
  refundedAmountMinor: number;
  refundableAmountMinor: number;
  refundReferenceNo: string | null;
  refundedByUserId: string | null;
  refundRejectedAt: string | null;
  refundRejectionNote: string | null;
  /** What the refund policy says is owed now: everything inside the seven-day window, the unused
   *  share of the period after it, less refunds already made. The panel's default amount. */
  policyRefundMinor: number;
}

export interface PaymentNotificationResponse {
  id: string;
  merchantOid: string;
  orderId: string | null;
  status: string;
  totalAmountMinor: number | null;
  paymentAmountMinor: number | null;
  paymentType: string | null;
  failedReasonCode: number | null;
  failedReasonMsg: string | null;
  testMode: boolean;
  hashValid: boolean;
  outcome: string;
  receivedAt: string;
}

export interface AdminPaymentOrderDetailResponse {
  order: AdminPaymentOrderResponse;
  notifications: PaymentNotificationResponse[];
  entitlement: PaymentEntitlementResponse | null;
}

export interface PaymentsMonthSummary {
  month: string;
  paidCount: number;
  grossMinor: number;
  refundedMinor: number;
  refundCount: number;
  cancelledCount: number;
  failedCount: number;
  netMinor: number;
}

export interface PaymentsSummaryResponse {
  currency: string;
  currentMonth: PaymentsMonthSummary;
  months: PaymentsMonthSummary[];
  openRefundRequests: number;
  activeProUsers: number;
  testOrdersLast30Days: number;
}

/** The five outcomes the alert list shows; Applied, Duplicate and RefundRecorded never appear. */
export type PaymentAlertOutcome = "BadHash" | "UnknownOrder" | "LateApplied" | "Malformed" | "Error";

export type PaymentAlertSortKey = "receivedAt" | "outcome" | "status" | "merchantOid";

export interface PaymentAlertsResponse {
  notifications: PagedResult<PaymentNotificationResponse>;
  amountMismatches: AdminPaymentOrderResponse[];
}

export interface AdminRefundRequest {
  amountMinor: number | null;
}

export interface RejectRefundRequest {
  note: string;
}

export interface MarkRefundedRequest {
  amountMinor: number;
  referenceNo: string;
}

// ---- Company salaries -------------------------------------------------------------------------

export type SalaryCurrency = "TRY" | "EUR" | "USD" | "GBP";

/** Its own pair, not the reviews' EmploymentStatus: an internship is an EmploymentType here. */
export type SalaryEmploymentStatus = "CurrentEmployee" | "FormerEmployee";

/** What readers see instead of the exact years — see CompanySalaryPublic. */
export type ExperienceBand = "ZeroToOne" | "TwoToFour" | "FiveToNine" | "TenPlus";

/** A row of the seeded occupation catalogue, as it rides on every salary response. */
export interface OccupationRef {
  id: string;
  code: string;
  nameTr: string;
  nameEn: string;
}

export type OccupationSearchResult = OccupationRef;

export interface CompanySalaryRequest {
  /** From the catalogue (`/api/occupations/search`); typed text is never accepted. */
  occupationId: string;
  yearsOfExperience: number;
  employmentType: EmploymentType;
  employmentStatus: SalaryEmploymentStatus;
  monthlyNetAmount: number;
  currency: SalaryCurrency;
  hasBonus: boolean;
  annualBonusAmount: number | null;
}

/** Another person's entry, as a signed-in reader sees it: no author, the band instead of the
 *  years, the month instead of the date. */
export interface CompanySalaryPublic {
  id: string;
  occupation: OccupationRef;
  experienceBand: ExperienceBand;
  employmentType: EmploymentType;
  employmentStatus: SalaryEmploymentStatus;
  monthlyNetAmount: number;
  currency: SalaryCurrency;
  annualBonusAmount: number | null;
  /** yyyy-MM */
  submittedMonth: string;
}

/** Per currency; the three figures are null below `minimumForStats`. */
export interface SalaryCurrencyStat {
  currency: SalaryCurrency;
  count: number;
  medianMonthlyNet: number | null;
  minMonthlyNet: number | null;
  maxMonthlyNet: number | null;
}

export interface CompanySalaryPage {
  items: CompanySalaryPublic[];
  total: number;
  page: number;
  pageSize: number;
  stats: SalaryCurrencyStat[];
  minimumForStats: number;
}

/** The author's own row: everything, including the exact years. */
export interface MyCompanySalary {
  id: string;
  companyId: string;
  companySlug: string;
  companyName: string;
  occupation: OccupationRef;
  yearsOfExperience: number;
  employmentType: EmploymentType;
  employmentStatus: SalaryEmploymentStatus;
  monthlyNetAmount: number;
  currency: SalaryCurrency;
  annualBonusAmount: number | null;
  submittedAt: string;
  updatedAt: string;
}

export interface SalaryQuota {
  used: number;
  limit: number;
}

export interface MySalariesResponse {
  items: MyCompanySalary[];
  quota: SalaryQuota;
}

export interface CompanySalaryViewerState {
  ownEntries: MyCompanySalary[];
  quota: SalaryQuota;
}

// ---- Candidate experiences ----------------------------------------------------------------------

/** The nine things a candidate can rate about a hiring process; `Overall` is the one required
 *  rating. Order = form and summary order. */
export type ExperienceCategory =
  | "Overall"
  | "Communication"
  | "ResponseTime"
  | "Punctuality"
  | "InterviewerPreparation"
  | "QuestionRelevance"
  | "Transparency"
  | "AssignmentLoad"
  | "OutcomeCommunication";

/** How the process ended for the candidate, as they report it. `NoResponse` = never told. */
export type HiringOutcome = "Offer" | "Rejected" | "InProgress" | "Withdrew" | "NoResponse";

/** Bands in time order — the "typical duration" is a median over these. */
export type ProcessDuration = "UnderOneWeek" | "OneToTwoWeeks" | "TwoToFourWeeks" | "OneToTwoMonths" | "OverTwoMonths";

export type StageCount = "One" | "Two" | "Three" | "Four" | "FivePlus";

export type InterviewType = "Phone" | "Video" | "OnSite" | "TechnicalTest" | "TakeHomeAssignment" | "Panel" | "AssessmentCenter";

export interface ExperienceCategoryRating {
  category: ExperienceCategory;
  rating: number;
}

/** One shape for create and update. Only `overallRating` is required; everything else may be
 *  omitted, and there is no free text. */
export interface CandidateExperienceRequest {
  overallRating: number;
  categoryRatings?: ExperienceCategoryRating[];
  likedStatements?: string[];
  improvableStatements?: string[];
  outcome?: HiringOutcome | null;
  duration?: ProcessDuration | null;
  stages?: StageCount | null;
  interviewTypes?: InterviewType[];
}

/** Another candidate's entry as anyone sees it: no author, no date beyond the quarter, no job
 *  title — the company knows whom it interviewed in a given month. */
export interface CandidateExperiencePublic {
  id: string;
  overallRating: number;
  categoryRatings: ExperienceCategoryRating[];
  likedStatements: string[];
  improvableStatements: string[];
  outcome: HiringOutcome | null;
  duration: ProcessDuration | null;
  stages: StageCount | null;
  interviewTypes: InterviewType[];
  /** yyyy-Qn */
  submittedQuarter: string;
}

export interface ExperienceCategoryAverage {
  category: ExperienceCategory;
  count: number;
  average: number | null;
}

export interface ExperienceStatementCount {
  key: string;
  count: number;
}

export interface HiringOutcomeCount {
  outcome: HiringOutcome;
  count: number;
}

export interface InterviewTypeCount {
  type: InterviewType;
  count: number;
}

/** `count`, `averageOverall` and `distribution` are real from the first entry; everything else
 *  is null/empty below `minimumForStats`. */
export interface CandidateExperienceSummary {
  count: number;
  score: number | null;
  minimumForStats: number;
  priorWeight: number;
  averageOverall: number | null;
  categories: ExperienceCategoryAverage[];
  distribution: number[];
  topLiked: ExperienceStatementCount[];
  topImprovable: ExperienceStatementCount[];
  outcomes: HiringOutcomeCount[];
  typicalDuration: ProcessDuration | null;
  typicalStages: StageCount | null;
  interviewTypes: InterviewTypeCount[];
  takeHomeAssignmentCount: number;
}

export interface CandidateExperiencePage {
  items: CandidateExperiencePublic[];
  total: number;
  page: number;
  pageSize: number;
  summary: CandidateExperienceSummary;
}

/** The author's own row: everything, including the exact dates. */
export interface MyCandidateExperience {
  id: string;
  companyId: string;
  companySlug: string;
  companyName: string;
  overallRating: number;
  categoryRatings: ExperienceCategoryRating[];
  likedStatements: string[];
  improvableStatements: string[];
  outcome: HiringOutcome | null;
  duration: ProcessDuration | null;
  stages: StageCount | null;
  interviewTypes: InterviewType[];
  submittedAt: string;
  updatedAt: string;
}

export interface ExperienceQuota {
  used: number;
  limit: number;
}

export interface MyCandidateExperiencesResponse {
  items: MyCandidateExperience[];
  quota: ExperienceQuota;
}

export interface CandidateExperienceViewerState {
  ownEntry: MyCandidateExperience | null;
  quota: ExperienceQuota;
}
