using AfterApply.Domain.JobSources;
using Shouldly;

namespace AfterApply.UnitTests.JobSources;

public class UserJobSourceProfileTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-14T04:00:00Z");

    [Fact]
    public void Re_Saving_Keeps_Surviving_Links_And_Reorders()
    {
        var profile = UserJobSourceProfile.Create(Guid.NewGuid(), "İstanbul", false, true, Now);
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        profile.SetQueries([(".NET Developer", a), ("Java Developer", b)]);
        var linkA = profile.Queries.Single(q => q.QueryId == a);

        profile.SetQueries([("Backend Developer", c), (".NET Geliştirici", a)]);

        profile.OrderedQueries.Select(q => (q.Title, q.QueryId)).ShouldBe([("Backend Developer", c), (".NET Geliştirici", a)]);
        // The same row, renamed and moved — not a delete and an insert of the same key.
        profile.Queries.Single(q => q.QueryId == a).ShouldBeSameAs(linkA);
        profile.Queries.ShouldNotContain(q => q.QueryId == b);
    }

    [Fact]
    public void Never_More_Than_Three()
    {
        var profile = UserJobSourceProfile.Create(Guid.NewGuid(), "İstanbul", false, true, Now);

        profile.SetQueries(Enumerable.Range(0, 5).Select(i => ($"T{i}", Guid.NewGuid())));

        profile.Queries.Count.ShouldBe(UserJobSourceProfile.MaxTitles);
        profile.OrderedQueries.Select(q => q.Ordinal).ShouldBe([0, 1, 2]);
    }
}
