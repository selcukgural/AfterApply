namespace AfterApply.Infrastructure.CompanyIntelligence;

public sealed class CompanyIntelligenceOptions
{
    public bool Enabled { get; init; } = false;

    public int HiddenBelow { get; init; } = 20;

    public int VeryLowBelow { get; init; } = 50;

    public int LowBelow { get; init; } = 200;

    public int MediumBelow { get; init; } = 1000;
}
