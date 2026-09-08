using AfterApply.Application.SiteTraffic.Contracts;
using FluentValidation;

namespace AfterApply.Application.SiteTraffic.Validators;

/// <summary>
/// Length bounds only. The question of *which* events and paths are acceptable belongs to
/// SiteTrafficNormalizer, and it answers it by silently dropping the report rather than by failing
/// validation: a 400 listing what the endpoint accepts would publish the allowlist, and a browser
/// beacon has nothing to do with the answer. What is worth rejecting outright is an oversized body,
/// because that is not a browser at all.
/// </summary>
public sealed class RecordSiteTrafficEventRequestValidator : AbstractValidator<RecordSiteTrafficEventRequest>
{
    public RecordSiteTrafficEventRequestValidator()
    {
        RuleFor(r => r.Event).NotEmpty().MaximumLength(SiteTrafficNormalizer.MaxEventNameLength);
        RuleFor(r => r.Path).NotEmpty().MaximumLength(SiteTrafficNormalizer.MaxPathLength);
        RuleFor(r => r.Referrer).MaximumLength(SiteTrafficNormalizer.MaxReferrerLength);
    }
}
