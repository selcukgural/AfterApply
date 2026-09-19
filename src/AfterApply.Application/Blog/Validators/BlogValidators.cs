using System.Text.Json;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Domain.Blog;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Blog.Validators;

public sealed class CreateBlogPostRequestValidator : AbstractValidator<CreateBlogPostRequest>
{
    public CreateBlogPostRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Language)
            .Must(BlogLanguage.IsSupported)
            .WithMessage(_ => localizer["VALIDATION_UNSUPPORTED_LANGUAGE"]);
    }
}

/// <summary>Field by field so the ProblemDetails names the field the editor has to highlight.
/// The domain's <see cref="BlogDraftContent.Validate"/> repeats the length caps at the boundary
/// that stores the row; this layer is the one that speaks the user's language.</summary>
public sealed class SaveBlogDraftRequestValidator : AbstractValidator<SaveBlogDraftRequest>
{
    public SaveBlogDraftRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Title).NotNull().MaximumLength(BlogPost.MaxTitleLength);
        RuleFor(x => x.Excerpt).MaximumLength(BlogPost.MaxExcerptLength);
        RuleFor(x => x.ContentHtml).NotNull().MaximumLength(BlogPost.MaxContentHtmlLength);
        RuleFor(x => x.ContentJson)
            .NotNull()
            .MaximumLength(BlogPost.MaxContentJsonLength)
            // The editor's document, opaque to the server except for this: it has to be the one
            // shape the editor can reopen, or the next visit to the post would open a blank page.
            .Must(BeAnEditorDocument)
            .WithMessage(_ => localizer["VALIDATION_BLOG_CONTENT_JSON_INVALID"]);
        RuleFor(x => x.Language)
            .Must(BlogLanguage.IsSupported)
            .WithMessage(_ => localizer["VALIDATION_UNSUPPORTED_LANGUAGE"]);
        // Blank is "none" (the editor sends the field back empty before the first publish and
        // the locked value after it); anything else has to be a slug the generator could make.
        RuleFor(x => x.Slug)
            .Must(slug => string.IsNullOrWhiteSpace(slug) || BlogSlugGenerator.IsValid(slug.Trim()))
            .WithMessage(_ => localizer["BLOG_SLUG_INVALID"]);
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(1);
    }

    private static bool BeAnEditorDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("type", out var type)
                   && type.ValueKind == JsonValueKind.String
                   && type.GetString() == "doc";
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public sealed class PublicBlogListQueryValidator : AbstractValidator<PublicBlogListQuery>
{
    public PublicBlogListQueryValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Lang)
            .Must(BlogLanguage.IsSupported)
            .WithMessage(_ => localizer["VALIDATION_UNSUPPORTED_LANGUAGE"]);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
    }
}

public sealed class AdminBlogListQueryValidator : AbstractValidator<AdminBlogListQuery>
{
    public AdminBlogListQueryValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Status!.Value).IsInEnum().When(x => x.Status.HasValue)
            .OverridePropertyName(nameof(AdminBlogListQuery.Status));
        RuleFor(x => x.Lang)
            .Must(lang => lang is null || BlogLanguage.IsSupported(lang))
            .WithMessage(_ => localizer["VALIDATION_UNSUPPORTED_LANGUAGE"]);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
    }
}
