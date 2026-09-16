namespace AfterApply.Domain.CompanySalaries;

/// <summary>
/// Whether the author drew this salary at the time of writing. Deliberately its own enum rather
/// than the reviews' <c>EmploymentStatus</c>: an internship is an <see cref="Common.EmploymentType"/>
/// here, not a third relationship, so the two lists must not drift together.
/// </summary>
public enum SalaryEmploymentStatus
{
    CurrentEmployee,
    FormerEmployee
}
