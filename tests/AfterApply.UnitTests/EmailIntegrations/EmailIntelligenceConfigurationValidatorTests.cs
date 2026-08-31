using AfterApply.Application.EmailIntegrations;
using AfterApply.Infrastructure.EmailIntegrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AfterApply.UnitTests.EmailIntegrations;

public class EmailIntelligenceConfigurationValidatorTests
{
    // The bound options object passed to Validate() is irrelevant to this validator — it checks the
    // raw IConfiguration (see the validator's own doc comment on why: an unset int would otherwise
    // bind to 0, indistinguishable from a deliberately configured 0). A minimal placeholder is enough.
    private static readonly EmailIntelligenceOptions IgnoredBoundOptions = new()
    {
        LowThreshold = 0, LlmThreshold = 0, HighConfidenceThreshold = 0,
        Weights = new()
        {
            MatchedApplication = 0, ApplicationPhrase = 0, InterviewPhrase = 0, AssessmentPhrase = 0, OfferPhrase = 0,
            RecruiterSignal = 0, KnownJobBoardOrAts = 0, CalendarLink = 0, ApplicationLink = 0, CompanyNameInSubject = 0,
            Newsletter = 0, Unsubscribe = 0, Marketing = 0, JobAlert = 0, Digest = 0,
            ApplicationCap = 0, InterviewCap = 0, AssessmentCap = 0, OfferCap = 0, RecruiterCap = 0,
            AtsCap = 0, CompanyMatchCap = 0, LinksCap = 0, NegativeCap = 0
        },
        Phrases = new()
        {
            Application = [], Interview = [], Assessment = [], Offer = [], Recruiter = [],
            RecruiterLocalPartPrefixes = [], RecruiterLocalPartExact = [], Newsletter = [], Unsubscribe = [],
            Marketing = [], JobAlert = [], Digest = [], AtsLinkDomains = [], CalendarLinkDomains = []
        }
    };

    [Fact]
    public void Validate_Fully_Populated_Configuration_Succeeds()
    {
        var configuration = BuildConfiguration(CompleteEmailIntelligenceJson);
        var validator = new EmailIntelligenceConfigurationValidator(configuration);

        var result = validator.Validate(name: null, IgnoredBoundOptions);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Missing_Weight_Fails_With_The_Specific_Key_Named()
    {
        // Same as the complete JSON but with Weights:OfferPhrase removed entirely.
        const string json = """
            {
              "EmailIntelligence": {
                "LowThreshold": 20, "LlmThreshold": 50, "HighConfidenceThreshold": 70,
                "Weights": { "MatchedApplication": 35 },
                "Phrases": { "Application": ["x"] }
              }
            }
            """;
        var configuration = BuildConfiguration(json);
        var validator = new EmailIntelligenceConfigurationValidator(configuration);

        var result = validator.Validate(name: null, IgnoredBoundOptions);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("EmailIntelligence:Weights:OfferPhrase");
    }

    [Fact]
    public void Validate_Empty_Phrase_Array_Fails()
    {
        const string json = """
            {
              "EmailIntelligence": {
                "LowThreshold": 20, "LlmThreshold": 50, "HighConfidenceThreshold": 70,
                "Weights": { "MatchedApplication": 35 },
                "Phrases": { "Application": [] }
              }
            }
            """;
        var configuration = BuildConfiguration(json);
        var validator = new EmailIntelligenceConfigurationValidator(configuration);

        var result = validator.Validate(name: null, IgnoredBoundOptions);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("EmailIntelligence:Phrases:Application");
    }

    [Fact]
    public void Validate_Entirely_Missing_Section_Fails_With_Every_Key_Listed()
    {
        var configuration = BuildConfiguration("{}");
        var validator = new EmailIntelligenceConfigurationValidator(configuration);

        var result = validator.Validate(name: null, IgnoredBoundOptions);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("EmailIntelligence:LlmThreshold");
        result.FailureMessage.ShouldContain("EmailIntelligence:Weights:InterviewPhrase");
        result.FailureMessage.ShouldContain("EmailIntelligence:Phrases:Interview");
    }

    private static IConfiguration BuildConfiguration(string json)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return new ConfigurationBuilder().AddJsonStream(stream).Build();
    }

    private const string CompleteEmailIntelligenceJson = """
        {
          "EmailIntelligence": {
            "LowThreshold": 20,
            "LlmThreshold": 50,
            "HighConfidenceThreshold": 70,
            "Weights": {
              "MatchedApplication": 35, "ApplicationPhrase": 30, "InterviewPhrase": 35, "AssessmentPhrase": 30,
              "OfferPhrase": 35, "RecruiterSignal": 10, "KnownJobBoardOrAts": 20, "CalendarLink": 15,
              "ApplicationLink": 10, "CompanyNameInSubject": 20, "Newsletter": -25, "Unsubscribe": -25,
              "Marketing": -25, "JobAlert": -30, "Digest": -20, "ApplicationCap": 35, "InterviewCap": 40,
              "AssessmentCap": 35, "OfferCap": 35, "RecruiterCap": 15, "AtsCap": 20, "CompanyMatchCap": 35,
              "LinksCap": 20, "NegativeCap": -35
            },
            "Phrases": {
              "Application": ["a"], "Interview": ["b"], "Assessment": ["c"], "Offer": ["d"], "Recruiter": ["e"],
              "RecruiterLocalPartPrefixes": ["f"], "RecruiterLocalPartExact": ["g"], "Newsletter": ["h"],
              "Unsubscribe": ["i"], "Marketing": ["j"], "JobAlert": ["k"], "Digest": ["l"],
              "AtsLinkDomains": ["m"], "CalendarLinkDomains": ["n"]
            }
          }
        }
        """;
}
