namespace AfterApply.Application.Matching.Contracts;

public sealed record UpdateCandidateProfileRequest(string CvText);

public sealed record ComputeJobMatchRequest(string JobDescription);
