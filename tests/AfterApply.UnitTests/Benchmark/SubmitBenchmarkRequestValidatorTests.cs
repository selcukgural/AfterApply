using AfterApply.Application.Benchmark.Contracts;
using AfterApply.Application.Benchmark.Validators;
using AfterApply.Domain.Benchmark;
using Shouldly;

namespace AfterApply.UnitTests.Benchmark;

public sealed class SubmitBenchmarkRequestValidatorTests
{
    private readonly SubmitBenchmarkRequestValidator _validator = new();

    private static SubmitBenchmarkRequest Valid() => new(
        60, 12, BenchmarkSector.SoftwareAndIt, BenchmarkPeriod.LastSixMonths, null, null, "tr", null);

    [Fact]
    public void An_answer_without_a_channel_passes()
    {
        // Every page shipped before the channel existed sends exactly this.
        _validator.Validate(Valid()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(BenchmarkSource.X)]
    [InlineData(BenchmarkSource.Eksi)]
    [InlineData(BenchmarkSource.Share)]
    public void A_listed_channel_passes(BenchmarkSource source)
    {
        _validator.Validate(Valid() with { Source = source }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void A_channel_outside_the_enum_is_refused()
    {
        _validator.Validate(Valid() with { Source = (BenchmarkSource)99 }).IsValid.ShouldBeFalse();
    }
}
