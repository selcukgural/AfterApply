namespace AfterApply.Application.Matching.Contracts;

public sealed record UpdateCandidateProfileRequest(string CvText, bool OpenAiConsentAccepted);

public sealed record ComputeJobMatchRequest(string JobDescription);
