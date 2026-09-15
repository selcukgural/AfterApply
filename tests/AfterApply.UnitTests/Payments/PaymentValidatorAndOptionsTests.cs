using AfterApply.Application.Localization;
using AfterApply.Application.Payments.Contracts;
using AfterApply.Application.Payments.Validators;
using AfterApply.Infrastructure.Payments;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace AfterApply.UnitTests.Payments;

public sealed class StartCheckoutRequestValidatorTests
{
    private readonly StartCheckoutRequestValidator _validator = new(new KeyEchoLocalizer());

    private static StartCheckoutRequest Valid() => new("monthly", "Ada Lovelace", "Somewhere 1, İstanbul", "+90 555 000 00 00", true);

    [Fact]
    public void A_complete_request_passes()
    {
        _validator.Validate(Valid()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Monthly")]
    [InlineData("YEARLY")]
    public void Plan_names_are_case_insensitive(string plan)
    {
        _validator.Validate(Valid() with { Plan = plan }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Unknown_plan_is_refused()
    {
        _validator.Validate(Valid() with { Plan = "lifetime" }).Errors.ShouldContain(e => e.ErrorMessage == "PAYMENT_PLAN_UNKNOWN");
    }

    [Fact]
    public void Terms_must_be_accepted()
    {
        _validator.Validate(Valid() with { AcceptTerms = false }).Errors.ShouldContain(e => e.ErrorMessage == "PAYMENT_TERMS_REQUIRED");
    }

    [Fact]
    public void Billing_fields_have_paytr_length_caps()
    {
        _validator.Validate(Valid() with { BillingName = new string('a', 61) }).Errors.ShouldContain(e => e.ErrorMessage == "VALIDATION_BILLING_NAME");
        _validator.Validate(Valid() with { BillingAddress = new string('a', 401) }).Errors.ShouldContain(e => e.ErrorMessage == "VALIDATION_BILLING_ADDRESS");
        _validator.Validate(Valid() with { BillingPhone = "+90 555 000 00 00 000 000" }).Errors.ShouldContain(e => e.ErrorMessage == "VALIDATION_BILLING_PHONE");
    }

    [Theory]
    [InlineData("")]
    [InlineData("asdf")]
    [InlineData("555 abc")]
    public void Phone_needs_digits_and_only_phone_punctuation(string phone)
    {
        _validator.Validate(Valid() with { BillingPhone = phone }).Errors.ShouldContain(e => e.ErrorMessage == "VALIDATION_BILLING_PHONE");
    }

    [Fact]
    public void Control_characters_are_refused_in_name_and_address()
    {
        _validator.Validate(Valid() with { BillingName = "Ada" + (char)7 }).Errors.ShouldContain(e => e.ErrorMessage == "VALIDATION_BILLING_NAME");
    }

    private sealed class KeyEchoLocalizer : IStringLocalizer<SharedStrings>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}

public sealed class PayTrOptionsValidatorTests
{
    private readonly PayTrOptionsValidator _validator = new();

    [Fact]
    public void Disabled_options_pass_whatever_they_contain()
    {
        _validator.Validate(null, new PayTrOptions()).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Enabled_needs_secrets_and_positive_prices()
    {
        var result = _validator.Validate(null, new PayTrOptions { Enabled = true });

        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(f => f.Contains("MerchantId"));
        result.Failures!.ShouldContain(f => f.Contains("Monthly"));
        result.Failures!.ShouldContain(f => f.Contains("Yearly"));
    }

    [Fact]
    public void Enabled_and_complete_passes()
    {
        var options = new PayTrOptions
        {
            Enabled = true,
            MerchantId = "1",
            MerchantKey = "k",
            MerchantSalt = "s",
            Plans = new PayTrPlanPrices { Monthly = new PayTrPlanPrice { AmountMinor = 29900 }, Yearly = new PayTrPlanPrice { AmountMinor = 299000 } }
        };

        _validator.Validate(null, options).Succeeded.ShouldBeTrue();
        options.IsConfigured.ShouldBeTrue();
    }
}

public sealed class PaymentFormattingTests
{
    [Theory]
    [InlineData("ada@example.com", true)]
    [InlineData("ayşe@example.com", false)]
    [InlineData("", false)]
    public void Only_ascii_emails_are_accepted_by_paytr(string email, bool expected)
    {
        PaymentFormatting.IsAsciiEmail(email).ShouldBe(expected);
    }

    [Fact]
    public void Amounts_and_dates_read_naturally_in_each_locale()
    {
        PaymentFormatting.Amount(29900, "tr").ShouldBe("299,00 ₺");
        PaymentFormatting.Amount(29900, "en").ShouldBe("₺299.00");

        var at = new DateTimeOffset(2026, 10, 15, 21, 30, 0, TimeSpan.Zero); // 16 Oct 00:30 in Istanbul
        PaymentFormatting.Date(at, "tr").ShouldBe("16 Ekim 2026");
        PaymentFormatting.Date(at, "en").ShouldBe("16 October 2026");
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("tr", "tr")]
    [InlineData("de", "tr")]
    [InlineData(null, "tr")]
    public void Locale_falls_back_to_turkish(string? locale, string expected)
    {
        PaymentFormatting.NormalizeLocale(locale).ShouldBe(expected);
    }
}
