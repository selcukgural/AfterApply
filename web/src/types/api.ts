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
  sortBy?: ApplicationListSortBy;
  sortDirection?: SortDirection;
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

export interface ClientConfigResponse {
  passwordPolicy: PasswordPolicy;
  personalAccessTokens: PersonalAccessTokenLimits;
  googleAuth: GoogleAuthConfig;
  linkedInAuth: LinkedInAuthConfig;
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
