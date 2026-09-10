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

export interface ClientConfigResponse {
  passwordPolicy: PasswordPolicy;
  personalAccessTokens: PersonalAccessTokenLimits;
  googleAuth: GoogleAuthConfig;
  linkedInAuth: LinkedInAuthConfig;
  gitHubAuth: GitHubAuthConfig;
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
}
