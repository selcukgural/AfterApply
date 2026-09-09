using AfterApply.Infrastructure.Identity;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

// The two decisions that are GitHub's alone: which of an account's addresses may stand as its
// email, and how one free-text profile name becomes a first/last pair. Everything downstream
// (matching an existing account, whether the sign-up form demands an email) hangs off the first.
public class GitHubProfileReaderTests
{
    private static readonly GitHubProfile Profile = new(4241, "ada", "Augusta Ada King");

    [Fact]
    public void Uses_The_Numeric_Id_As_The_Subject_Never_The_Login()
    {
        var identity = GitHubProfileReader.Read(Profile, []);

        // A login can be renamed and the old one handed to someone else; the id cannot.
        identity.Subject.ShouldBe("4241");
        identity.Subject.ShouldNotBe("ada");
    }

    [Fact]
    public void Prefers_The_Primary_Verified_Address()
    {
        var email = GitHubProfileReader.SelectEmail([
            new GitHubEmail("other@example.com", Primary: false, Verified: true),
            new GitHubEmail("ada@example.com", Primary: true, Verified: true)
        ]);

        email.ShouldBe("ada@example.com");
    }

    [Fact]
    public void Falls_Back_To_Another_Verified_Address_When_The_Primary_Is_Not_Verified()
    {
        var email = GitHubProfileReader.SelectEmail([
            new GitHubEmail("unverified@example.com", Primary: true, Verified: false),
            new GitHubEmail("verified@example.com", Primary: false, Verified: true)
        ]);

        // Just as proven as a primary one — GitHub verified it either way.
        email.ShouldBe("verified@example.com");
    }

    [Fact]
    public void Never_Returns_An_Unverified_Address()
    {
        var email = GitHubProfileReader.SelectEmail([
            new GitHubEmail("unverified@example.com", Primary: true, Verified: false)
        ]);

        // Accepting this would let anyone claim an existing e-kariyerim account by adding its
        // address to their own GitHub profile.
        email.ShouldBeNull();
    }

    [Theory]
    [InlineData("4241+ada@users.noreply.github.com")]
    [InlineData("ada@users.noreply.github.com")]
    [InlineData("ADA@Users.NoReply.GitHub.com")]
    public void Never_Returns_A_Noreply_Address_Even_When_Verified(string noReply)
    {
        var email = GitHubProfileReader.SelectEmail([new GitHubEmail(noReply, Primary: true, Verified: true)]);

        // Verified and unique, but undeliverable — an account created under it could never receive
        // a password reset.
        email.ShouldBeNull();
    }

    [Fact]
    public void Falls_Back_Past_A_Noreply_Address_To_A_Real_One()
    {
        var email = GitHubProfileReader.SelectEmail([
            new GitHubEmail("4241+ada@users.noreply.github.com", Primary: true, Verified: true),
            new GitHubEmail("ada@example.com", Primary: false, Verified: true)
        ]);

        email.ShouldBe("ada@example.com");
    }

    [Fact]
    public void Reports_No_Email_When_The_Address_List_Could_Not_Be_Read()
    {
        // The user:email scope was not granted — not a failure, just the emailless sign-up path.
        var identity = GitHubProfileReader.Read(Profile, null);

        identity.Email.ShouldBeNull();
        identity.EmailVerified.ShouldBeFalse();
    }

    [Fact]
    public void Marks_A_Selected_Address_As_Verified()
    {
        var identity = GitHubProfileReader.Read(Profile, [new GitHubEmail("ada@example.com", true, true)]);

        identity.Email.ShouldBe("ada@example.com");
        identity.EmailVerified.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Augusta Ada King", "Augusta Ada", "King")]
    [InlineData("Selçuk Güral", "Selçuk", "Güral")]
    [InlineData("  Ada   King  ", "Ada", "King")]
    public void Splits_A_Display_Name_On_The_Last_Space(string name, string given, string family)
    {
        GitHubProfileReader.SplitName(name, "ada").ShouldBe((given, family));
    }

    [Fact]
    public void Leaves_The_Surname_Empty_For_A_Single_Word_Name()
    {
        GitHubProfileReader.SplitName("Ada", "ada").ShouldBe(("Ada", (string?)null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Falls_Back_To_The_Login_When_The_Profile_Has_No_Name(string? name)
    {
        // Better than an empty form; both fields are editable before the account exists.
        GitHubProfileReader.SplitName(name, "ada").ShouldBe(("ada", (string?)null));
    }

    [Fact]
    public void Reports_No_Name_At_All_When_There_Is_Neither_A_Name_Nor_A_Login()
    {
        GitHubProfileReader.SplitName(null, null).ShouldBe(((string?)null, (string?)null));
    }
}
