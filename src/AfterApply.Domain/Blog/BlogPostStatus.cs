namespace AfterApply.Domain.Blog;

/// <summary>
/// Where a post is on the public site. Only two states on purpose: a post is either on the site
/// or it is not. "Unpublished after being live" is <see cref="Draft"/> with a
/// <c>PublishedAt</c> — the snapshot and the slug are kept so it can go back up at the same URL.
/// </summary>
public enum BlogPostStatus
{
    Draft = 0,
    Published = 1
}
