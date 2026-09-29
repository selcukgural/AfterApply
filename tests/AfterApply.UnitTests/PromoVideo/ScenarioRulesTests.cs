using PromoVideo;
using Shouldly;

namespace AfterApply.UnitTests.PromoVideo;

public class ScenarioRulesTests
{
    private static Scenario Valid() => new()
    {
        Title = "t",
        Scenes = [new Scene { Id = "a", Narration = "Merhaba.", Steps = [new Step { Action = "goto", Path = "/tr" }] }]
    };

    [Fact]
    public void A_Minimal_Scenario_Is_Valid()
    {
        ScenarioRules.Validate(Valid()).ShouldBeEmpty();
    }

    [Fact]
    public void The_Shipped_Example_Scenarios_Are_Valid()
    {
        // A tripwire for the files people copy from: they must parse and pass the rules.
        var directory = Path.Combine(RepositoryRoot(), "tools", "PromoVideo", "scenarios");
        var files = Directory.GetFiles(directory, "*.json");
        files.ShouldNotBeEmpty();
        foreach (var file in files)
        {
            ScenarioRules.Validate(ScenarioFile.Parse(File.ReadAllText(file))).ShouldBeEmpty(file);
        }
    }

    [Theory]
    [InlineData("http://localhost:3000")]
    [InlineData("http://127.0.0.1:3000")]
    [InlineData("http://ekariyerim.test")]
    [InlineData("http://web.localhost")]
    public void Signing_In_Is_Allowed_Against_A_Local_Stack(string baseUrl)
    {
        ScenarioRules.Validate(Valid() with { BaseUrl = baseUrl, SignIn = true }).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("https://ekariyerim.com")]
    [InlineData("https://localhost.evil.com")]
    public void Signing_In_Is_Refused_Against_Any_Other_Host(string baseUrl)
    {
        // A signed-in recording of a real account would put real applications in a public video.
        ScenarioRules.Validate(Valid() with { BaseUrl = baseUrl, SignIn = true })
            .ShouldContain(e => e.StartsWith("signIn is only allowed", StringComparison.Ordinal));
        ScenarioRules.Validate(Valid() with { BaseUrl = baseUrl }).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("tr/dashboard")]
    public void Goto_Takes_Only_A_Path_On_The_Scenario_Site(string path)
    {
        var scenario = Valid() with
        {
            Scenes = [new Scene { Id = "a", Narration = "x", Steps = [new Step { Action = "goto", Path = path }] }]
        };

        ScenarioRules.Validate(scenario).ShouldHaveSingleItem().ShouldContain("goto needs a path");
    }

    [Fact]
    public void Every_Step_Problem_Is_Reported_With_Its_Place()
    {
        var scenario = Valid() with
        {
            Scenes =
            [
                new Scene
                {
                    Id = "a",
                    Narration = "x",
                    Steps =
                    [
                        new Step { Action = "fly" },
                        new Step { Action = "click" },
                        new Step { Action = "type", Target = "input" },
                        new Step { Action = "press", Key = "F5" },
                        new Step { Action = "scroll", Target = "#a", By = 100 },
                        new Step { Action = "wait", Seconds = 0 }
                    ]
                }
            ]
        };

        var errors = ScenarioRules.Validate(scenario);

        errors.Count.ShouldBe(6);
        errors[0].ShouldStartWith("scenes[0].steps[0]: unknown action 'fly'");
        errors[1].ShouldBe("scenes[0].steps[1]: click needs a target.");
        errors[2].ShouldBe("scenes[0].steps[2]: type needs a target and text.");
        errors[3].ShouldStartWith("scenes[0].steps[3]: press needs a key");
        errors[4].ShouldBe("scenes[0].steps[4]: scroll needs exactly one of target or by.");
        errors[5].ShouldBe("scenes[0].steps[5]: wait needs seconds between 0 and 60.");
    }

    [Fact]
    public void Scenes_Need_Unique_Ids_And_Either_Narration_Or_A_Length()
    {
        var scenario = Valid() with
        {
            Scenes = [new Scene { Id = "a", Narration = "x" }, new Scene { Id = "a" }, new Scene { Id = "b", MinSeconds = 3 }]
        };

        ScenarioRules.Validate(scenario).ShouldBe(
        [
            "scenes[1]: id 'a' is used twice.",
            "scenes[1]: a scene without narration needs minSeconds."
        ]);
    }

