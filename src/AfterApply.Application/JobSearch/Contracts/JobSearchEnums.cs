namespace AfterApply.Application.JobSearch.Contracts;

// The typed forms of JSearch's enumerated query parameters. The wire strings live in
// Infrastructure (JSearchWireValues); these names are what the API accepts and returns. Values the
// provider *produces* (employment type of a posting, salary period, seniority) stay strings on the
// response DTOs so an unlisted upstream value never breaks deserialisation.

/// <summary>JSearch <c>date_posted</c>.</summary>
public enum JobSearchDatePosted
{
    All,
    Today,
    ThreeDays,
    Week,
    Month
}

/// <summary>JSearch <c>employment_types</c> (comma-delimited upstream).</summary>
public enum JobSearchEmploymentType
{
    FullTime,
    Contractor,
    PartTime,
    Intern
}

/// <summary>JSearch <c>job_requirements</c> (comma-delimited upstream).</summary>
public enum JobSearchJobRequirement
{
    Under3YearsExperience,
    MoreThan3YearsExperience,
    NoExperience,
    NoDegree
}

/// <summary>JSearch <c>location_type</c> on the two salary endpoints.</summary>
public enum JobSearchLocationType
{
    Any,
    City,
    State,
    Country
}

/// <summary>JSearch <c>years_of_experience</c> on the two salary endpoints.</summary>
public enum JobSearchExperienceRange
{
    All,
    LessThanOne,
    OneToThree,
    FourToSix,
    SevenToNine,
    TenToFourteen,
    AboveFifteen
}
