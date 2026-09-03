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

export interface AnalyticsOverviewResponse {
  rates: AnalyticsRatesResponse;
  responseTime: ResponseTimeStatsResponse;
  statusDistribution: StatusDistributionItem[];
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
}

export interface UpdateApplicationRequest {
  jobTitle: string;
  jobUrl: string | null;
  location: string | null;
  employmentType: EmploymentType;
  appliedAt: string;
  notes: string | null;
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

export interface PersonalAccessTokenResponse {
  id: string;
  name: string;
  createdAt: string;
  lastUsedAt: string | null;
}

export interface CreatedPersonalAccessTokenResponse {
  id: string;
  name: string;
  token: string;
  createdAt: string;
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
  jobTitle: string;
  jobUrl: string | null;
  location: string | null;
  notes: string | null;
  addedAt: string;
}

export interface CreateTrackedJobRequest {
  companyName: string;
  jobTitle: string;
  jobUrl: string | null;
  location: string | null;
  notes: string | null;
}

export interface ConvertTrackedJobRequest {
  employmentType: EmploymentType;
  appliedAt: string;
  notes: string | null;
}
