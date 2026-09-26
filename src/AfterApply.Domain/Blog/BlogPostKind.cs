namespace AfterApply.Domain.Blog;

/// <summary>
/// Which section of the site a post belongs to (DECISIONS.md 2026-09-26). The guide is written
/// in the same editor, with the same draft/publish slots, as the blog — only where it is shown
/// differs: <c>/blog</c> or <c>/guide</c>. Set when the post is created and never changed after:
/// the kind is part of the URL, like the language and the slug.
/// </summary>
public enum BlogPostKind
{
    Blog = 0,
    Guide = 1
}
