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
