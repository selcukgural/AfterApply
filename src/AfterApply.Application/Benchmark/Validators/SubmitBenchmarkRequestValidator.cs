using AfterApply.Application.Benchmark.Contracts;
using FluentValidation;

namespace AfterApply.Application.Benchmark.Validators;

public sealed class SubmitBenchmarkRequestValidator : AbstractValidator<SubmitBenchmarkRequest>
{
    /// <summary>Generous rather than tight. Someone who really did send 1,500 applications exists
    /// and should not be told they are lying; what this excludes is the typo and the joke, which is
    /// all a bound can honestly do.</summary>
    private const int MaxApplicationCount = 2000;

    public SubmitBenchmarkRequestValidator()
    {
        RuleFor(r => r.ApplicationCount).NotNull().InclusiveBetween(1, MaxApplicationCount);

        RuleFor(r => r.ReplyCount).NotNull().GreaterThanOrEqualTo(0);

        // The one cross-field rule, and the only one a person plausibly trips by accident.
        RuleFor(r => r.ReplyCount)
            .LessThanOrEqualTo(r => r.ApplicationCount)
            .When(r => r.ApplicationCount.HasValue && r.ReplyCount.HasValue)
            .WithMessage("ReplyCount cannot exceed ApplicationCount.");

        RuleFor(r => r.Sector).NotNull().IsInEnum();
        RuleFor(r => r.Period).NotNull().IsInEnum();

        RuleFor(r => r.Seniority).IsInEnum().When(r => r.Seniority.HasValue);
        RuleFor(r => r.Location).IsInEnum().When(r => r.Location.HasValue);

        RuleFor(r => r.Locale).NotEmpty().Must(l => l is "tr" or "en");

        // Filled means something walked the form filling every input. A human never sees it.
        RuleFor(r => r.Website).Empty();
    }
}
