using AfterApply.Application.Blog.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Domain.Blog;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Blog.Validators;

public sealed class CreateBlogCommentRequestValidator : AbstractValidator<CreateBlogCommentRequest>
{
    public CreateBlogCommentRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Content).NotEmpty()
            .MaximumLength(BlogComment.MaxContentLength)
            .Must(content => content.Trim().Length >= BlogComment.MinContentLength)
            .WithMessage(_ => localizer["VALIDATION_BLOG_COMMENT_TOO_SHORT", BlogComment.MinContentLength]);
    }
}

public sealed class EditBlogCommentRequestValidator : AbstractValidator<EditBlogCommentRequest>
{
    public EditBlogCommentRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Content).NotEmpty()
            .MaximumLength(BlogComment.MaxContentLength)
            .Must(content => content.Trim().Length >= BlogComment.MinContentLength)
            .WithMessage(_ => localizer["VALIDATION_BLOG_COMMENT_TOO_SHORT", BlogComment.MinContentLength]);
    }
}

public sealed class ReportBlogCommentRequestValidator : AbstractValidator<ReportBlogCommentRequest>
{
    public ReportBlogCommentRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(BlogCommentReport.MaxNoteLength);
        // "Other" with no explanation gives the admin nothing to act on.
        RuleFor(x => x.Note).NotEmpty()
            .When(x => x.Reason == BlogCommentReportReason.Other)
            .WithMessage(_ => localizer["VALIDATION_REPORT_NOTE_REQUIRED_FOR_OTHER"]);
    }
}

public sealed class PublicBlogCommentListQueryValidator : AbstractValidator<PublicBlogCommentListQuery>
{
    public PublicBlogCommentListQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
    }
}

public sealed class MyBlogCommentListQueryValidator : AbstractValidator<MyBlogCommentListQuery>
{
    public MyBlogCommentListQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
    }
}

public sealed class AdminBlogCommentListQueryValidator : AbstractValidator<AdminBlogCommentListQuery>
{
    public AdminBlogCommentListQueryValidator()
    {
        RuleFor(x => x.Status!.Value).IsInEnum().When(x => x.Status.HasValue)
            .OverridePropertyName(nameof(AdminBlogCommentListQuery.Status));
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
    }
}