    [Fact]
    public void Google_Needs_A_Voice_Name_And_Piper_A_Model()
    {
        ScenarioRules.Validate(Valid() with { Voice = new VoiceSettings { Provider = "google" } })
            .ShouldHaveSingleItem().ShouldStartWith("voice.name is required for google");
        ScenarioRules.Validate(Valid() with { Voice = new VoiceSettings { Provider = "piper" } })
            .ShouldHaveSingleItem().ShouldStartWith("voice.model");
        ScenarioRules.Validate(Valid() with { Voice = new VoiceSettings { Provider = "elevenlabs" } })
            .ShouldHaveSingleItem().ShouldStartWith("voice.provider must be one of");
    }

    [Fact]
    public void The_Output_Size_Must_Be_Even_For_The_H264_Encoder()
    {
        ScenarioRules.Validate(Valid() with { Output = new OutputSettings { Width = 1921 } })
            .ShouldHaveSingleItem().ShouldStartWith("output width/height must be even");
    }

    [Fact]
    public void The_File_Allows_Comments_And_Trailing_Commas_But_Not_Unknown_Fields()
    {
        var scenario = ScenarioFile.Parse("""
            // a comment
            { "title": "x", "scenes": [ { "id": "a", "narration": "Merhaba.", }, ], }
            """);
        scenario.Scenes.ShouldHaveSingleItem().Narration.ShouldBe("Merhaba.");

        // A typo ("narations") must fail loudly instead of rendering a silent scene.
        Should.Throw<System.Text.Json.JsonException>(() => ScenarioFile.Parse("""{ "scenes": [ { "id": "a", "narations": "x" } ] }"""));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AfterApply.slnx")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
    }
}

public class ScenarioExtrasTests
{
    private static Scenario Valid() => new()
    {
        Scenes = [new Scene { Id = "a", Narration = "Merhaba.", Steps = [new Step { Action = "card", Title = "e-kariyerim" }] }]
    };

    [Fact]
    public void Loanwords_Are_Said_As_Whole_Words_Only_And_The_Longest_Match_Wins()
    {
        var pronounce = new Dictionary<string, string>
        {
            ["CV"] = "sivi",
            ["e-kariyerim"] = "e kariyerim",
            ["ekariyerim.com"] = "e kariyerim nokta kom"
        };

        ScenarioRules.Spoken("CV'ni tara, CV tarama ücretsiz.", pronounce).ShouldBe("sivi'ni tara, sivi tarama ücretsiz.");
        // Not inside another word or token.
        ScenarioRules.Spoken("CVS ve MyCV aynı kalır.", pronounce).ShouldBe("CVS ve MyCV aynı kalır.");
        ScenarioRules.Spoken("ekariyerim.com ve e-kariyerim", pronounce).ShouldBe("e kariyerim nokta kom ve e kariyerim");
    }

    [Fact]
    public void A_Card_Needs_A_Title()
    {
        ScenarioRules.Validate(Valid()).ShouldBeEmpty();
        ScenarioRules.Validate(Valid() with
        {
            Scenes = [new Scene { Id = "a", Narration = "x", Steps = [new Step { Action = "card" }, new Step { Action = "hideCard" }] }]
        }).ShouldBe(["scenes[0].steps[0]: card needs a title."]);
    }

    [Theory]
    [InlineData(null, null, 0.2, "music needs exactly one of generate or file.")]
    [InlineData("ambient", "a.mp3", 0.2, "music needs exactly one of generate or file.")]
    [InlineData("techno", null, 0.2, "music.generate must be \"ambient\".")]
    [InlineData("ambient", null, 0, "music.volume must be between 0 and 1.")]
    public void Music_Is_Either_Generated_Or_A_File_At_A_Sane_Level(string? generate, string? file, double volume, string error)
    {
        ScenarioRules.Validate(Valid() with { Music = new MusicSettings { Generate = generate, File = file, Volume = volume } })
            .ShouldBe([error]);
        ScenarioRules.Validate(Valid() with { Music = new MusicSettings { Generate = "ambient" } }).ShouldBeEmpty();
    }
}
